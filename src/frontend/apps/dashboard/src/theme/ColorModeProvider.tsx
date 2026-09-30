import { CssBaseline } from '@mui/material';
import { ThemeProvider } from '@mui/material/styles';
import type { ReactNode } from 'react';
import { colorModeStorageKey } from './colorMode';
import { theme } from './theme';

/** Light, dark, or system color mode, persisted in localStorage under `ff-color-mode`. */
export function ColorModeProvider({ children }: { children?: ReactNode }) {
  return (
    <ThemeProvider
      theme={theme}
      defaultMode="system"
      modeStorageKey={colorModeStorageKey}
      disableTransitionOnChange
    >
      <CssBaseline enableColorScheme />
      {children}
    </ThemeProvider>
  );
}
