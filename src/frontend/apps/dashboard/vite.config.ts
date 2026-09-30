import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': 'http://localhost:5101',
    },
  },
  build: {
    rolldownOptions: {
      output: {
        // Libraries change less often than the app, so they get their own long-cached chunks.
        codeSplitting: {
          groups: [
            {
              name: 'react',
              test: /node_modules[\\/](react|react-dom|react-router|scheduler|@tanstack)[\\/]/,
            },
            {
              name: 'mui',
              test: /node_modules[\\/](@mui[\\/](material|system|utils|styled-engine|private-theming)|@emotion|@popperjs|react-transition-group)[\\/]/,
            },
          ],
        },
      },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
  },
});
