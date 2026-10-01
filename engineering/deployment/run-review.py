#!/usr/bin/env python3
"""Run the existing local review; keep all newly started processes attached and owned."""

from __future__ import annotations

import fcntl
import json
import os
from pathlib import Path
import re
import shutil
import signal
import socket
import ssl
import subprocess
import sys
import time
import urllib.error
import urllib.request


ROOT = Path(__file__).resolve().parents[2]
VERIFICATION = ROOT / ".local/verification"
BACKEND = ROOT / "engineering/codebase/brand-workspace.backend-services"
PROJECT = BACKEND / "BrandWorkspace.Api/BrandWorkspace.Api.csproj"
ORIGIN = "https://localhost:8264"


class ReviewError(Exception):
    pass


class StopRequested(Exception):
    pass


def read_values(path: Path) -> dict[str, str]:
    """Read literal KEY=value data. Never evaluate shell syntax or interpolate values."""
    values = {}
    for number, line in enumerate(path.read_text().splitlines(), 1):
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        key, separator, value = line.partition("=")
        key, value = key.strip(), value.strip()
        if not separator or not re.fullmatch(r"[A-Z_][A-Z0-9_]*", key) or key in values:
            raise ReviewError(f"Invalid or repeated configuration key on line {number}; values were not logged.")
        if value[:1] in ("'", '"'):
            if len(value) < 2 or value[-1] != value[0]:
                raise ReviewError(f"Unclosed configuration quote on line {number}.")
            value = value[1:-1]
        if "\x00" in value:
            raise ReviewError(f"Invalid configuration value on line {number}.")
        if key not in {"POSTGRES_PASSWORD", "BRAND_WORKSPACE_HTTPS_CERTIFICATE", "BRAND_WORKSPACE_HTTPS_PASSWORD"}:
            raise ReviewError(f"Unsupported configuration key on line {number}.")
        values[key] = value
    if not values.get("POSTGRES_PASSWORD"):
        raise ReviewError("The existing compose.env must contain POSTGRES_PASSWORD.")
    return values


def healthy() -> bool:
    """Require the expected service and trusted HTTPS, without ambient HTTP proxies."""
    opener = urllib.request.build_opener(
        urllib.request.ProxyHandler({}),
        urllib.request.HTTPSHandler(context=ssl.create_default_context()),
    )
    try:
        with opener.open(ORIGIN + "/api/system/status", timeout=2) as response:
            status = json.loads(response.read(8192))
            if not isinstance(status, dict) or status.get("service") != "brand-workspace" or status.get("persistence") != "PostgreSQL":
                return False
        with opener.open(ORIGIN + "/health/ready", timeout=2) as response:
            return json.loads(response.read(8192)) == {"status": "ready"}
    except urllib.error.URLError as error:
        if isinstance(error.reason, ssl.SSLCertVerificationError):
            raise ReviewError("Local HTTPS certificate verification failed; no certificate trust was changed.") from None
        return False
    except (OSError, ValueError):
        return False


def port_in_use(port: int) -> bool:
    try:
        with socket.create_connection(("localhost", port), timeout=0.3):
            return True
    except OSError:
        return False


class Children:
    """Own process groups so Ctrl+C also cleans up build and CLI descendants."""

    def __init__(self):
        self.active = []
        self.started_postgres = None
        self.lock = None

    def start(self, command, *, env, cwd=ROOT, capture=False, label="Command"):
        try:
            child = subprocess.Popen(
                command, cwd=cwd, env=env, start_new_session=True,
                stdout=subprocess.PIPE if capture else None,
                stderr=subprocess.STDOUT if capture else None, text=True,
            )
        except OSError:
            raise ReviewError(f"{label} could not start; check the required local tools.") from None
        self.active.append(child)
        return child

    def run(self, command, *, env, cwd=ROOT, capture=False, label="Command", timeout=600, check=True):
        child = self.start(command, env=env, cwd=cwd, capture=capture, label=label)
        try:
            output, _ = child.communicate(timeout=timeout)
        except subprocess.TimeoutExpired:
            raise ReviewError(f"{label} timed out.") from None
        self.active.remove(child)
        if check and child.returncode:
            raise ReviewError(f"{label} failed with exit code {child.returncode}.")
        return child.returncode, output or ""

    def stop_all(self):
        for child in reversed(self.active):
            for sig, seconds in ((signal.SIGINT, 35), (signal.SIGTERM, 10), (signal.SIGKILL, 5)):
                try:
                    os.killpg(child.pid, sig)
                    deadline = time.monotonic() + seconds
                    while time.monotonic() < deadline:
                        child.poll()
                        os.killpg(child.pid, 0)
                        time.sleep(0.1)
                except ProcessLookupError:
                    break
            child.poll()
        self.active.clear()


def container_state(children: Children, env, container_id):
    code, output = children.run(
        ["docker", "inspect", "--format", "{{json .State}}", container_id],
        env=env, capture=True, check=False, label="Owned PostgreSQL instance check", timeout=15,
    )
    if code:
        return None
    try:
        state = json.loads(output)
        return state if isinstance(state, dict) else None
    except ValueError:
        return None


def capture_postgres_instance(children: Children):
    owned = children.started_postgres
    if owned is None or owned["instance"] is not None:
        return
    _, output = children.run(owned["compose"] + ["ps", "--status", "running", "-q", "postgres"],
                             env=owned["env"], capture=True, label="Started PostgreSQL identity", timeout=15)
    container_id = output.strip()
    if not re.fullmatch(r"[0-9a-f]{64}", container_id):
        return
    state = container_state(children, owned["env"], container_id)
    if state and state.get("Running") and state.get("StartedAt"):
        owned["instance"] = (container_id, state["StartedAt"])


def stop_owned_postgres(children: Children):
    owned = children.started_postgres
    if owned is None or owned["instance"] is None:
        return
    container_id, started_at = owned["instance"]
    state = container_state(children, owned["env"], container_id)
    if state and state.get("Running") and state.get("StartedAt") == started_at:
        # Target the original container and start instance, never the project's current service name.
        children.run(["docker", "stop", "--timeout", "30", container_id], env=owned["env"],
                     label="Owned PostgreSQL shutdown", timeout=45)


def node_environment(children: Children) -> dict[str, str]:
    version = (ROOT / ".nvmrc").read_text().strip().removeprefix("v")
    if not re.fullmatch(r"24\.\d+\.\d+", version):
        raise ReviewError("The review launcher expects an exact Node 24 version in .nvmrc.")
    candidate = Path.home() / ".nvm/versions/node" / ("v" + version) / "bin/node"
    node = str(candidate) if candidate.is_file() else shutil.which("node")
    if not node:
        raise ReviewError("Install or activate the Node version declared in .nvmrc first.")
    env = os.environ.copy()
    env["PATH"] = str(Path(node).parent) + os.pathsep + env.get("PATH", "")
    _, actual = children.run([node, "--version"], env=env, capture=True, label="Node version check", timeout=10)
    if actual.strip() != "v" + version:
        raise ReviewError(f"Activate Node {version} from .nvmrc before starting review.")
    if not shutil.which("pnpm", path=env["PATH"]):
        raise ReviewError("The declared Node environment needs pnpm 10; the launcher does not install tools.")
    _, pnpm_version = children.run(["pnpm", "--version"], env=env, capture=True, label="pnpm version check", timeout=10)
    if not pnpm_version.strip().startswith("10."):
        raise ReviewError("Activate pnpm 10 before starting review.")
    return env


def run_review(children: Children) -> int:
    if healthy():
        print(f"Review is already running: {ORIGIN}", flush=True)
        return 0
    required = (VERIFICATION / "compose.env", VERIFICATION / "operator-password.txt")
    if any(not path.is_file() or path.stat().st_size == 0 for path in required):
        raise ReviewError("Existing .local/verification/compose.env and operator-password.txt are required; no credentials were created.")
    # The lock prevents two launcher instances from taking ownership of the same local services.
    children.lock = (VERIFICATION / "review.lock").open("a")
    lock = children.lock
    try:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except BlockingIOError:
        raise ReviewError("Another review launcher is starting or owns this runtime; use its terminal.") from None
    if healthy():
        print(f"Review is already running: {ORIGIN}", flush=True)
        return 0
    if any(port_in_use(port) for port in (8264, 8265)):
        raise ReviewError("Port 8264 or 8265 is occupied by an unready service; stop its owning terminal first.")
    values = read_values(VERIFICATION / "compose.env")
    env = node_environment(children)
    if not shutil.which("dotnet", path=env["PATH"]) or not shutil.which("docker", path=env["PATH"]):
        raise ReviewError("The existing .NET 10 SDK and Docker Compose are required.")
    docker_host = env.get("DOCKER_HOST") if not env.get("DOCKER_CONTEXT") else None
    if not docker_host:
        _, docker_host = children.run(["docker", "context", "inspect", "--format", "{{.Endpoints.docker.Host}}"],
                                       env=env, capture=True, label="Local Docker context", timeout=15)
    if not docker_host.strip().startswith("unix://"):
        raise ReviewError("Select the local Docker Desktop Unix-socket context before starting review.")
    children.run(["dotnet", "dev-certs", "https", "--check"], env=env, label="Existing HTTPS certificate check", timeout=30)
    print("Building the current API and locked frontend...", flush=True)
    children.run(
        ["dotnet", "build", str(PROJECT), "-p:RestoreLockedMode=true", "--disable-build-servers", "-m:1", "--verbosity", "minimal"],
        env=env, cwd=BACKEND, label="Normal API and frontend build",
    )
    # Explicit process environment wins over Compose interpolation; values remain literal data.
    compose_env = env | values
    compose_env.pop("DOCKER_CONTEXT", None)
    compose_env["DOCKER_HOST"] = docker_host.strip()
    compose = ["docker", "compose", "--project-name", "brand-workspace", "--env-file", str(VERIFICATION / "compose.env"),
               "-f", str(ROOT / "engineering/deployment/docker-compose.yml")]
    _, running = children.run(compose + ["ps", "--status", "running", "-q", "postgres"],
                              env=compose_env, capture=True, label="Local PostgreSQL status", timeout=30)
    postgres = None
    if not running.strip():
        print("Starting the existing PostgreSQL service in the foreground...", flush=True)
        postgres = children.start(compose + ["up", "--no-deps", "--no-recreate", "--pull", "never", "postgres"],
                                  env=compose_env, label="Local PostgreSQL")
        children.started_postgres = {"compose": compose, "env": compose_env, "instance": None, "process": postgres}
    else:
        print("Reusing PostgreSQL; this launcher will leave it running.", flush=True)
    deadline = time.monotonic() + 60
    while True:
        if postgres is not None and postgres.poll() is not None:
            raise ReviewError("The owned PostgreSQL process exited during startup.")
        capture_postgres_instance(children)
        code, _ = children.run(compose + ["exec", "-T", "postgres", "pg_isready", "-U", "brand_workspace", "-d", "brand_workspace"],
                               env=compose_env, capture=True, label="PostgreSQL readiness", timeout=10, check=False)
        if code == 0:
            if postgres is not None and children.started_postgres["instance"] is None:
                raise ReviewError("Could not establish ownership of the started PostgreSQL instance.")
            break
        if time.monotonic() >= deadline:
            raise ReviewError("PostgreSQL did not become ready within 60 seconds.")
        time.sleep(1)
    runtime_env = env.copy()
    for key in ("Kestrel__Certificates__Default__Path", "Kestrel__Certificates__Default__Password"):
        runtime_env.pop(key, None)
    password = '"' + values["POSTGRES_PASSWORD"].replace('"', '""') + '"'
    runtime_env.update({
        "ConnectionStrings__Database": "Host=127.0.0.1;Port=8270;Database=brand_workspace;Username=brand_workspace;Password=" + password,
        "DOTNET_ENVIRONMENT": "Development", "ASPNETCORE_ENVIRONMENT": "Development",
        "ASPNETCORE_URLS": ORIGIN + ";http://localhost:8265", "AllowedHosts": "localhost;127.0.0.1",
        "DataProtection__KeysPath": str(VERIFICATION / "host-keys"),
        "DataProtection__ApplicationName": "brand-workspace:Development",
    })
    command = ["dotnet", "run", "--project", str(PROJECT), "--no-build", "--no-launch-profile"]
    children.run(command + ["--", "--migrate"], env=runtime_env, cwd=BACKEND, label="Explicit migrations", timeout=120)
    api = children.start(command, env=runtime_env, cwd=BACKEND, label="Review API")
    deadline = time.monotonic() + 60
    while not healthy():
        if api.poll() is not None:
            raise ReviewError("The API exited before verified HTTPS readiness.")
        if time.monotonic() >= deadline:
            raise ReviewError("The API did not become ready within 60 seconds; check its logs and existing certificate trust.")
        time.sleep(0.5)
    print(f"\nReview: {ORIGIN}\nOperator: smoke@example.test\nPassword: .local/verification/operator-password.txt\nKeep this terminal open. Ctrl+C stops only services started here.\n", flush=True)
    while api.poll() is None:
        if postgres is not None and postgres.poll() is not None:
            raise ReviewError("The owned PostgreSQL process stopped; ending this review runtime.")
        time.sleep(0.5)
    return api.returncode


def main() -> int:
    children = Children()
    result = 1

    def stop(_signum, _frame):
        raise StopRequested()

    for sig in (signal.SIGINT, signal.SIGTERM, signal.SIGHUP):
        signal.signal(sig, stop)
    try:
        result = run_review(children)
    except StopRequested:
        print("\nStopping owned review processes; preserving database, keys, and credentials.", flush=True)
        result = 0
    except (ReviewError, OSError) as error:
        print(f"Review could not start: {error}", file=sys.stderr)
    finally:
        for sig in (signal.SIGINT, signal.SIGTERM, signal.SIGHUP):
            signal.signal(sig, signal.SIG_IGN)
        # Compose handles SIGINT/TERM by stopping its current service. Exclude that client
        # from generic signaling: only the guarded exact-instance Docker stop owns shutdown.
        postgres_client = children.started_postgres.get("process") if children.started_postgres else None
        if postgres_client in children.active:
            children.active.remove(postgres_client)
        children.stop_all()
        if children.started_postgres is not None:
            try:
                capture_postgres_instance(children)
                stop_owned_postgres(children)
            except ReviewError as error:
                print(str(error), file=sys.stderr)
                result = 1
            finally:
                children.stop_all()
                if postgres_client is not None:
                    try:
                        # A stopped owned container normally makes its attached client exit itself.
                        postgres_client.wait(timeout=3)
                    except subprocess.TimeoutExpired:
                        # Never invoke Compose's service-stop handler for a replacement instance.
                        try:
                            os.killpg(postgres_client.pid, signal.SIGKILL)
                        except ProcessLookupError:
                            pass
                        postgres_client.wait(timeout=5)
        if children.lock is not None:
            children.lock.close()
    return result


if __name__ == "__main__":
    raise SystemExit(main())
