import { readFileSync } from 'node:fs';
import { Agent } from 'node:https';
import tailwindcss from '@tailwindcss/vite';
import vue from '@vitejs/plugin-vue';
import { defineConfig } from 'vite';
import mkcert from 'vite-plugin-mkcert';

export default defineConfig(({ command }) => {
  const apiTarget = new URL(process.env.VITE_API_TARGET ?? 'https://localhost:8440');
  if (apiTarget.protocol !== 'https:') throw new Error('VITE_API_TARGET must use HTTPS.');
  const caPath = process.env.VITE_API_CA_PATH;

  return {
    base: '/',
    plugins: [vue(), tailwindcss(), ...(command === 'serve' ? [mkcert()] : [])],
    define: { __APP_VERSION__: JSON.stringify(process.env.APP_VERSION ?? 'dev') },
    server: {
      host: 'localhost',
      port: 8442,
      strictPort: true,
      https: {},
      proxy: {
        '/api': {
          target: apiTarget.origin,
          changeOrigin: false,
          secure: true,
          ...(caPath ? { agent: new Agent({ ca: readFileSync(caPath) }) } : {}),
        },
      },
    },
  };
});
