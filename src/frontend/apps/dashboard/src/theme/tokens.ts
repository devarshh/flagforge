/**
 * Design tokens. The palette borrows from signals: lit and unlit lamps, with state colors kept apart from
 * environment colors. Go green and caution amber are for lamps, borders, and tinted chips (3:1 is enough for UI
 * components); text on light backgrounds uses the `dark` shades, which meet WCAG AA.
 */
export const palette = {
  light: {
    ink: '#1C2733',
    inkMuted: '#4B5768',
    paper: '#F5F7FA',
    surface: '#FFFFFF',
    border: '#D8DEE6',
    primary: '#1F5FBF',
    on: '#1E8E5A',
    onDark: '#146B43',
    off: '#7B8594',
    caution: '#B86E00',
    cautionDark: '#8A5200',
    stop: '#C2362B',
    stopDark: '#9E2A21',
  },
  dark: {
    ink: '#E6EBF1',
    inkMuted: '#A9B4C2',
    paper: '#111A24',
    surface: '#18222F',
    border: '#2B3848',
    primary: '#6EA0F0',
    on: '#4CC38A',
    onDark: '#7BD9AB',
    off: '#8C97A6',
    caution: '#E0A040',
    cautionDark: '#F0C07A',
    stop: '#F07167',
    stopDark: '#F59D95',
  },
} as const;

export const radius = { control: 6, panel: 8, dialog: 12 } as const;

export const fonts = {
  sans: '"IBM Plex Sans", system-ui, -apple-system, "Segoe UI", sans-serif',
  mono: '"IBM Plex Mono", ui-monospace, SFMono-Regular, Menlo, monospace',
} as const;

/** Colors environments can take in settings; distinct from the state colors above. */
export const environmentPalette = [
  '#3A7CA5',
  '#8E6CC0',
  '#C2362B',
  '#2F8F83',
  '#B86E00',
  '#5B6B7F',
  '#A0527A',
  '#4F7A28',
] as const;

/** Colors that tell variations apart in rollout bars and charts, in variation order. */
export const variationPalette = [
  '#1F5FBF',
  '#8E6CC0',
  '#2F8F83',
  '#B86E00',
  '#A0527A',
  '#4F7A28',
  '#5B6B7F',
  '#3A7CA5',
] as const;

export function variationColor(index: number): string {
  return variationPalette[index % variationPalette.length]!;
}
