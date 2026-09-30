import { Box, Button, Card, CardContent, Stack, Tooltip, Typography } from '@mui/material';
import AddShoppingCartRounded from '@mui/icons-material/AddShoppingCartRounded';
import { useFlag } from '@flagforge/sdk/react';
import { flagDefaults, flagKeys } from '../flags';
import { formatPrice, products, type Product } from '../products';
import { CoffeeArt } from './CoffeeArt';

export interface ProductListProps {
  canAdd: boolean;
  limit: number;
  onAdd: (product: Product) => void;
}

/** The six coffees, as a grid when `new-product-layout` is on and as a list otherwise. */
export function ProductList({ canAdd, limit, onAdd }: ProductListProps) {
  const grid = useFlag(flagKeys.newProductLayout, flagDefaults.newProductLayout);
  const addButton = (product: Product) => (
    <Tooltip title={canAdd ? '' : `Your cart holds up to ${limit} items.`}>
      <span>
        <Button
          variant="contained"
          color="primary"
          startIcon={<AddShoppingCartRounded />}
          disabled={!canAdd}
          onClick={() => onAdd(product)}
          aria-label={`Add ${product.name} to cart`}
        >
          Add to cart
        </Button>
      </span>
    </Tooltip>
  );

  return (
    <Box
      component="ul"
      aria-label={grid ? 'Coffees, grid layout' : 'Coffees, list layout'}
      sx={{
        listStyle: 'none',
        p: 0,
        m: 0,
        display: 'grid',
        gap: 2,
        gridTemplateColumns: grid
          ? { xs: '1fr', sm: 'repeat(2, 1fr)', md: 'repeat(3, 1fr)' }
          : '1fr',
      }}
    >
      {products.map((product) => (
        <Card
          component="li"
          key={product.id}
          variant="outlined"
          sx={{ display: 'flex', flexDirection: grid ? 'column' : 'row' }}
        >
          <CoffeeArt art={product.art} compact={!grid} />
          <CardContent
            sx={{
              flexGrow: 1,
              display: 'flex',
              flexDirection: grid ? 'column' : { xs: 'column', sm: 'row' },
              gap: 2,
            }}
          >
            <Box sx={{ flexGrow: 1 }}>
              <Typography variant="h3" component="h2">
                {product.name}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {product.origin}
              </Typography>
              <Typography variant="body2" sx={{ mt: 1 }}>
                {product.notes}
              </Typography>
            </Box>
            <Stack
              direction={grid ? 'row' : { xs: 'row', sm: 'column' }}
              spacing={1.5}
              sx={{
                alignItems: grid ? 'center' : { xs: 'center', sm: 'flex-end' },
                justifyContent: 'space-between',
              }}
            >
              <Typography sx={{ fontWeight: 600 }}>{formatPrice(product.price)}</Typography>
              {addButton(product)}
            </Stack>
          </CardContent>
        </Card>
      ))}
    </Box>
  );
}
