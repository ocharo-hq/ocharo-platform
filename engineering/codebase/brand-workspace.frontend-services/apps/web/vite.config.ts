import { readFileSync } from 'node:fs';
import { Agent } from 'node:https';
import { fileURLToPath, URL } from 'node:url';

import tailwindcss from '@tailwindcss/vite';
import vue from '@vitejs/plugin-vue';
import { defineConfig } from 'vite';
import mkcert from 'vite-plugin-mkcert';

export default defineConfig(({ command }) => ({
  base: '/',
  define: { __APP_VERSION__: JSON.stringify(process.env.APP_VERSION || "dev") },
  plugins: [
    vue(),
    tailwindcss(),
    ...(command === 'serve' && process.env.VITE_HTTPS !== 'false' ? [mkcert()] : []),
  ],
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  server: {
    host: 'localhost',
    port: Number(process.env.VITE_PORT ?? 8266),
    strictPort: true,
    proxy: {
      '/api': { target: process.env.VITE_API_TARGET ?? 'https://localhost:8264', secure: true, ...(process.env.VITE_API_CA_PATH ? { agent: new Agent({ ca: readFileSync(process.env.VITE_API_CA_PATH) }) } : {}) },
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
}));
