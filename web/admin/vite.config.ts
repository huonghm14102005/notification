import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

const apiTarget = (typeof process !== 'undefined' && process.env?.VITE_API_URL) ? process.env.VITE_API_URL : 'http://localhost:5000';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/v1': apiTarget,
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: './tests/setup.ts',
    globals: true,
    exclude: ['tests/e2e/**', 'node_modules/**'],
  },
});
