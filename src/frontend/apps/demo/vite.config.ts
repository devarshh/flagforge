import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  base: '/demo/',
  plugins: [react()],
  server: {
    port: 5174,
    strictPort: true,
    proxy: {
      '/sdk': { target: 'http://localhost:5102', ws: true },
    },
  },
  test: {
    environment: 'jsdom',
  },
});
