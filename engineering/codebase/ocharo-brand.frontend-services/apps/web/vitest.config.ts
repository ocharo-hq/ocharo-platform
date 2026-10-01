import vue from '@vitejs/plugin-vue';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  plugins: [vue()],
  define: { __APP_VERSION__: JSON.stringify(process.env.APP_VERSION || "dev") },
  test: { environment: 'happy-dom', include: ['tests/**/*.test.ts'], restoreMocks: true },
});
