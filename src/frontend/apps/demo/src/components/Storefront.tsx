import {
  Box,
  Container,
  CssBaseline,
  Drawer,
  Snackbar,
  Typography,
  useMediaQuery,
} from '@mui/material';
import { ThemeProvider } from '@mui/material/styles';
import { useFlag } from '@flagforge/sdk/react';
import type { EvaluationContext } from '@flagforge/sdk';
import { useMemo, useState } from 'react';
import { addItem, cartCount, removeItem, type CartLine } from '../cart';
import { cartLimitOf, flagDefaults, flagKeys, storeThemeOf } from '../flags';
import type { Persona } from '../personas';
import { createStoreTheme } from '../theme';
import { CartDrawer } from './CartDrawer';
import { FlagInspector } from './FlagInspector';
import { ProductList } from './ProductList';
import { PromoBanner } from './PromoBanner';
import { StoreHeader } from './StoreHeader';

const inspectorWidth = 380;

export interface StorefrontProps {
  persona: Persona;
  context: EvaluationContext;
  onPersonaChange: (persona: Persona) => void;
  onOpenSettings: () => void;
}

/** The store. Everything a flag controls reads it through the SDK's React hooks, so changes show up live. */
export function Storefront({ persona, context, onPersonaChange, onOpenSettings }: StorefrontProps) {
  // The SDK keeps a JSON value's identity while it is unchanged, so the theme is rebuilt only on real changes.
  const servedTheme = useFlag(flagKeys.storeTheme, flagDefaults.storeTheme);
  const theme = useMemo(() => createStoreTheme(storeThemeOf(servedTheme)), [servedTheme]);
  const limit = cartLimitOf(useFlag(flagKeys.maxCartItems, flagDefaults.maxCartItems));
  // noSsr reads the real value on the first render, which decides whether the inspector starts open.
  const wide = useMediaQuery(theme.breakpoints.up('lg'), { noSsr: true });
  const [lines, setLines] = useState<CartLine[]>([]);
  const [cartOpen, setCartOpen] = useState(false);
  // Docked beside the store on wide screens; on narrower ones it would cover the store, so it starts closed.
  const [inspectorOpen, setInspectorOpen] = useState(wide);
  const [ordered, setOrdered] = useState(false);
  const count = cartCount(lines);
  const inspectorDocked = wide && inspectorOpen;

  return (
    <ThemeProvider theme={theme}>
      <CssBaseline />
      <Box
        sx={{
          minHeight: '100vh',
          bgcolor: 'background.default',
          mr: inspectorDocked ? `${inspectorWidth}px` : 0,
        }}
      >
        <StoreHeader
          persona={persona}
          contextKey={context.key}
          cartCount={count}
          onPersonaChange={onPersonaChange}
          onOpenCart={() => setCartOpen(true)}
          onToggleInspector={() => setInspectorOpen((open) => !open)}
          onOpenSettings={onOpenSettings}
        />
        <PromoBanner />
        <Container component="main" maxWidth="lg" sx={{ py: 4 }}>
          <Typography variant="h1" sx={{ mb: 0.5 }}>
            Freshly roasted beans
          </Typography>
          <Typography color="text.secondary" sx={{ mb: 3 }}>
            Roasted this week and shipped whole. Carts hold up to {limit} bags.
          </Typography>
          <ProductList
            canAdd={count < limit}
            limit={limit}
            onAdd={(product) => setLines((current) => addItem(current, product.id, limit))}
          />
        </Container>
      </Box>
      <Drawer
        anchor="right"
        variant={wide ? 'persistent' : 'temporary'}
        open={inspectorOpen}
        onClose={() => setInspectorOpen(false)}
        slotProps={{ paper: { sx: { width: { xs: '100%', sm: inspectorWidth } } } }}
      >
        <FlagInspector context={context} onClose={() => setInspectorOpen(false)} />
      </Drawer>
      <CartDrawer
        open={cartOpen}
        onClose={() => setCartOpen(false)}
        lines={lines}
        limit={limit}
        onAdd={(productId) => setLines((current) => addItem(current, productId, limit))}
        onRemove={(productId) => setLines((current) => removeItem(current, productId))}
        onCheckout={() => {
          setLines([]);
          setCartOpen(false);
          setOrdered(true);
        }}
      />
      <Snackbar
        open={ordered}
        autoHideDuration={5000}
        onClose={() => setOrdered(false)}
        message="Thanks! This is a demo store, so nothing was ordered."
      />
    </ThemeProvider>
  );
}
