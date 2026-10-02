"""Private, bounded background removal. No URLs, persisted uploads, or runtime downloads."""
from contextlib import asynccontextmanager
from dataclasses import dataclass
from io import BytesIO
from pathlib import Path
from threading import BoundedSemaphore
from typing import Callable
import hmac
import json
import logging
import os
import warnings
from http import HTTPStatus
from uuid import uuid4

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from starlette.exceptions import HTTPException
from fastapi.responses import JSONResponse, Response
from PIL import Image, ImageChops, ImageOps, UnidentifiedImageError
from starlette.concurrency import run_in_threadpool

from model_store import MODEL_NAME, verified_model

MAX_INPUT_BYTES = 20 * 1024 * 1024
MAX_PIXELS = 20_000_000
MAX_DIMENSION = 8192
MAX_OUTPUT_BYTES = 20 * 1024 * 1024
ALLOWED = {"image/png": "PNG", "image/jpeg": "JPEG", "image/webp": "WEBP"}
Image.MAX_IMAGE_PIXELS = MAX_PIXELS


@dataclass(frozen=True)
class Settings:
    key: str
    model_directory: Path

    @classmethod
    def environment(cls):
        settings_file = Path(os.environ.get("IMAGE_WORKER_SETTINGS_FILE", "/app/appsettings.Local.json"))
        config = json.loads(settings_file.read_text()) if settings_file.exists() else {}
        section = config.get("ImageWorker", {}) if isinstance(config, dict) else {}
        if not isinstance(section, dict):
            raise ValueError("ImageWorker settings must be an object.")
        key_file = os.environ.get("IMAGE_WORKER_KEY_FILE")
        key = Path(key_file).read_text().strip() if key_file else os.environ.get("IMAGE_WORKER_KEY", section.get("Key", ""))
        if not isinstance(key, str) or len(key) < 32:
            raise ValueError("Configure a private worker key of at least 32 characters.")
        return cls(key, Path(os.environ.get("IMAGE_WORKER_MODEL_DIRECTORY", section.get("ModelDirectory", "/models"))))


def load_processor(settings: Settings) -> Callable[[Image.Image], Image.Image]:
    path = verified_model(settings.model_directory)
    # A direct ONNX session avoids rembg's implicit network-enabled model provisioning path.
    import onnxruntime
    onnxruntime.disable_telemetry_events()
    from rembg.sessions.u2net import U2netSession
    options = onnxruntime.SessionOptions()
    options.intra_op_num_threads = 2
    options.inter_op_num_threads = 1
    native = onnxruntime.InferenceSession(str(path), sess_options=options, providers=["CPUExecutionProvider"])
    session = object.__new__(U2netSession)
    session.inner_session = native
    session.model_name = MODEL_NAME

    def process(image: Image.Image) -> Image.Image:
        mask = session.predict(image.convert("RGB"))[0]
        if mask.size != image.size:
            raise ValueError("Model output dimensions changed.")
        result = image.convert("RGBA")
        # Removal must never make formerly transparent pixels opaque.
        mask = ImageChops.multiply(mask.convert("L"), result.getchannel("A"))
        result.putalpha(mask)
        return result

    return process


def decode(content: bytes, content_type: str) -> Image.Image:
    if not content or len(content) > MAX_INPUT_BYTES:
        raise ValueError("Provide an image up to 20 MiB.")
    with warnings.catch_warnings():
        warnings.simplefilter("error", Image.DecompressionBombWarning)
        with Image.open(BytesIO(content)) as source:
            if source.format != ALLOWED.get(content_type) or getattr(source, "n_frames", 1) != 1:
                raise ValueError("Provide one PNG, JPEG, or WebP image.")
            if min(source.size) < 1 or max(source.size) > MAX_DIMENSION or source.width * source.height > MAX_PIXELS:
                raise ValueError("Image must fit within 8192 pixels per side and 20 megapixels.")
            source.load()
            return ImageOps.exif_transpose(source).convert("RGBA")


def render(content: bytes, content_type: str, processor: Callable) -> bytes:
    with decode(content, content_type) as source:
        result = processor(source)
        try:
            if not isinstance(result, Image.Image) or result.size != source.size or result.mode != "RGBA":
                raise RuntimeError("Background remover returned an invalid image.")
            output = BytesIO()
            # Construct fresh pixel data: never carry EXIF, GPS, comments, or source ICC metadata.
            clean = Image.frombytes("RGBA", result.size, result.tobytes())
            try:
                clean.save(output, format="PNG", compress_level=6)
            finally:
                clean.close()
            data = output.getvalue()
            if len(data) > MAX_OUTPUT_BYTES:
                raise ValueError("Processed image exceeds the output byte limit.")
            return data
        finally:
            if result is not source:
                result.close()


def problem(request: Request, status: int, detail: str, *, headers=None, extensions=None):
    """One safe HTTP failure contract, including framework failures and correlation."""
    request_id = getattr(request.state, "request_id", None) or uuid4().hex
    body = {"type": "about:blank", "title": HTTPStatus(status).phrase, "status": status,
            "detail": detail, "instance": request.url.path, "requestId": request_id,
            "traceId": request_id, **(extensions or {})}
    response_headers = {"Cache-Control": "no-store", "X-Content-Type-Options": "nosniff", **(headers or {})}
    if request.method == "HEAD":
        return Response(status_code=status, media_type="application/problem+json", headers=response_headers)
    return JSONResponse(body, status_code=status, media_type="application/problem+json", headers=response_headers)


def create_app(settings: Settings, loader: Callable = load_processor) -> FastAPI:
    gate = BoundedSemaphore(1)

    @asynccontextmanager
    async def lifespan(app):
        app.state.processor = None
        try:
            app.state.processor = await run_in_threadpool(loader, settings)
        except (ValueError, OSError, RuntimeError, ImportError):
            # Missing/corrupt weights leave the private readiness check unavailable, never a fake success.
            pass
        yield
        app.state.processor = None

    app = FastAPI(lifespan=lifespan, docs_url=None, redoc_url=None, openapi_url=None)

    @app.exception_handler(HTTPException)
    async def http_error(request: Request, exception: HTTPException):
        return problem(request, exception.status_code, HTTPStatus(exception.status_code).phrase,
                       headers=exception.headers)

    @app.exception_handler(RequestValidationError)
    async def validation_error(request: Request, exception: RequestValidationError):
        errors = [{"property": ".".join(str(part) for part in item["loc"]),
                   "code": item["type"], "message": "The supplied value is invalid."}
                  for item in exception.errors()]
        return problem(request, 422, "The request is invalid.", extensions={"errors": errors})

    @app.middleware("http")
    async def boundary(request: Request, call_next):
        request.state.request_id = uuid4().hex
        if request.url.path != "/health/live":
            supplied = request.headers.get("x-image-worker-key", "")
            if not supplied or not hmac.compare_digest(supplied.encode(), settings.key.encode()):
                return problem(request, 401, "Worker authentication required.", headers={"WWW-Authenticate": "ImageWorker"})
        try:
            response = await call_next(request)
        except Exception:
            logging.getLogger(__name__).exception("Unhandled image worker request %s", request.state.request_id)
            return problem(request, 500, "The request could not be completed.")
        response.headers["Cache-Control"] = "no-store"
        response.headers["X-Content-Type-Options"] = "nosniff"
        return response

    @app.get("/health/live")
    async def live():
        return {"alive": True}

    @app.get("/health/ready")
    async def ready(request: Request):
        available = app.state.processor is not None
        identity = {"ready": available, "model": MODEL_NAME, "version": os.environ.get("APP_VERSION") or "dev", "commit": os.environ.get("VCS_REF") or None}
        return JSONResponse(identity) if available else problem(request, 503, "Background removal is not ready.", extensions=identity)

    @app.post("/v1/remove-background")
    async def remove_background(request: Request):
        processor = app.state.processor
        if processor is None:
            return problem(request, 503, "Background removal is not ready.")
        content_type = request.headers.get("content-type", "").split(";", 1)[0].lower().strip()
        if content_type not in ALLOWED:
            return problem(request, 415, "Provide PNG, JPEG, or WebP bytes.")
        length = request.headers.get("content-length")
        if length is not None:
            try:
                if not 0 < int(length) <= MAX_INPUT_BYTES:
                    raise ValueError()
            except ValueError:
                return problem(request, 413, "Provide an image up to 20 MiB.")
        if not gate.acquire(blocking=False):
            return problem(request, 429, "Background removal is busy. Retry shortly.", headers={"Retry-After": "2"})
        try:
            data = bytearray()
            async for chunk in request.stream():
                if len(data) + len(chunk) > MAX_INPUT_BYTES:
                    return problem(request, 413, "Provide an image up to 20 MiB.")
                data.extend(chunk)
            output = await run_in_threadpool(render, bytes(data), content_type, processor)
            return Response(output, media_type="image/png", headers={"X-Image-Model": MODEL_NAME})
        except (ValueError, UnidentifiedImageError, Image.DecompressionBombError, Image.DecompressionBombWarning, OSError):
            return problem(request, 422, "The image is invalid or exceeds processing limits.")
        except Exception:
            return problem(request, 503, "Background removal failed. The original image is unchanged.")
        finally:
            gate.release()

    return app


def from_environment():
    return create_app(Settings.environment())
