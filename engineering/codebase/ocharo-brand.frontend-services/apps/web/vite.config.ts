import { readFileSync } from 'node:fs';
import { Agent } from 'node:https';
import tailwindcss from '@tailwindcss/vite';
import vue from '@vitejs/plugin-vue';
import { defineConfig } from 'vite';
import mkcert from 'vite-plugin-mkcert';

/** Keeps development proxy overrides on verified HTTPS endpoints. */
function httpsTarget(value: string): string {
  if (new URL(value).protocol !== 'https:') throw new Error('The development API target must use HTTPS.');
  return value;
}

export default defineConfig(({ command }) => {
  return {
    define: { __APP_VERSION__: JSON.stringify(process.env.APP_VERSION || "dev") },
    base: '/',
    plugins: [vue(), tailwindcss(), ...(command === 'serve' ? [mkcert()] : [])],
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
          ...(command === 'serve' && process.env.VITE_API_CA_PATH
            ? { agent: new Agent({ ca: readFileSync(process.env.VITE_API_CA_PATH) }) }
            : {}),
        },
      },
    },
  };
});
