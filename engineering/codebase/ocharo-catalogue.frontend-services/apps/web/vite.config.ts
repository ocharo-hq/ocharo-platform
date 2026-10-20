import tailwindcss from '@tailwindcss/vite';
import vue from '@vitejs/plugin-vue';
import { defineConfig, type Plugin } from 'vite';
import mkcert from 'vite-plugin-mkcert';
import { colorModeScript } from '@wow-two-beta/fe/foundation/primitives';
import { developmentApiProxy } from './scripts/development-api';
import { ThemeStorageKey } from './src/bootstrap/theme';

/** Serves and emits `/theme.js`: the SDK's pre-paint colour script, so a dark Catalogue never flashes light first. */
function themeScript(): Plugin {
  const source = colorModeScript({ storageKey: ThemeStorageKey });
  return {
    name: 'ocharo-catalogue-theme-script',
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
