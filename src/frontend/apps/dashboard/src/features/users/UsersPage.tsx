import { Button, IconButton, ListItemText, Menu, MenuItem, Stack, Typography } from '@mui/material';
import AddRounded from '@mui/icons-material/AddRounded';
import MoreVertRounded from '@mui/icons-material/MoreVertRounded';
import { DataGrid, type GridColDef } from '@mui/x-data-grid';
import { useCallback, useMemo, useState } from 'react';
import { errorMessage } from '../../api/errors';
import type { User } from '../../api/types';
import { RequireRole } from '../../auth/RequireRole';
import { useCurrentUser } from '../../auth/authContext';
import { ConfirmDialog } from '../../components/ConfirmDialog';
import { ErrorState } from '../../components/ErrorState';
import { PageHeader } from '../../components/PageHeader';
import { RelativeTime } from '../../components/RelativeTime';
import { ToneChip } from '../../components/ToneChip';
import { useNotify } from '../../components/notify';
import { ChangeRoleDialog } from './ChangeRoleDialog';
import { CreateUserDialog } from './CreateUserDialog';
import { roleLabel } from './roles';
import { TemporaryPasswordDialog } from './TemporaryPasswordDialog';
import { useResetPassword, useUpdateUser, useUsers } from './usersApi';

export function UsersPage() {
  return (
    <RequireRole role="admin">
      <UsersAdmin />
    </RequireRole>
  );
}

function UserActions({
  user,
  onChangeRole,
  onResetPassword,
  onSetActive,
}: {
  user: User;
  onChangeRole: () => void;
  onResetPassword: () => void;
  onSetActive: (active: boolean) => void;
}) {
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const run = (action: () => void) => () => {
    setAnchor(null);
    action();
  };
  return (
    <>
      <IconButton
        aria-label={`Actions for ${user.displayName}`}
        aria-haspopup="menu"
        size="small"
        onClick={(event) => setAnchor(event.currentTarget)}
      >
        <MoreVertRounded fontSize="small" />
      </IconButton>
      <Menu anchorEl={anchor} open={anchor !== null} onClose={() => setAnchor(null)}>
        <MenuItem onClick={run(onChangeRole)} disabled={!user.isActive}>
          Change role
        </MenuItem>
        <MenuItem onClick={run(onResetPassword)} disabled={!user.isActive}>
          Reset password
        </MenuItem>
        <MenuItem onClick={run(() => onSetActive(!user.isActive))}>
          {user.isActive ? 'Deactivate' : 'Reactivate'}
        </MenuItem>
      </Menu>
    </>
  );
}

function UsersAdmin() {
  const me = useCurrentUser();
  const notify = useNotify();
  const [pagination, setPagination] = useState({ page: 0, pageSize: 25 });
  const users = useUsers({ page: pagination.page + 1, pageSize: pagination.pageSize });
  const { mutate: updateUser, isPending: updating } = useUpdateUser();
  const resetPassword = useResetPassword();
  const [creating, setCreating] = useState(false);
  const [changingRole, setChangingRole] = useState<User | null>(null);
  const [resetting, setResetting] = useState<User | null>(null);
  const [deactivating, setDeactivating] = useState<User | null>(null);
  const [password, setPassword] = useState<{
    person: { displayName: string; email: string };
    value: string;
  } | null>(null);

  const setActive = useCallback(
    (user: User, isActive: boolean) =>
      updateUser(
        { userId: user.id, changes: { isActive } },
        {
          onSuccess: () => {
            setDeactivating(null);
            notify(
              isActive ? `${user.displayName} reactivated` : `${user.displayName} deactivated`,
            );
          },
          onError: (error) => notify(errorMessage(error), 'error'),
        },
      ),
    [updateUser, notify],
  );

  const columns = useMemo<GridColDef<User>[]>(
    () => [
      {
        field: 'displayName',
        headerName: 'User',
        flex: 1.4,
        minWidth: 220,
        sortable: false,
        display: 'flex',
        renderCell: ({ row }) => (
          <ListItemText
            primary={`${row.displayName}${row.id === me.id ? ' (you)' : ''}`}
            secondary={row.email}
            sx={{ my: 0 }}
          />
        ),
      },
      {
        field: 'role',
        headerName: 'Role',
        width: 110,
        sortable: false,
        valueFormatter: (value: User['role']) => roleLabel(value),
      },
      {
        field: 'status',
        headerName: 'Status',
        flex: 1,
        minWidth: 200,
        sortable: false,
        display: 'flex',
        renderCell: ({ row }) => (
          <Stack direction="row" sx={{ gap: 0.5, flexWrap: 'wrap' }}>
            {row.isActive ? (
              <ToneChip label="Active" tone="success" />
            ) : (
              <ToneChip label="Deactivated" tone="neutral" />
            )}
            {row.lockoutEndsAt && Date.parse(row.lockoutEndsAt) > Date.now() && (
              <ToneChip label="Locked out" tone="error" />
            )}
            {row.mustChangePassword && row.isActive && (
              <ToneChip label="Temporary password" tone="warning" />
            )}
          </Stack>
        ),
      },
      {
        field: 'lastLoginAt',
        headerName: 'Last sign-in',
        width: 140,
        sortable: false,
        display: 'flex',
        renderCell: ({ row }) => <RelativeTime value={row.lastLoginAt} />,
      },
      {
        field: 'actions',
        headerName: 'Actions',
        width: 80,
        sortable: false,
        display: 'flex',
        align: 'right',
        headerAlign: 'right',
        renderHeader: () => <span className="visually-hidden">Actions</span>,
        renderCell: ({ row }) => (
          <UserActions
            user={row}
            onChangeRole={() => setChangingRole(row)}
            onResetPassword={() => setResetting(row)}
            onSetActive={(active) => (active ? setActive(row, true) : setDeactivating(row))}
          />
        ),
      },
    ],
    [me.id, setActive],
  );

  return (
    <>
      <PageHeader
        title="Users"
        subtitle="People who can sign in to FlagForge, and what they can change."
        actions={
          <Button variant="contained" startIcon={<AddRounded />} onClick={() => setCreating(true)}>
            Add user
          </Button>
        }
      />
      {users.error && <ErrorState error={users.error} onRetry={() => void users.refetch()} />}
      <DataGrid
        aria-label="Users"
        rows={users.data?.items ?? []}
        columns={columns}
        loading={users.isFetching}
        rowCount={users.data?.totalCount ?? 0}
        paginationMode="server"
        paginationModel={pagination}
        onPaginationModelChange={setPagination}
        pageSizeOptions={[25, 50, 100]}
        rowHeight={60}
        autoHeight
        disableColumnMenu
        disableRowSelectionOnClick
        slotProps={{ loadingOverlay: { variant: 'linear-progress', noRowsVariant: 'skeleton' } }}
      />
      <CreateUserDialog
        open={creating}
        onClose={() => setCreating(false)}
        onCreated={(created) => {
          setCreating(false);
          setPassword({ person: created.user, value: created.temporaryPassword });
        }}
      />
      <ChangeRoleDialog user={changingRole} onClose={() => setChangingRole(null)} />
      <ConfirmDialog
        open={resetting !== null}
        title={`Reset the password for ${resetting?.displayName ?? 'this user'}?`}
        description="They are signed out everywhere and must sign in with a temporary password, then choose a new one."
        confirmLabel="Reset password"
        pending={resetPassword.isPending}
        onConfirm={() =>
          resetting &&
          resetPassword.mutate(resetting.id, {
            onSuccess: (result) => {
              setPassword({ person: resetting, value: result.temporaryPassword });
              setResetting(null);
            },
            onError: (error) => notify(errorMessage(error), 'error'),
          })
        }
        onClose={() => setResetting(null)}
      />
      <ConfirmDialog
        open={deactivating !== null}
        title={`Deactivate ${deactivating?.displayName ?? 'this user'}?`}
        description="They are signed out and can no longer sign in. Their history stays in the audit log. You can reactivate them later."
        confirmLabel="Deactivate"
        destructive
        pending={updating}
        onConfirm={() => deactivating && setActive(deactivating, false)}
        onClose={() => setDeactivating(null)}
      />
      <TemporaryPasswordDialog
        person={password?.person ?? null}
        password={password?.value ?? ''}
        onClose={() => setPassword(null)}
      />
      <Typography variant="body2" color="text.secondary" sx={{ mt: 2 }}>
        FlagForge does not send email. Share temporary passwords with people directly.
      </Typography>
    </>
  );
}
