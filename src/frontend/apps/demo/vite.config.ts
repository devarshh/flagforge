import react from '@vitejs/plugin-react';
import { fileURLToPath } from 'node:url';
import { loadEnv } from 'vite';
import { defineConfig } from 'vitest/config';

const repositoryRoot = fileURLToPath(new URL('../../../..', import.meta.url));

/** The seeded development key (see `.env.example`); only the dev server uses it, never a production build. */
const developmentSdkKey = 'ffk_local_demo_key_for_development_only_000';

export default defineConfig(({ command, mode }) => {
  const rootEnv = loadEnv(mode, repositoryRoot, '');
  const sdkKey =
    process.env.VITE_DEMO_SDK_KEY ??
    rootEnv.VITE_DEMO_SDK_KEY ??
    rootEnv.FF_SEED_DEMO_SDK_KEY ??
    developmentSdkKey;
  return {
    base: '/demo/',
    plugins: [react()],
    define:
      command === 'serve' ? { 'import.meta.env.VITE_DEMO_SDK_KEY': JSON.stringify(sdkKey) } : {},
    build: {
      rolldownOptions: {
        output: {
          // Libraries change less often than the store, so they get their own long-cached chunks.
          codeSplitting: {
            groups: [
              { name: 'react', test: /node_modules[\\/](react|react-dom|scheduler)[\\/]/ },
              { name: 'signalr', test: /node_modules[\\/]@microsoft[\\/]signalr[\\/]/ },
              {
                name: 'mui',
                test: /node_modules[\\/](@mui|@emotion|@popperjs|react-transition-group)[\\/]/,
              },
            ],
          },
        },
      },
    },
    server: {
      port: 5174,
      strictPort: true,
      proxy: {
        '/sdk': { target: 'http://localhost:5102', ws: true },
      },
    },
    test: {
      environment: 'jsdom',
      setupFiles: ['./src/test/setup.ts'],
      css: false,
    },
  };
});
