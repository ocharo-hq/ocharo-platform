"""Local-review regression tests; all process, network, and signal boundaries are mocked."""

import contextlib
import importlib.util
import io
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch

SOURCE = Path(__file__).resolve().parents[1] / "run-review.py"
spec = importlib.util.spec_from_file_location('review', SOURCE)
review = importlib.util.module_from_spec(spec)
spec.loader.exec_module(review)


class FakeProcess:
    pid = 123456

    def __init__(self, code=None):
        self.returncode = code

    def poll(self):
        return self.returncode

    def wait(self, timeout):
        return self.returncode


class FakeChildren(review.Children):
    def __init__(self, running=True):
        super().__init__()
        self.running = running
        self.commands = []
        self.starts = []
        self.stops = 0

    def run(self, command, **kwargs):
        self.commands.append((command, kwargs))
        if command[:3] == ['docker', 'context', 'inspect']:
            return 0, 'unix:///local/docker.sock'
        if 'ps' in command:
            return 0, 'a' * 64 if self.running or self.starts else ''
        if command[:2] == ['docker', 'inspect']:
            return 0, '{"Running":true,"StartedAt":"first-start"}'
        return 0, ''

    def start(self, command, **kwargs):
        self.starts.append((command, kwargs))
        return FakeProcess(None if 'up' in command else 0)

    def stop_all(self):
        self.stops += 1


class LauncherTests(unittest.TestCase):
    def setUp(self):
        # Fail closed if a future test forgets to mock a runtime side effect.
        for target in (
            'subprocess.Popen',
            'urllib.request.OpenerDirector.open',
            'os.killpg',
            'signal.signal',
        ):
            boundary = patch(target, side_effect=AssertionError('Unexpected live boundary: ' + target))
            boundary.start()
            self.addCleanup(boundary.stop)

    def test_literal_configuration_never_evaluates_shell_syntax(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'compose.env'
            path.write_text('POSTGRES_PASSWORD=\'$(touch /never-execute); "$HOME"\'\n')
            self.assertEqual(review.read_values(path)['POSTGRES_PASSWORD'], '$(touch /never-execute); "$HOME"')

    def test_duplicate_and_ambient_execution_keys_are_rejected(self):
        for text in ('POSTGRES_PASSWORD=a\nPOSTGRES_PASSWORD=b', 'POSTGRES_PASSWORD=a\nPATH=/injected', 'POSTGRES_PASSWORD="unclosed'):
            with self.subTest(text=text), tempfile.TemporaryDirectory() as directory:
                path = Path(directory) / 'compose.env'
                path.write_text(text)
                with self.assertRaises(review.ReviewError):
                    review.read_values(path)

    def test_healthy_fast_path_starts_nothing(self):
        children = FakeChildren()
        with patch.object(review, 'healthy', return_value=True), contextlib.redirect_stdout(io.StringIO()) as output:
            self.assertEqual(review.run_review(children), 0)
        self.assertIn(review.ORIGIN, output.getvalue())
        self.assertEqual(children.commands, [])
        self.assertEqual(children.starts, [])

    def simulate_start(self, running):
        children = FakeChildren(running)
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'compose.env').write_text('POSTGRES_PASSWORD=\'quoted";literal\'\n')
            (root / 'operator-password.txt').write_text('fixture-only')
            with patch.object(review, 'VERIFICATION', root), \
                 patch.object(review, 'healthy', side_effect=[False, False, True]), \
                 patch.object(review, 'port_in_use', return_value=False), \
                 patch.object(review, 'node_environment', return_value={'PATH': '/fixture'}), \
                 patch.object(review.shutil, 'which', return_value='/fixture/tool'), \
                 contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(review.run_review(children), 0)
            self.assertIsNotNone(children.lock)
            self.assertFalse(children.lock.closed, 'The lock must survive until main finishes process cleanup.')
            children.lock.close()
        return children

    def test_reuses_running_postgres_and_preserves_runtime_identity(self):
        children = self.simulate_start(True)
        self.assertIsNone(children.started_postgres)
        self.assertEqual(len(children.starts), 1)
        command, options = children.starts[0]
        self.assertIn('--no-build', command)
        self.assertEqual(options['env']['DataProtection__ApplicationName'], 'brand-workspace:Development')
        self.assertTrue(options['env']['ConnectionStrings__Database'].endswith('Password="quoted"";literal"'))
        self.assertTrue(any('--migrate' in command for command, _ in children.commands))
        self.assertTrue(any('--disable-build-servers' in command for command, _ in children.commands))

    def test_absent_postgres_is_attached_and_owned(self):
        children = self.simulate_start(False)
        self.assertIsNotNone(children.started_postgres)
        command, options = children.starts[0]
        self.assertIn('up', command)
        self.assertNotIn('-d', command)
        self.assertIn('--no-recreate', command)
        self.assertIn('--project-name', command)
        self.assertEqual(options['env']['POSTGRES_PASSWORD'], 'quoted";literal')

    def test_failed_start_cleans_owned_postgres_without_removing_volume(self):
        children = FakeChildren()

        def fail(instance):
            instance.started_postgres = {'compose': ['docker', 'compose'], 'env': {'PATH': '/fixture'}, 'instance': ('a' * 64, 'first-start')}
            raise review.ReviewError('simulated startup failure')

        with patch.object(review, 'Children', return_value=children), patch.object(review, 'run_review', side_effect=fail), \
             patch.object(review.signal, 'signal'), contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(review.main(), 1)
        self.assertEqual(children.commands[-1][0], ['docker', 'stop', '--timeout', '30', 'a' * 64])
        self.assertEqual(children.stops, 2)

    def test_reused_postgres_is_not_stopped(self):
        children = FakeChildren()
        with patch.object(review, 'Children', return_value=children), patch.object(review, 'run_review', return_value=0), \
             patch.object(review.signal, 'signal'):
            self.assertEqual(review.main(), 0)
        self.assertEqual(children.commands, [])

    def test_cleanup_signals_descendants_even_when_leader_exited(self):
        children = review.Children()
        children.active.append(FakeProcess(0))
        with patch.object(review.os, 'killpg', side_effect=[None, ProcessLookupError()]) as kill:
            children.stop_all()
        self.assertEqual(kill.call_args_list[0].args, (123456, review.signal.SIGINT))
        self.assertEqual(children.active, [])

    def test_healthy_requires_real_readiness_even_when_identity_matches(self):
        opener = Mock()
        opener.open.side_effect = [
            io.BytesIO(b'{"service":"brand-workspace","persistence":"PostgreSQL"}'),
            io.BytesIO(b'{"status":"unavailable"}'),
        ]
        with patch.object(review.urllib.request, 'build_opener', return_value=opener):
            self.assertFalse(review.healthy())
        self.assertTrue(opener.open.call_args_list[-1].args[0].endswith('/health/ready'))

    def test_cleanup_preserves_restarted_same_container(self):
        children = FakeChildren()
        children.started_postgres = {'env': {}, 'instance': ('a' * 64, 'old-start')}
        review.stop_owned_postgres(children)
        self.assertFalse(any('stop' in command for command, _ in children.commands))

    def test_cleanup_preserves_replacement_container(self):
        children = FakeChildren()
        children.started_postgres = {'env': {}, 'instance': ('a' * 64, 'first-start')}
        with patch.object(review, 'container_state', return_value=None):
            review.stop_owned_postgres(children)
        self.assertEqual(children.commands, [])


    def test_replacement_teardown_never_sends_compose_stop_signals(self):
        children = FakeChildren()

        class ComposeClient(FakeProcess):
            def wait(self, timeout):
                if timeout == 3:
                    raise review.subprocess.TimeoutExpired('owned-compose', timeout)
                return -9

        postgres = ComposeClient()

        def interrupted(instance):
            instance.active.append(postgres)
            instance.started_postgres = {
                'env': {}, 'instance': ('a' * 64, 'old-start'), 'process': postgres,
            }
            raise review.StopRequested()

        with patch.object(review, 'Children', return_value=children), \
             patch.object(review, 'run_review', side_effect=interrupted), \
             patch.object(review.signal, 'signal'), \
             patch.object(review.os, 'killpg') as kill, \
             contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(review.main(), 0)
        self.assertEqual(kill.call_args_list[0].args, (postgres.pid, review.signal.SIGKILL))
        self.assertEqual(kill.call_count, 1)
        self.assertNotIn(postgres, children.active)
        self.assertFalse(any('stop' in command for command, _ in children.commands))


if __name__ == '__main__':
    unittest.main()
