import { createContext, useContext } from 'react';

export type NotifySeverity = 'success' | 'error' | 'info';

export type Notify = (message: string, severity?: NotifySeverity) => void;

export const NotifyContext = createContext<Notify>(() => undefined);

/** Shows a short confirmation such as "Flag turned on" or "Changes saved". */
export function useNotify(): Notify {
  return useContext(NotifyContext);
}
