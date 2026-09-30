import { IconButton, ListItemIcon, ListItemText, Menu, MenuItem, Tooltip } from '@mui/material';
import CheckRounded from '@mui/icons-material/CheckRounded';
import DarkModeOutlined from '@mui/icons-material/DarkModeOutlined';
import LightModeOutlined from '@mui/icons-material/LightModeOutlined';
import SettingsBrightnessOutlined from '@mui/icons-material/SettingsBrightnessOutlined';
import { useState } from 'react';
import { useColorMode, type ColorMode } from '../theme/colorMode';

const options: { mode: ColorMode; label: string }[] = [
  { mode: 'light', label: 'Light' },
  { mode: 'dark', label: 'Dark' },
  { mode: 'system', label: 'Match system' },
];

export function ColorModeMenu() {
  const { mode, setMode } = useColorMode();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const icon =
    mode === 'dark' ? (
      <DarkModeOutlined />
    ) : mode === 'light' ? (
      <LightModeOutlined />
    ) : (
      <SettingsBrightnessOutlined />
    );
  return (
    <>
      <Tooltip title="Color mode">
        <IconButton
          color="inherit"
          aria-label="Color mode"
          aria-haspopup="menu"
          onClick={(event) => setAnchor(event.currentTarget)}
        >
          {icon}
        </IconButton>
      </Tooltip>
      <Menu anchorEl={anchor} open={anchor !== null} onClose={() => setAnchor(null)}>
        {options.map((option) => (
          <MenuItem
            key={option.mode}
            selected={option.mode === mode}
            onClick={() => {
              setMode(option.mode);
              setAnchor(null);
            }}
          >
            <ListItemIcon>{option.mode === mode && <CheckRounded fontSize="small" />}</ListItemIcon>
            <ListItemText>{option.label}</ListItemText>
          </MenuItem>
        ))}
      </Menu>
    </>
  );
}
