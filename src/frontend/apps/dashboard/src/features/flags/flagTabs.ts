export const flagTabs = [
  { id: 'targeting', label: 'Targeting', perEnvironment: true },
  { id: 'variations', label: 'Variations', perEnvironment: false },
  { id: 'schedule', label: 'Schedule', perEnvironment: true },
  { id: 'insights', label: 'Insights', perEnvironment: true },
  { id: 'history', label: 'History', perEnvironment: false },
  { id: 'settings', label: 'Settings', perEnvironment: false },
] as const;

export type FlagTabId = (typeof flagTabs)[number]['id'];

export function isFlagTab(value: string | null): value is FlagTabId {
  return flagTabs.some((tab) => tab.id === value);
}
