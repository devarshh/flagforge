import { alpha, createTheme } from '@mui/material/styles';
import type {} from '@mui/x-data-grid/themeAugmentation';
import type {} from '@mui/x-date-pickers/themeAugmentation';
import { fonts, palette, radius } from './tokens';

declare module '@mui/material/styles' {
  interface Palette {
    lamp: { on: string; off: string };
    border: string;
  }
  interface PaletteOptions {
    lamp?: { on: string; off: string };
    border?: string;
  }
  interface TypeBackground {
    surface: string;
  }
}

function schemePalette(mode: 'light' | 'dark') {
  const colors = palette[mode];
  return {
    mode,
    primary: { main: colors.primary },
    success: { main: colors.on, dark: colors.onDark },
    warning: { main: colors.caution, dark: colors.cautionDark },
    error: { main: colors.stop, dark: colors.stopDark },
    text: { primary: colors.ink, secondary: colors.inkMuted },
    background: { default: colors.paper, paper: colors.surface, surface: colors.surface },
    divider: colors.border,
    border: colors.border,
    lamp: { on: colors.on, off: colors.off },
  };
}

export const theme = createTheme({
  cssVariables: { colorSchemeSelector: 'data-color-scheme' },
  colorSchemes: {
    light: { palette: schemePalette('light') },
    dark: { palette: schemePalette('dark') },
  },
  shape: { borderRadius: radius.control },
  typography: {
    fontFamily: fonts.sans,
    fontSize: 14,
    h1: { fontSize: 32, fontWeight: 600, lineHeight: 1.25 },
    h2: { fontSize: 24, fontWeight: 600, lineHeight: 1.3 },
    h3: { fontSize: 20, fontWeight: 600, lineHeight: 1.35 },
    h4: { fontSize: 16, fontWeight: 600, lineHeight: 1.4 },
    h5: { fontSize: 16, fontWeight: 600 },
    h6: { fontSize: 16, fontWeight: 600 },
    subtitle1: { fontSize: 16 },
    subtitle2: { fontSize: 14, fontWeight: 600 },
    body1: { fontSize: 14 },
    body2: { fontSize: 13 },
    caption: { fontSize: 13 },
    button: { fontSize: 14, fontWeight: 500, textTransform: 'none' },
    overline: { fontSize: 13, textTransform: 'none', letterSpacing: 0 },
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        'code, kbd, .mono': { fontFamily: fonts.mono },
        '.visually-hidden': {
          position: 'absolute',
          width: 1,
          height: 1,
          margin: -1,
          padding: 0,
          overflow: 'hidden',
          clip: 'rect(0 0 0 0)',
          whiteSpace: 'nowrap',
          border: 0,
        },
        ':focus-visible': { outline: '2px solid', outlineOffset: 2 },
        '@media (prefers-reduced-motion: reduce)': {
          '*, *::before, *::after': {
            animationDuration: '0.01ms !important',
            transitionDuration: '0.01ms !important',
          },
        },
      },
    },
    MuiButton: { defaultProps: { disableElevation: true } },
    MuiPaper: {
      defaultProps: { elevation: 0 },
      styleOverrides: { root: { backgroundImage: 'none' } },
    },
    MuiCard: {
      defaultProps: { variant: 'outlined' },
      styleOverrides: { root: { borderRadius: radius.panel } },
    },
    MuiAppBar: {
      defaultProps: { elevation: 0, color: 'inherit' },
      styleOverrides: {
        root: ({ theme: t }) => ({
          borderBottom: `1px solid ${t.vars?.palette.divider ?? t.palette.divider}`,
          backgroundColor: t.vars?.palette.background.paper ?? t.palette.background.paper,
        }),
      },
    },
    MuiDialog: {
      defaultProps: { slotProps: { paper: { elevation: 8 } } },
      styleOverrides: { paper: { borderRadius: radius.dialog } },
    },
    MuiMenu: { defaultProps: { slotProps: { paper: { elevation: 6 } } } },
    MuiPopover: { defaultProps: { slotProps: { paper: { elevation: 6 } } } },
    MuiChip: { styleOverrides: { root: { borderRadius: radius.control } } },
    MuiTooltip: { styleOverrides: { tooltip: { fontSize: 13 } } },
    MuiTab: { styleOverrides: { root: { textTransform: 'none', minHeight: 44 } } },
    MuiTextField: { defaultProps: { size: 'small' } },
    MuiSelect: { defaultProps: { size: 'small' } },
    MuiAutocomplete: { defaultProps: { size: 'small' } },
    MuiPickersTextField: { defaultProps: { size: 'small' } },
    MuiDataGrid: {
      styleOverrides: {
        root: ({ theme: t }) => ({
          borderRadius: radius.panel,
          '--DataGrid-containerBackground':
            t.vars?.palette.background.paper ?? t.palette.background.paper,
        }),
        row: ({ theme: t }) => ({
          '&:hover': { backgroundColor: alpha(t.palette.primary.main, 0.04) },
        }),
      },
    },
  },
});
