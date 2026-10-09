import { proxyUnavailable } from './dev/ProxyProblem';
import { readFileSync } from 'node:fs';
import { Agent } from 'node:https';
import { fileURLToPath, URL } from 'node:url';

import tailwindcss from '@tailwindcss/vite';
import vue from '@vitejs/plugin-vue';
import { defineConfig, type ProxyOptions } from 'vite';
import mkcert from 'vite-plugin-mkcert';

/** Keeps development proxy overrides on verified HTTPS endpoints. */
function httpsTarget(value: string): string {
  if (new URL(value).protocol !== 'https:') throw new Error('The development API target must use HTTPS.');
  return value;
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
    ...(command === "serve" ? [mkcert()] : []),
  ],
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  server: {
    host: 'localhost',
    port: Number(process.env.VITE_PORT ?? 8266),
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
