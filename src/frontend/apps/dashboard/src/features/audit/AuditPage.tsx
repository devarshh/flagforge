import {
  Autocomplete,
  Box,
  Button,
  Drawer,
  IconButton,
  MenuItem,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import CloseRounded from '@mui/icons-material/CloseRounded';
import VisibilityOutlined from '@mui/icons-material/VisibilityOutlined';
import { DataGrid, type GridColDef, type GridRowParams } from '@mui/x-data-grid';
import { DatePicker } from '@mui/x-date-pickers/DatePicker';
import type { Dayjs } from 'dayjs';
import { useEffect, useMemo, useRef, useState } from 'react';
import type { AuditEntry } from '../../api/types';
import { hasRole, useCurrentUser } from '../../auth/authContext';
import { ErrorState } from '../../components/ErrorState';
import { PageHeader } from '../../components/PageHeader';
import { RelativeTime } from '../../components/RelativeTime';
import { useProjects } from '../projects/projectsApi';
import { useUsers } from '../users/usersApi';
import { auditActionLabel, auditActionLabels } from './actions';
import { AuditEntryDetails } from './AuditEntryDetails';
import { useAuditLog, type AuditParams } from './auditApi';

interface Actor {
  id: string;
  name: string;
}

interface Filters {
  projectKey: string;
  flagKey: string;
  action: string;
  actor: Actor | null;
  from: Dayjs | null;
  to: Dayjs | null;
}

const emptyFilters: Filters = {
  projectKey: '',
  flagKey: '',
  action: '',
  actor: null,
  from: null,
  to: null,
};

/**
 * The global audit log. The free Data Grid has no expandable detail rows, so an entry's before-and-after diff opens
 * in a side panel (click the row or its view button).
 */
export function AuditPage() {
  const user = useCurrentUser();
  const projects = useProjects();
  const admin = hasRole(user, 'admin');
  const users = useUsers({ page: 1, pageSize: 100 }, admin);
  const [filters, setFilters] = useState<Filters>(emptyFilters);
  const [flagText, setFlagText] = useState('');
  const [pagination, setPagination] = useState({ page: 0, pageSize: 25 });
  const [selected, setSelected] = useState<AuditEntry | null>(null);
  const flagTimer = useRef<ReturnType<typeof setTimeout>>(undefined);

  useEffect(() => () => clearTimeout(flagTimer.current), []);

  const updateFilters = (changes: Partial<Filters>) => {
    setFilters((current) => ({ ...current, ...changes }));
    setPagination((current) => ({ ...current, page: 0 }));
  };

  const params: AuditParams = {
    projectKey: filters.projectKey || undefined,
    flagKey: filters.projectKey && filters.flagKey ? filters.flagKey : undefined,
    action: filters.action || undefined,
    actorId: filters.actor?.id,
    from: filters.from?.isValid() ? filters.from.startOf('day').toISOString() : undefined,
    to: filters.to?.isValid() ? filters.to.add(1, 'day').startOf('day').toISOString() : undefined,
    page: pagination.page + 1,
    pageSize: pagination.pageSize,
  };
  const audit = useAuditLog(params);

  // Actors to filter by: every user (admins can list them) plus anyone on the current page.
  const actors = useMemo(() => {
    const known = new Map<string, string>();
    for (const item of users.data?.items ?? []) known.set(item.id, item.displayName);
    for (const entry of audit.data?.items ?? [])
      if (entry.actorId) known.set(entry.actorId, entry.actorName);
    return [...known.entries()]
      .map(([id, name]) => ({ id, name }))
      .sort((left, right) => left.name.localeCompare(right.name));
  }, [users.data, audit.data]);

  const columns = useMemo<GridColDef<AuditEntry>[]>(
    () => [
      {
        field: 'occurredAt',
        headerName: 'When',
        width: 140,
        sortable: false,
        display: 'flex',
        renderCell: ({ row }) => <RelativeTime value={row.occurredAt} />,
      },
      {
        field: 'actorName',
        headerName: 'Who',
        width: 170,
        sortable: false,
        valueGetter: (_value, row) =>
          row.actorType === 'system' ? `${row.actorName} (system)` : row.actorName,
      },
      {
        field: 'action',
        headerName: 'Action',
        width: 210,
        sortable: false,
        valueFormatter: (value: string) => auditActionLabel(value),
      },
      {
        field: 'resourceKey',
        headerName: 'Resource',
        flex: 1,
        minWidth: 200,
        sortable: false,
        display: 'flex',
        renderCell: ({ row }) => (
          <Typography className="mono" variant="body2" noWrap>
            {row.resourceKey}
          </Typography>
        ),
      },
      {
        field: 'comment',
        headerName: 'Comment',
        flex: 1,
        minWidth: 160,
        sortable: false,
        valueFormatter: (value: string | null) => value ?? '',
      },
      {
        field: 'details',
        headerName: 'Details',
        width: 72,
        sortable: false,
        display: 'flex',
        renderHeader: () => <span className="visually-hidden">Details</span>,
        renderCell: ({ row }) => (
          <Tooltip title="View changes">
            <IconButton
              size="small"
              aria-label={`View changes: ${auditActionLabel(row.action)} by ${row.actorName}`}
              onClick={() => setSelected(row)}
            >
              <VisibilityOutlined fontSize="small" />
            </IconButton>
          </Tooltip>
        ),
      },
    ],
    [],
  );

  const filtered = JSON.stringify(filters) !== JSON.stringify(emptyFilters);
  return (
    <>
      <PageHeader
        title="Audit log"
        subtitle="Every change in FlagForge: who made it, when, and what changed."
      />
      <Stack spacing={2}>
        <Box
          sx={{
            display: 'grid',
            gap: 1.5,
            gridTemplateColumns: {
              xs: '1fr',
              sm: 'repeat(2, minmax(0, 1fr))',
              lg: 'repeat(6, minmax(0, 1fr))',
            },
            alignItems: 'start',
          }}
          role="search"
          aria-label="Filter the audit log"
        >
          <TextField
            select
            label="Project"
            value={filters.projectKey}
            onChange={(event) => {
              setFlagText('');
              updateFilters({ projectKey: event.target.value, flagKey: '' });
            }}
          >
            <MenuItem value="">All projects</MenuItem>
            {(projects.data ?? []).map((project) => (
              <MenuItem key={project.key} value={project.key}>
                {project.name}
              </MenuItem>
            ))}
          </TextField>
          <Tooltip title={filters.projectKey ? '' : 'Choose a project to filter by flag.'}>
            <TextField
              label="Flag key"
              value={flagText}
              disabled={!filters.projectKey}
              onChange={(event) => {
                const value = event.target.value;
                setFlagText(value);
                clearTimeout(flagTimer.current);
                flagTimer.current = setTimeout(() => updateFilters({ flagKey: value.trim() }), 300);
              }}
              slotProps={{ htmlInput: { className: 'mono', spellCheck: false } }}
            />
          </Tooltip>
          <TextField
            select
            label="Action"
            value={filters.action}
            onChange={(event) => updateFilters({ action: event.target.value })}
          >
            <MenuItem value="">All actions</MenuItem>
            {Object.entries(auditActionLabels).map(([action, label]) => (
              <MenuItem key={action} value={action}>
                {label}
              </MenuItem>
            ))}
          </TextField>
          <Autocomplete
            options={actors}
            value={filters.actor}
            onChange={(_, actor) => updateFilters({ actor })}
            getOptionLabel={(actor) => actor.name}
            isOptionEqualToValue={(option, value) => option.id === value.id}
            renderInput={(params) => <TextField {...params} label="Who" />}
          />
          <DatePicker
            label="From"
            value={filters.from}
            onChange={(from) => updateFilters({ from })}
            disableFuture
          />
          <DatePicker
            label="To"
            value={filters.to}
            onChange={(to) => updateFilters({ to })}
            disableFuture
          />
        </Box>
        {filtered && (
          <Box>
            <Button
              size="small"
              onClick={() => {
                setFlagText('');
                updateFilters(emptyFilters);
              }}
            >
              Clear filters
            </Button>
          </Box>
        )}
        {audit.error && <ErrorState error={audit.error} onRetry={() => void audit.refetch()} />}
        <DataGrid
          aria-label="Audit entries"
          rows={audit.data?.items ?? []}
          columns={columns}
          loading={audit.isFetching}
          rowCount={audit.data?.totalCount ?? 0}
          paginationMode="server"
          paginationModel={pagination}
          onPaginationModelChange={setPagination}
          pageSizeOptions={[25, 50, 100]}
          autoHeight
          disableColumnMenu
          disableRowSelectionOnClick
          onRowClick={(params: GridRowParams<AuditEntry>) => setSelected(params.row)}
          localeText={{
            noRowsLabel: filtered
              ? 'No entries match these filters.'
              : 'Nothing has been recorded yet.',
          }}
          slotProps={{ loadingOverlay: { variant: 'linear-progress', noRowsVariant: 'skeleton' } }}
          sx={{ '& .MuiDataGrid-row': { cursor: 'pointer' } }}
        />
      </Stack>
      <Drawer
        anchor="right"
        open={selected !== null}
        sx={{ zIndex: (theme) => theme.zIndex.modal }}
        onClose={() => setSelected(null)}
        slotProps={{ paper: { sx: { width: { xs: '100%', sm: 560 } } } }}
      >
        {selected && (
          <Stack
            spacing={2}
            sx={{ p: 2.5 }}
            component="section"
            aria-labelledby="audit-entry-title"
          >
            <Stack direction="row" sx={{ alignItems: 'center' }}>
              <Typography id="audit-entry-title" variant="h3" component="h2" sx={{ flexGrow: 1 }}>
                {auditActionLabel(selected.action)}
              </Typography>
              <IconButton aria-label="Close details" onClick={() => setSelected(null)}>
                <CloseRounded />
              </IconButton>
            </Stack>
            <AuditEntryDetails entry={selected} />
          </Stack>
        )}
      </Drawer>
    </>
  );
}
