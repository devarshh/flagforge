import { useColorScheme } from '@mui/material/styles';

export const colorModeStorageKey = 'ff-color-mode';

export type ColorMode = 'light' | 'dark' | 'system';

export function useColorMode(): { mode: ColorMode; setMode: (mode: ColorMode) => void } {
  const { mode, setMode } = useColorScheme();
  return { mode: mode ?? 'system', setMode };
}
