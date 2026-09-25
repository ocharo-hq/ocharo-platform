import { fileURLToPath, URL } from 'node:url';

import tailwindcss from '@tailwindcss/vite';
import vue from '@vitejs/plugin-vue';
import { defineConfig } from 'vite';
import mkcert from 'vite-plugin-mkcert';

export default defineConfig(({ command }) => ({
  base: '/',
  plugins: [
    vue(),
    tailwindcss(),
    ...(command === 'serve' && process.env.VITE_HTTPS !== 'false' ? [mkcert()] : []),
  ],
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  server: {
    host: 'localhost',
    port: 8266,
    strictPort: true,
    proxy: {
      '/api': { target: 'https://localhost:8264', secure: false },
    },
  },
  build: {
    outDir: '../brand-workspace.backend-services/BrandWorkspace.Api/wwwroot',
    emptyOutDir: true,
  },
}));
