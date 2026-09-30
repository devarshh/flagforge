import { Alert, Box, Button, Divider, Drawer, IconButton, Stack, Typography } from '@mui/material';
import AddRounded from '@mui/icons-material/AddRounded';
import CloseRounded from '@mui/icons-material/CloseRounded';
import RemoveRounded from '@mui/icons-material/RemoveRounded';
import { useFlag } from '@flagforge/sdk/react';
import { cartCount, cartTotal, overLimitBy, type CartLine } from '../cart';
import { checkoutColorOf, flagDefaults, flagKeys } from '../flags';
import { formatPrice, productById } from '../products';

export interface CartDrawerProps {
  open: boolean;
  onClose: () => void;
  lines: readonly CartLine[];
  limit: number;
  onAdd: (productId: string) => void;
  onRemove: (productId: string) => void;
  onCheckout: () => void;
}

/** The in-memory cart. `max-cart-items` caps it, and `checkout-button-color` colors the checkout button. */
export function CartDrawer({
  open,
  onClose,
  lines,
  limit,
  onAdd,
  onRemove,
  onCheckout,
}: CartDrawerProps) {
  const checkoutColor = checkoutColorOf(
    useFlag(flagKeys.checkoutButtonColor, flagDefaults.checkoutButtonColor),
  );
  const count = cartCount(lines);
  const excess = overLimitBy(lines, limit);
  return (
    <Drawer
      anchor="right"
      open={open}
      onClose={onClose}
      slotProps={{ paper: { sx: { width: { xs: '100%', sm: 380 } } } }}
    >
      <Stack
        spacing={2}
        sx={{ p: 2.5, height: '100%' }}
        component="section"
        aria-labelledby="cart-title"
      >
        <Stack direction="row" sx={{ alignItems: 'center' }}>
          <Typography id="cart-title" variant="h2" sx={{ flexGrow: 1 }}>
            Your cart
          </Typography>
          <IconButton aria-label="Close cart" onClick={onClose}>
            <CloseRounded />
          </IconButton>
        </Stack>
        <Typography variant="body2" color="text.secondary">
          {count} of {limit} items
        </Typography>
        {excess > 0 && (
          <Alert severity="warning">
            Carts now hold up to {limit} items. Remove {excess} {excess === 1 ? 'item' : 'items'} to
            check out.
          </Alert>
        )}
        {lines.length === 0 ? (
          <Typography color="text.secondary">
            Your cart is empty. Add a coffee to get started.
          </Typography>
        ) : (
          <Stack
            component="ul"
            divider={<Divider />}
            spacing={1.5}
            sx={{ listStyle: 'none', p: 0, m: 0 }}
          >
            {lines.map((line) => {
              const product = productById(line.productId);
              return (
                <Stack
                  component="li"
                  key={line.productId}
                  direction="row"
                  spacing={1}
                  sx={{ alignItems: 'center' }}
                >
                  <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                    <Typography noWrap>{product?.name ?? line.productId}</Typography>
                    <Typography variant="body2" color="text.secondary">
                      {formatPrice((product?.price ?? 0) * line.quantity)}
                    </Typography>
                  </Box>
                  <IconButton
                    size="small"
                    aria-label={`Remove one ${product?.name ?? ''}`}
                    onClick={() => onRemove(line.productId)}
                  >
                    <RemoveRounded fontSize="small" />
                  </IconButton>
                  <Typography aria-label="Quantity" sx={{ minWidth: 20, textAlign: 'center' }}>
                    {line.quantity}
                  </Typography>
                  <IconButton
                    size="small"
                    aria-label={`Add one ${product?.name ?? ''}`}
                    disabled={count >= limit}
                    onClick={() => onAdd(line.productId)}
                  >
                    <AddRounded fontSize="small" />
                  </IconButton>
                </Stack>
              );
            })}
          </Stack>
        )}
        <Box sx={{ flexGrow: 1 }} />
        <Divider />
        <Stack direction="row" sx={{ justifyContent: 'space-between' }}>
          <Typography sx={{ fontWeight: 600 }}>Total</Typography>
          <Typography sx={{ fontWeight: 600 }}>{formatPrice(cartTotal(lines))}</Typography>
        </Stack>
        <Button
          variant="contained"
          size="large"
          color={checkoutColor}
          disabled={lines.length === 0 || excess > 0}
          onClick={onCheckout}
        >
          Check out
        </Button>
      </Stack>
    </Drawer>
  );
}
