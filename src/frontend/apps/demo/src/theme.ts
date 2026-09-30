import { createTheme, type Theme } from '@mui/material/styles';
import type { StoreTheme } from './flags';

/** Acme Coffee's own look: espresso, white surfaces, and a matcha accent that the store-theme flag can change. */
export const brand = {
  espresso: '#3B2A20',
  cream: '#F7F3EE',
  surface: '#FFFFFF',
  ink: '#2B1E17',
  muted: '#6B5A4E',
  success: '#2E7D32',
} as const;

export function createStoreTheme({ accent, rounded }: StoreTheme): Theme {
  return createTheme({
    palette: {
      mode: 'light',
      primary: { main: brand.espresso },
      secondary: { main: accent },
      success: { main: brand.success },
      background: { default: brand.cream, paper: brand.surface },
      text: { primary: brand.ink, secondary: brand.muted },
    },
    shape: { borderRadius: rounded ? 14 : 2 },
    typography: {
      fontFamily: '"IBM Plex Sans", system-ui, -apple-system, "Segoe UI", sans-serif',
      h1: { fontSize: 28, fontWeight: 600 },
      h2: { fontSize: 22, fontWeight: 600 },
      h3: { fontSize: 18, fontWeight: 600 },
      button: { textTransform: 'none', fontWeight: 600 },
    },
    components: {
      MuiButton: { defaultProps: { disableElevation: true } },
      MuiCssBaseline: {
        styleOverrides: {
          // The doubled class outranks component styles (one class each), which are injected after global ones.
          '.mono.mono': { fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace' },
          '@media (prefers-reduced-motion: reduce)': {
            '*, *::before, *::after': {
              transitionDuration: '0.01ms !important',
              animationDuration: '0.01ms !important',
            },
          },
        },
      },
    },
  });
}
