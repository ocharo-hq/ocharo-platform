import { readFileSync } from 'node:fs';
import { Agent } from 'node:https';
import tailwindcss from '@tailwindcss/vite';
import vue from '@vitejs/plugin-vue';
import { defineConfig, type Plugin } from 'vite';
import mkcert from 'vite-plugin-mkcert';
import { colorModeScript } from '@wow-two-beta/fe/foundation/primitives';
import { proxyUnavailable } from './dev/ProxyProblem';
import { ThemeStorageKey } from './src/bootstrap/theme';

/** Keeps development proxy overrides on verified HTTPS endpoints. */
function httpsTarget(value: string): string {
  if (new URL(value).protocol !== 'https:') throw new Error('The development API target must use HTTPS.');
  return value;
}

/** Serves and emits `/theme.js`: the SDK's pre-paint colour script, so a dark Brand never flashes light first. */
function themeScript(): Plugin {
  const source = colorModeScript({ storageKey: ThemeStorageKey });
  return {
    name: 'ocharo-brand-theme-script',
    configureServer(server) {
      server.middlewares.use('/theme.js', (_request, response) => {
        response.setHeader('Content-Type', 'text/javascript');
        response.end(source);
      });
    },
    generateBundle() {
      this.emitFile({ type: 'asset', fileName: 'theme.js', source });
    },
  };
}

export default defineConfig(({ command }) => {
  return {
    define: { __APP_VERSION__: JSON.stringify(process.env.APP_VERSION || 'dev') },
    base: '/',
    plugins: [vue(), tailwindcss(), themeScript(), ...(command === 'serve' ? [mkcert()] : [])],
    build: { outDir: 'dist', emptyOutDir: true, target: 'es2022' },
    server: {
      host: 'localhost',
      port: 8288,
      strictPort: true,
      https: {},
      proxy: {
        '/api': {
          target: httpsTarget(process.env.VITE_API_TARGET ?? 'https://localhost:8286'),
          changeOrigin: false,
          secure: true,
          configure: (proxy) => {
            proxy.on('error', proxyUnavailable);
          },
          ...(command === 'serve' && process.env.VITE_API_CA_PATH
            ? { agent: new Agent({ ca: readFileSync(process.env.VITE_API_CA_PATH) }) }
            : {}),
        },
      },
    },
  };
});
