import { Avatar, Divider, IconButton, ListItemText, Menu, MenuItem, Tooltip } from '@mui/material';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { useAuth, useCurrentUser } from '../auth/authContext';

const roleNames = { viewer: 'Viewer', editor: 'Editor', admin: 'Admin' } as const;

export function UserMenu() {
  const user = useCurrentUser();
  const { logout } = useAuth();
  const navigate = useNavigate();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const initials = user.displayName
    .split(/\s+/)
    .map((part) => part[0])
    .join('')
    .slice(0, 2)
    .toUpperCase();

  return (
    <>
      <Tooltip title="Account">
        <IconButton
          aria-label={`Account: ${user.displayName}`}
          aria-haspopup="menu"
          onClick={(event) => setAnchor(event.currentTarget)}
        >
          <Avatar sx={{ width: 30, height: 30, fontSize: 13, bgcolor: 'primary.main' }}>
            {initials}
          </Avatar>
        </IconButton>
      </Tooltip>
      <Menu
        anchorEl={anchor}
        open={anchor !== null}
        onClose={() => setAnchor(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
      >
        <ListItemText
          sx={{ px: 2, py: 1 }}
          primary={user.displayName}
          secondary={`${user.email} (${roleNames[user.role]})`}
        />
        <Divider />
        <MenuItem
          onClick={() => {
            setAnchor(null);
            void navigate('/account/password');
          }}
        >
          Change password
        </MenuItem>
        <MenuItem
          onClick={() => {
            setAnchor(null);
            void logout().then(() => navigate('/login'));
          }}
        >
          Sign out
        </MenuItem>
      </Menu>
    </>
  );
}
