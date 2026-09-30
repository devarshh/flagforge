import {
  AppBar,
  Box,
  Container,
  Drawer,
  IconButton,
  Link,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  ListSubheader,
  Toolbar,
  Typography,
  useMediaQuery,
  useTheme,
} from '@mui/material';
import AdminPanelSettingsOutlined from '@mui/icons-material/AdminPanelSettingsOutlined';
import FlagOutlined from '@mui/icons-material/FlagOutlined';
import FolderOutlined from '@mui/icons-material/FolderOutlined';
import HistoryOutlined from '@mui/icons-material/HistoryOutlined';
import HourglassEmptyOutlined from '@mui/icons-material/HourglassEmptyOutlined';
import MenuRounded from '@mui/icons-material/MenuRounded';
import TuneOutlined from '@mui/icons-material/TuneOutlined';
import { useQuery } from '@tanstack/react-query';
import { useState, type ReactNode } from 'react';
import { NavLink, Outlet, useMatch } from 'react-router';
import { apiClient } from '../api/client';
import { queryKeys } from '../api/queryKeys';
import type { Meta } from '../api/types';
import { hasRole, useCurrentUser } from '../auth/authContext';
import { ColorModeMenu } from './ColorModeMenu';
import { ProjectSwitcher } from './ProjectSwitcher';
import { UserMenu } from './UserMenu';

const drawerWidth = 232;

interface NavItem {
  to: string;
  label: string;
  icon: ReactNode;
}

export function AppShell() {
  const theme = useTheme();
  const desktop = useMediaQuery(theme.breakpoints.up('md'));
  const [mobileOpen, setMobileOpen] = useState(false);
  const user = useCurrentUser();
  const projectKey = useMatch('/projects/:projectKey/*')?.params.projectKey;
  const { data: meta } = useQuery({
    queryKey: queryKeys.meta,
    queryFn: () => apiClient.get<Meta>('/api/v1/meta'),
    staleTime: Infinity,
  });

  const projectItems: NavItem[] = projectKey
    ? [
        { to: `/projects/${projectKey}/flags`, label: 'Flags', icon: <FlagOutlined /> },
        {
          to: `/projects/${projectKey}/stale`,
          label: 'Stale flags',
          icon: <HourglassEmptyOutlined />,
        },
        { to: `/projects/${projectKey}/settings`, label: 'Settings', icon: <TuneOutlined /> },
      ]
    : [];
  const globalItems: NavItem[] = [
    { to: '/projects', label: 'All projects', icon: <FolderOutlined /> },
    { to: '/audit', label: 'Audit log', icon: <HistoryOutlined /> },
    ...(hasRole(user, 'admin')
      ? [{ to: '/admin/users', label: 'Users', icon: <AdminPanelSettingsOutlined /> }]
      : []),
  ];

  const navigation = (
    <Box component="nav" aria-label="Main" sx={{ overflow: 'auto', py: 1 }}>
      {projectItems.length > 0 && (
        <List dense subheader={<ListSubheader disableSticky>Project</ListSubheader>}>
          {projectItems.map((item) => (
            <NavItemLink key={item.to} item={item} onNavigate={() => setMobileOpen(false)} />
          ))}
        </List>
      )}
      <List dense subheader={<ListSubheader disableSticky>Workspace</ListSubheader>}>
        {globalItems.map((item) => (
          <NavItemLink
            key={item.to}
            item={item}
            end={item.to === '/projects'}
            onNavigate={() => setMobileOpen(false)}
          />
        ))}
      </List>
    </Box>
  );

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh', bgcolor: 'background.default' }}>
      <Link
        href="#main-content"
        sx={{
          position: 'absolute',
          left: 8,
          top: -48,
          zIndex: (t) => t.zIndex.tooltip,
          p: 1,
          bgcolor: 'background.paper',
          '&:focus': { top: 8 },
        }}
      >
        Skip to content
      </Link>
      <AppBar position="fixed" sx={{ zIndex: (t) => t.zIndex.drawer + 1 }}>
        <Toolbar sx={{ gap: 1 }}>
          {!desktop && (
            <IconButton
              edge="start"
              aria-label="Open navigation"
              onClick={() => setMobileOpen(true)}
            >
              <MenuRounded />
            </IconButton>
          )}
          <Typography
            component={NavLink}
            to="/projects"
            variant="h4"
            sx={{ color: 'text.primary', textDecoration: 'none', mr: 1, whiteSpace: 'nowrap' }}
          >
            FlagForge
          </Typography>
          <ProjectSwitcher projectKey={projectKey} />
          <Box sx={{ flexGrow: 1 }} />
          <ColorModeMenu />
          <UserMenu />
        </Toolbar>
      </AppBar>
      <Drawer
        variant={desktop ? 'permanent' : 'temporary'}
        open={desktop || mobileOpen}
        onClose={() => setMobileOpen(false)}
        sx={{
          width: drawerWidth,
          flexShrink: 0,
          '& .MuiDrawer-paper': { width: drawerWidth, boxSizing: 'border-box' },
        }}
      >
        <Toolbar />
        {navigation}
      </Drawer>
      <Box sx={{ flexGrow: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
        <Toolbar />
        <Container
          component="main"
          id="main-content"
          tabIndex={-1}
          maxWidth="xl"
          sx={{ py: 3, flexGrow: 1, outline: 'none' }}
        >
          <Outlet />
        </Container>
        <Box component="footer" sx={{ px: 3, py: 2, borderTop: 1, borderColor: 'divider' }}>
          <Typography variant="body2" color="text.secondary">
            FlagForge {meta ? `${meta.version} (${meta.commit})` : ''}
          </Typography>
        </Box>
      </Box>
    </Box>
  );
}

function NavItemLink({
  item,
  end = false,
  onNavigate,
}: {
  item: NavItem;
  end?: boolean;
  onNavigate: () => void;
}) {
  return (
    <ListItemButton
      component={NavLink}
      to={item.to}
      end={end}
      onClick={onNavigate}
      sx={{ mx: 1, borderRadius: 1, '&.active': { bgcolor: 'action.selected', fontWeight: 600 } }}
    >
      <ListItemIcon sx={{ minWidth: 36 }}>{item.icon}</ListItemIcon>
      <ListItemText primary={item.label} />
    </ListItemButton>
  );
}
