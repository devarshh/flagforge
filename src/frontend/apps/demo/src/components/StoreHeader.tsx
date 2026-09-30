import {
  AppBar,
  Badge,
  Box,
  Button,
  IconButton,
  ListItemText,
  Menu,
  MenuItem,
  Toolbar,
  Tooltip,
  Typography,
} from '@mui/material';
import ExpandMoreRounded from '@mui/icons-material/ExpandMoreRounded';
import FlagOutlined from '@mui/icons-material/FlagOutlined';
import SettingsOutlined from '@mui/icons-material/SettingsOutlined';
import ShoppingBagOutlined from '@mui/icons-material/ShoppingBagOutlined';
import { useState } from 'react';
import { personas, type Persona } from '../personas';

export interface StoreHeaderProps {
  persona: Persona;
  contextKey: string;
  cartCount: number;
  onPersonaChange: (persona: Persona) => void;
  onOpenCart: () => void;
  onToggleInspector: () => void;
  onOpenSettings: () => void;
}

export function StoreHeader({
  persona,
  contextKey,
  cartCount,
  onPersonaChange,
  onOpenCart,
  onToggleInspector,
  onOpenSettings,
}: StoreHeaderProps) {
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  return (
    <AppBar position="sticky" color="primary" elevation={0}>
      <Toolbar sx={{ gap: 1 }}>
        <Typography variant="h2" component="p" sx={{ fontSize: 20, whiteSpace: 'nowrap' }}>
          Acme Coffee
        </Typography>
        <Box sx={{ flexGrow: 1 }} />
        <Button
          color="inherit"
          endIcon={<ExpandMoreRounded />}
          aria-haspopup="menu"
          onClick={(event) => setAnchor(event.currentTarget)}
          sx={{ minWidth: 0 }}
        >
          <Box
            component="span"
            sx={{ display: { xs: 'none', sm: 'inline' }, mr: 0.5, opacity: 0.8 }}
          >
            Shopping as
          </Box>
          {persona.id === 'random' ? contextKey : persona.name}
        </Button>
        <Menu anchorEl={anchor} open={anchor !== null} onClose={() => setAnchor(null)}>
          {personas.map((option) => (
            <MenuItem
              key={option.id}
              selected={option.id === persona.id}
              onClick={() => {
                setAnchor(null);
                onPersonaChange(option);
              }}
            >
              <ListItemText primary={option.name} secondary={option.description} />
            </MenuItem>
          ))}
        </Menu>
        <Tooltip title="Flag inspector">
          <IconButton color="inherit" aria-label="Flag inspector" onClick={onToggleInspector}>
            <FlagOutlined />
          </IconButton>
        </Tooltip>
        <Tooltip title="Connection settings">
          <IconButton color="inherit" aria-label="Connection settings" onClick={onOpenSettings}>
            <SettingsOutlined />
          </IconButton>
        </Tooltip>
        <Tooltip title="Cart">
          <IconButton color="inherit" aria-label={`Cart, ${cartCount} items`} onClick={onOpenCart}>
            <Badge badgeContent={cartCount} color="secondary">
              <ShoppingBagOutlined />
            </Badge>
          </IconButton>
        </Tooltip>
      </Toolbar>
    </AppBar>
  );
}
