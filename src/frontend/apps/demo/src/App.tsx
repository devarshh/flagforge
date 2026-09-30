import { CssBaseline } from '@mui/material';
import { ThemeProvider } from '@mui/material/styles';
import { createClient, type EvaluationContext, type FlagForgeClient } from '@flagforge/sdk';
import { FlagForgeProvider } from '@flagforge/sdk/react';
import { useCallback, useEffect, useRef, useState } from 'react';
import { SettingsDialog } from './components/SettingsDialog';
import { Storefront } from './components/Storefront';
import { Welcome } from './components/Welcome';
import { flagDefaults } from './flags';
import { personas, type Persona } from './personas';
import { chooseSdkKey, readConfiguredKey, readStoredKey, storeKey } from './sdkKey';
import { createStoreTheme } from './theme';

const baseTheme = createStoreTheme(flagDefaults.storeTheme);
const firstPersona = personas[0]!;

interface Connection {
  id: number;
  sdkKey: string;
  client: FlagForgeClient;
}

async function defaultKey(): Promise<string | null> {
  return chooseSdkKey({
    stored: null,
    configured: await readConfiguredKey(),
    development: import.meta.env.VITE_DEMO_SDK_KEY,
  });
}

/**
 * Finds an SDK key (pasted in settings, the deployment's `config.json`, or the dev server's fallback), creates one
 * FlagForge client for it, and switches shoppers with `identify` so the store re-evaluates without reconnecting.
 */
export function App() {
  const [persona, setPersona] = useState<Persona>(firstPersona);
  const [context, setContext] = useState<EvaluationContext>(() => firstPersona.context());
  const [connection, setConnection] = useState<Connection | null>(null);
  const [resolving, setResolving] = useState(true);
  const [usingStoredKey, setUsingStoredKey] = useState(false);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const connectionRef = useRef<Connection | null>(null);
  const contextRef = useRef(context);

  const connect = useCallback((sdkKey: string) => {
    const previous = connectionRef.current;
    void previous?.client.close();
    const next: Connection = {
      id: (previous?.id ?? 0) + 1,
      sdkKey,
      client: createClient({
        baseUrl: window.location.origin,
        sdkKey,
        context: contextRef.current,
      }),
    };
    connectionRef.current = next;
    setConnection(next);
  }, []);

  useEffect(() => {
    let cancelled = false;
    void Promise.all([Promise.resolve(readStoredKey()), defaultKey()]).then(
      ([stored, fallback]) => {
        if (cancelled) {
          return;
        }

        setResolving(false);
        setUsingStoredKey(stored !== null);
        const sdkKey = stored ?? fallback;
        if (sdkKey) {
          connect(sdkKey);
        } else {
          setSettingsOpen(true);
        }
      },
    );
    return () => {
      cancelled = true;
    };
  }, [connect]);

  useEffect(
    () => () => {
      void connectionRef.current?.client.close();
      connectionRef.current = null;
    },
    [],
  );

  const changePersona = (next: Persona) => {
    const nextContext = next.context();
    contextRef.current = nextContext;
    setPersona(next);
    setContext(nextContext);
    void connectionRef.current?.client.identify(nextContext);
  };

  const saveKey = (sdkKey: string) => {
    storeKey(sdkKey);
    setUsingStoredKey(true);
    setSettingsOpen(false);
    connect(sdkKey);
  };

  const resetToDefaultKey = async () => {
    storeKey(null);
    setUsingStoredKey(false);
    setSettingsOpen(false);
    const sdkKey = await defaultKey();
    if (sdkKey) {
      connect(sdkKey);
    } else {
      setSettingsOpen(true);
    }
  };

  return (
    <ThemeProvider theme={baseTheme}>
      <CssBaseline />
      {connection ? (
        <FlagForgeProvider key={connection.id} client={connection.client}>
          <Storefront
            persona={persona}
            context={context}
            onPersonaChange={changePersona}
            onOpenSettings={() => setSettingsOpen(true)}
          />
        </FlagForgeProvider>
      ) : (
        <Welcome resolving={resolving} onOpenSettings={() => setSettingsOpen(true)} />
      )}
      <SettingsDialog
        open={settingsOpen}
        currentKey={connection?.sdkKey ?? null}
        usingStoredKey={usingStoredKey}
        onSave={saveKey}
        onUseDefault={() => void resetToDefaultKey()}
        onClose={() => setSettingsOpen(false)}
      />
    </ThemeProvider>
  );
}
