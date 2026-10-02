import tailwindcss from '@tailwindcss/vite';
import vue from '@vitejs/plugin-vue';
import { defineConfig } from 'vite';
import mkcert from 'vite-plugin-mkcert';
import { developmentApiProxy } from './scripts/development-api';

export default defineConfig(({ command }) => {
  return {
    define: { __APP_VERSION__: JSON.stringify(process.env.APP_VERSION || 'dev') },
    base: '/',
    plugins: [vue(), tailwindcss(), ...(command === 'serve' ? [mkcert()] : [])],
    build: { outDir: 'dist', emptyOutDir: true, target: 'es2022' },
    server: {
      host: 'localhost',
      port: 8282,
      strictPort: true,
      https: {},
      proxy: {
        '/api': developmentApiProxy({
          target: process.env.VITE_API_TARGET ?? 'https://localhost:8278',
          development: command === 'serve',
          certificatePath: process.env.VITE_API_CA_PATH,
        }),
      },
    },
  };
});
