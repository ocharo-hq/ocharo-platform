from concurrent.futures import ThreadPoolExecutor
from io import BytesIO
from pathlib import Path
from threading import Event

import pytest
from fastapi.testclient import TestClient
from PIL import Image, PngImagePlugin

import worker
from worker import Settings, create_app

KEY = 'test-only-private-worker-key-0000000000000000'
HEADERS = {'X-Image-Worker-Key': KEY, 'Content-Type': 'image/png'}


def picture(mode='RGBA', size=(24, 32), kind='PNG', **kwargs):
    image = Image.new(mode, size, (35, 65, 95, 128) if mode == 'RGBA' else (35, 65, 95))
    output = BytesIO()
    image.save(output, format=kind, **kwargs)
    return output.getvalue()


def client(processor=lambda image: image.copy()):
    return TestClient(create_app(Settings(KEY, Path('/unused')), lambda _: processor))


def test_private_operations_require_key_and_no_store():
    with client() as app:
        assert app.get('/health/live').json() == {'alive': True}
        for path in ['/health/ready', '/v1/remove-background']:
            response = app.request('GET' if path.endswith('ready') else 'POST', path)
            assert response.status_code == 401
            assert response.headers['cache-control'] == 'no-store'
        assert app.get('/docs', headers=HEADERS).status_code == 404


def test_capability_is_truthful_when_model_is_missing():
    def missing(_):
        raise ValueError('missing weights')
    with TestClient(create_app(Settings(KEY, Path('/unused')), missing)) as app:
        response = app.get('/health/ready', headers=HEADERS)
        assert response.status_code == 503 and not response.json()['ready']
        assert app.post('/v1/remove-background', headers=HEADERS, content=picture()).status_code == 503


def test_png_preserves_size_alpha_and_discards_metadata():
    metadata = PngImagePlugin.PngInfo()
    metadata.add_text('private-location', 'do not retain')
    with client() as app:
        status = app.get('/health/ready', headers=HEADERS).json()
        assert status['ready'] is True and status['model'] == 'u2net'
        assert isinstance(status['version'], str)
        response = app.post('/v1/remove-background', headers=HEADERS, content=picture(pnginfo=metadata))
        assert response.status_code == 200
        assert response.headers['content-type'] == 'image/png'
        assert response.headers['x-image-model'] == 'u2net'
        assert response.headers['cache-control'] == 'no-store'
        with Image.open(BytesIO(response.content)) as image:
            assert image.size == (24, 32) and image.mode == 'RGBA'
            assert image.getchannel('A').getextrema() == (128, 128)
            assert not image.info


def test_jpeg_exif_orientation_is_applied():
    exif = Image.Exif()
    exif[274] = 6
    with client() as app:
        response = app.post('/v1/remove-background', headers={**HEADERS, 'Content-Type': 'image/jpeg'}, content=picture('RGB', (30, 15), 'JPEG', exif=exif))
        assert response.status_code == 200
        with Image.open(BytesIO(response.content)) as result:
            assert result.size == (15, 30) and not result.getexif()


@pytest.mark.parametrize('content_type,body,status', [
    ('text/plain', b'https://example.test/photo.png', 415),
    ('image/svg+xml', b'<svg/>', 415),
    ('image/png', b'not image bytes', 422),
    ('image/jpeg', picture(), 422),
])
def test_untrusted_input_is_rejected(content_type, body, status):
    with client() as app:
        response = app.post('/v1/remove-background', headers={**HEADERS, 'Content-Type': content_type}, content=body)
        assert response.status_code == status


def test_truncated_raster_is_rejected():
    with client() as app:
        response = app.post('/v1/remove-background', headers=HEADERS, content=picture()[:40])
        assert response.status_code == 422


def test_animation_is_rejected():
    frames = [Image.new('RGBA', (12, 12), color) for color in ['red', 'blue']]
    out = BytesIO()
    frames[0].save(out, format='PNG', save_all=True, append_images=frames[1:], duration=50)
    with client() as app:
        assert app.post('/v1/remove-background', headers=HEADERS, content=out.getvalue()).status_code == 422


def test_byte_limits_apply_to_declared_and_streamed_payloads(monkeypatch):
    monkeypatch.setattr(worker, 'MAX_INPUT_BYTES', 64)
    with client() as app:
        assert app.post('/v1/remove-background', headers=HEADERS, content=b'x' * 65).status_code == 413
        assert app.post('/v1/remove-background', headers={**HEADERS, 'Content-Length': '1'}, content=b'x' * 65).status_code == 413


def test_pixel_limits_precede_processing(monkeypatch):
    monkeypatch.setattr(worker, 'MAX_PIXELS', 100)
    called = []
    with client(lambda image: called.append(True)) as app:
        assert app.post('/v1/remove-background', headers=HEADERS, content=picture()).status_code == 422
        assert called == []


def test_wrong_model_output_never_becomes_success():
    with client(lambda _: Image.new('RGBA', (1, 1))) as app:
        assert app.post('/v1/remove-background', headers=HEADERS, content=picture()).status_code == 503


def test_concurrent_jobs_are_bounded_without_queueing_uploads():
    entered, release = Event(), Event()
    def slow(image):
        entered.set()
        assert release.wait(5)
        return image.copy()
    with client(slow) as app, ThreadPoolExecutor() as pool:
        first = pool.submit(app.post, '/v1/remove-background', headers=HEADERS, content=picture())
        assert entered.wait(3)
        try:
            response = app.post('/v1/remove-background', headers=HEADERS, content=picture())
            assert response.status_code == 429 and response.headers['retry-after'] == '2'
        finally:
            release.set()
        assert first.result().status_code == 200


def test_failed_request_releases_capacity():
    with client() as app:
        assert app.post('/v1/remove-background', headers=HEADERS, content=b'bad').status_code == 422
        assert app.post('/v1/remove-background', headers=HEADERS, content=picture()).status_code == 200


def test_mounted_settings_preserve_environment_override(tmp_path, monkeypatch):
    import json
    config = tmp_path / 'appsettings.Local.json'
    config.write_text(json.dumps({'ImageWorker': {'Key': KEY, 'ModelDirectory': '/configured-models'}}))
    monkeypatch.setenv('IMAGE_WORKER_SETTINGS_FILE', str(config))
    monkeypatch.delenv('IMAGE_WORKER_KEY_FILE', raising=False)
    monkeypatch.delenv('IMAGE_WORKER_KEY', raising=False)
    monkeypatch.delenv('IMAGE_WORKER_MODEL_DIRECTORY', raising=False)
    settings = Settings.environment()
    assert settings.key == KEY and settings.model_directory == Path('/configured-models')
    monkeypatch.setenv('IMAGE_WORKER_KEY', 'test-only-environment-key-0000000000000000')
    assert Settings.environment().key == 'test-only-environment-key-0000000000000000'


def test_private_readiness_reports_injected_build_identity(monkeypatch):
    monkeypatch.setenv('APP_VERSION', '0.1.7-test.2')
    monkeypatch.setenv('VCS_REF', '0123456789abcdef0123456789abcdef01234567')
    with client() as app:
        status = app.get('/health/ready', headers=HEADERS).json()
        assert status['version'] == '0.1.7-test.2'
        assert status['commit'] == '0123456789abcdef0123456789abcdef01234567'


@pytest.mark.parametrize('method,path,headers,body,status', [
    ('GET', '/health/ready', {}, None, 401),
    ('GET', '/unknown', HEADERS, None, 404),
    ('PUT', '/v1/remove-background', HEADERS, None, 405),
    ('POST', '/v1/remove-background', {**HEADERS, 'Content-Type': 'text/plain'}, b'invalid', 415),
    ('POST', '/v1/remove-background', HEADERS, b'invalid', 422),
])
def test_every_failure_uses_problem_details(method, path, headers, body, status):
    with client() as app:
        response = app.request(method, path, headers={**headers, 'Accept': 'text/html'}, content=body)
        assert response.status_code == status
        assert response.headers['content-type'].startswith('application/problem+json')
        problem = response.json()
        assert problem['status'] == status and problem['type'] == 'about:blank'
        assert problem['title'] and problem['detail'] and problem['instance'] == path
        assert problem['traceId'] and problem['requestId']
        assert response.headers['cache-control'] == 'no-store'
        if status == 401:
            assert response.headers['www-authenticate'] == 'ImageWorker'
        if status == 405:
            assert response.headers['allow'] == 'POST'


def test_head_error_never_emits_a_body():
    with client() as app:
        response = app.head('/unknown', headers=HEADERS)
        assert response.status_code == 404 and response.content == b''
        assert response.headers['content-type'].startswith('application/problem+json')


def test_unexpected_exception_is_safe_problem_details():
    application = create_app(Settings(KEY, Path('/unused')), lambda _: lambda image: image.copy())

    @application.get('/explode')
    async def explode():
        raise RuntimeError('private-path-and-secret')

    with TestClient(application) as app:
        response = app.get('/explode', headers=HEADERS)
        assert response.status_code == 500
        assert response.headers['content-type'].startswith('application/problem+json')
        assert response.json()['status'] == 500
        assert 'private-path-and-secret' not in response.text


def test_readiness_failure_is_problem_details_without_losing_capability():
    def missing(_):
        raise ValueError('private-model-path')

    with TestClient(create_app(Settings(KEY, Path('/unused')), missing)) as app:
        response = app.get('/health/ready', headers=HEADERS)
        assert response.status_code == 503
        assert response.headers['content-type'].startswith('application/problem+json')
        assert response.json()['status'] == 503
        assert response.json()['ready'] is False
        assert 'private-model-path' not in response.text


def test_disconnected_upload_propagates_and_releases_capacity():
    import asyncio
    from starlette.requests import ClientDisconnect, Request

    async def run():
        application = create_app(Settings(KEY, Path('/unused')), lambda _: lambda image: image.copy())
        application.state.processor = lambda image: image.copy()
        endpoint = next(route.endpoint for route in application.routes if getattr(route, 'path', None) == '/v1/remove-background')
        scope = {'type': 'http', 'method': 'POST', 'path': '/v1/remove-background', 'headers':
                 [(b'content-type', b'image/png'), (b'x-image-worker-key', KEY.encode())], 'app': application}
        chunks = iter([{'type': 'http.request', 'body': b'partial', 'more_body': True}, {'type': 'http.disconnect'}])

        async def receive_disconnected():
            return next(chunks)

        with pytest.raises(ClientDisconnect):
            await endpoint(Request(scope, receive_disconnected))

        async def receive_image():
            return {'type': 'http.request', 'body': picture(), 'more_body': False}

        recovered = await endpoint(Request(scope, receive_image))
        assert recovered.status_code == 200 and recovered.media_type == 'image/png'
        boundary = application.user_middleware[0].kwargs['dispatch']

        async def disconnected_next(_):
            raise ClientDisconnect()

        with pytest.raises(ClientDisconnect):
            await boundary(Request(scope), disconnected_next)

    asyncio.run(run())
