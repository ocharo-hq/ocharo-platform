"""Explicit, pinned U-2-Net provisioning; request handling never downloads models."""
from pathlib import Path
import hashlib
import tempfile
import urllib.request

MODEL_NAME = "u2net"
MODEL_URL = "https://github.com/danielgatis/rembg/releases/download/v0.0.0/u2net.onnx"
# Published by rembg v2.0.67's U2netSession.download_models.
MODEL_MD5 = "60024c5c889badc19c04ad937298a77b"
MAX_MODEL_BYTES = 200 * 1024 * 1024


def verified_model(directory: Path) -> Path:
    path = directory / "u2net.onnx"
    if not path.is_file() or not 0 < path.stat().st_size <= MAX_MODEL_BYTES:
        raise ValueError("Provision the pinned U-2-Net model before starting the worker.")
    with path.open("rb") as stream:
        digest = hashlib.file_digest(stream, "md5").hexdigest()
    if digest != MODEL_MD5:
        raise ValueError("The U-2-Net model checksum does not match its pinned release.")
    return path


def provision(directory: Path) -> None:
    directory.mkdir(parents=True, exist_ok=True)
    target = directory / "u2net.onnx"
    if target.exists():
        verified_model(directory)
        return
    handle, name = tempfile.mkstemp(prefix="u2net-", suffix=".download", dir=directory)
    import os
    os.close(handle)
    temporary = Path(name)
    try:
        with urllib.request.urlopen(MODEL_URL, timeout=60) as response, temporary.open("wb") as output:
            total = 0
            while block := response.read(1024 * 1024):
                total += len(block)
                if total > MAX_MODEL_BYTES:
                    raise ValueError("Model exceeds the pinned download limit.")
                output.write(block)
        with temporary.open("rb") as stream:
            if hashlib.file_digest(stream, "md5").hexdigest() != MODEL_MD5:
                raise ValueError("Downloaded model checksum mismatch.")
        if target.exists():
            verified_model(directory)
        else:
            temporary.replace(target)
            target.chmod(0o444)
    finally:
        temporary.unlink(missing_ok=True)


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    args = parser.parse_args()
    provision(args.directory)
    print("Pinned U-2-Net model verified.")
