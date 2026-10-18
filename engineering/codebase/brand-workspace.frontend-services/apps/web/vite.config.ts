import { proxyUnavailable } from './dev/ProxyProblem';
import { readFileSync } from 'node:fs';
import { Agent } from 'node:https';
import { fileURLToPath, URL } from 'node:url';

import tailwindcss from '@tailwindcss/vite';
import vue from '@vitejs/plugin-vue';
import { defineConfig, type Plugin, type ProxyOptions } from 'vite';
import { colorModeScript } from '@wow-two-beta/fe/foundation/primitives';

import { ThemeStorageKey } from './src/bootstrap/theme';
import mkcert from 'vite-plugin-mkcert';

/** Keeps development proxy overrides on verified HTTPS endpoints. */
function httpsTarget(value: string): string {
  if (new URL(value).protocol !== 'https:') throw new Error('The development API target must use HTTPS.');
  return value;
}

/** Serves and emits `/theme.js`: the SDK's pre-paint colour script, so a dark Desk never flashes light first. */
function themeScript(): Plugin {
  const source = colorModeScript({ storageKey: ThemeStorageKey });
  return {
    name: 'ocharo-desk-theme-script',
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

/** Shares verified HTTPS proxying for the app API and stateless MCP transport. */
function deskProxy(command: 'build' | 'serve'): ProxyOptions {
  return {
    target: httpsTarget(process.env.VITE_API_TARGET ?? 'https://localhost:8264'),
    secure: true,
    configure: proxy => { proxy.on('error', proxyUnavailable); },
    ...(command === 'serve' && process.env.VITE_API_CA_PATH
      ? { agent: new Agent({ ca: readFileSync(process.env.VITE_API_CA_PATH) }) } : {}),
  };
}

export default defineConfig(({ command }) => ({
  base: '/',
  define: { __APP_VERSION__: JSON.stringify(process.env.APP_VERSION || "dev") },
  plugins: [
    vue(),
    tailwindcss(),
    themeScript(),
    ...(command === "serve" ? [mkcert()] : []),
  ],
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  server: {
    host: 'localhost',
    port: Number(process.env.VITE_PORT ?? 8378),
    strictPort: true,
    proxy: {
      '/api': deskProxy(command),
      '/mcp': deskProxy(command),
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
}));
