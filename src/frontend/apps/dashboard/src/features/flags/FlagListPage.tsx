import {
  Box,
  Button,
  Chip,
  FormControlLabel,
  InputAdornment,
  Link,
  Stack,
  Switch,
  TextField,
  Typography,
} from '@mui/material';
import AddRounded from '@mui/icons-material/AddRounded';
import SearchRounded from '@mui/icons-material/SearchRounded';
import { DataGrid, type GridColDef } from '@mui/x-data-grid';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Link as RouterLink, useParams } from 'react-router';
import type { FlagSummary } from '../../api/types';
import { useCurrentUser } from '../../auth/authContext';
import { CopyButton } from '../../components/CopyButton';
import { EmptyState } from '../../components/EmptyState';
import { EnvironmentChip } from '../../components/EnvironmentChip';
import { ErrorState } from '../../components/ErrorState';
import { PageHeader } from '../../components/PageHeader';
import { RelativeTime } from '../../components/RelativeTime';
import { ToneChip } from '../../components/ToneChip';
import { sortEnvironments } from '../environments/order';
import { useProject } from '../projects/projectsApi';
import { CreateFlagDialog } from './CreateFlagDialog';
import { FlagToggle } from './FlagToggle';
import { useFlagList } from './flagsApi';
import { canEditFlags } from './permissions';
import { pageSizeOptions, useListParams } from './useListParams';
import { flagTypeLabels } from './variationValues';

const searchDebounceMs = 300;

function latest(values: (string | null)[]): string | null {
  return values.reduce<string | null>(
    (max, value) => (value !== null && (max === null || value > max) ? value : max),
    null,
  );
}

function NoMatchesOverlay() {
  return (
    <Box sx={{ display: 'grid', placeItems: 'center', height: '100%', p: 3 }}>
      <Typography color="text.secondary">
        No flags match these filters. Change the search or clear the tag.
      </Typography>
    </Box>
  );
}

export function FlagListPage() {
  const { projectKey = '' } = useParams();
  const user = useCurrentUser();
  const [params, update] = useListParams();
  const project = useProject(projectKey);
  const flags = useFlagList(projectKey, params);
  const [creating, setCreating] = useState(false);
  const [searchText, setSearchText] = useState(params.search);
  const searchInput = useRef<HTMLInputElement>(null);
  const searchTimer = useRef<ReturnType<typeof setTimeout>>(undefined);
  const canCreate = canEditFlags(user.role);

  useEffect(() => () => clearTimeout(searchTimer.current), []);

  // Pressing "/" anywhere outside a text field jumps to search.
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== '/' || event.metaKey || event.ctrlKey || event.altKey) {
        return;
      }

      const target = event.target as HTMLElement | null;
      if (
        target &&
        (target.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName))
      ) {
        return;
      }

      event.preventDefault();
      searchInput.current?.focus();
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, []);

  const onSearchChange = (value: string) => {
    setSearchText(value);
    clearTimeout(searchTimer.current);
    searchTimer.current = setTimeout(() => update({ search: value.trim() }), searchDebounceMs);
  };

  const environments = useMemo(
    () => sortEnvironments(project.data?.environments ?? []),
    [project.data],
  );
  const pageTags = useMemo(
    () =>
      [...new Set((flags.data?.items ?? []).flatMap((flag) => flag.tags))].sort((left, right) =>
        left.localeCompare(right),
      ),
    [flags.data],
  );

  const columns = useMemo<GridColDef<FlagSummary>[]>(
    () => [
      {
        field: 'name',
        headerName: 'Flag',
        flex: 1.6,
        minWidth: 220,
        sortable: false,
        display: 'flex',
        renderCell: ({ row }) => (
          <Box sx={{ minWidth: 0 }}>
            <Link
              component={RouterLink}
              to={`/projects/${projectKey}/flags/${row.key}`}
              underline="hover"
              noWrap
              sx={{ display: 'block', fontWeight: 500 }}
            >
              {row.name}
            </Link>
            <Stack direction="row" sx={{ alignItems: 'center', minWidth: 0 }}>
              <Typography className="mono" variant="body2" color="text.secondary" noWrap>
                {row.key}
              </Typography>
              <CopyButton value={row.key} label={`Copy key ${row.key}`} />
            </Stack>
          </Box>
        ),
      },
      {
        field: 'type',
        headerName: 'Type',
        width: 88,
        sortable: false,
        valueFormatter: (value: FlagSummary['type']) => flagTypeLabels[value],
      },
      {
        field: 'tags',
        headerName: 'Tags',
        flex: 1,
        minWidth: 150,
        sortable: false,
        display: 'flex',
        renderCell: ({ row }) => (
          <Stack direction="row" sx={{ gap: 0.5, flexWrap: 'wrap', py: 1 }}>
            {row.tags.map((tag) => (
              <Chip
                key={tag}
                size="small"
                variant="outlined"
                label={tag}
                onClick={() => update({ tag })}
              />
            ))}
          </Stack>
        ),
      },
      ...environments.map<GridColDef<FlagSummary>>((environment) => ({
        field: `environment:${environment.key}`,
        headerName: environment.name,
        width: 120,
        sortable: false,
        display: 'flex',
        align: 'center',
        headerAlign: 'center',
        renderHeader: () => (
          <EnvironmentChip
            name={environment.name}
            color={environment.color}
            isProtected={environment.isProtected}
          />
        ),
        renderCell: ({ row }) => {
          const state = row.environments.find((item) => item.environmentKey === environment.key);
          return state ? (
            <FlagToggle
              projectKey={projectKey}
              flagKey={row.key}
              environment={environment}
              enabled={state.enabled}
              archived={row.isArchived}
            />
          ) : null;
        },
      })),
      {
        field: 'lastEvaluatedAt',
        headerName: 'Last evaluated',
        width: 130,
        sortable: false,
        display: 'flex',
        valueGetter: (_value, row) =>
          latest(row.environments.map((environment) => environment.lastEvaluatedAt)),
        renderCell: ({ value }) => <RelativeTime value={value as string | null} />,
      },
      {
        field: 'status',
        headerName: 'Status',
        width: 132,
        sortable: false,
        display: 'flex',
        renderCell: ({ row }) => (
          <Stack direction="row" spacing={0.5}>
            {row.isStale && <ToneChip label="Stale" tone="warning" />}
            {row.isArchived && <ToneChip label="Archived" tone="neutral" />}
          </Stack>
        ),
      },
    ],
    [environments, projectKey, update],
  );

  const createButton = canCreate && (
    <Button variant="contained" startIcon={<AddRounded />} onClick={() => setCreating(true)}>
      Create flag
    </Button>
  );
  const filtered = params.search !== '' || params.tag !== null || params.includeArchived;
  const projectIsEmpty = !filtered && flags.data?.totalCount === 0;

  return (
    <>
      <PageHeader title="Flags" subtitle={project.data?.name} actions={createButton} />
      {project.error && <ErrorState error={project.error} onRetry={() => void project.refetch()} />}
      {flags.error && <ErrorState error={flags.error} onRetry={() => void flags.refetch()} />}
      {projectIsEmpty ? (
        <EmptyState
          title="No flags yet"
          description={
            canCreate
              ? 'Create a flag to control a feature without deploying.'
              : 'No flags yet. Editors and admins can create flags to control features without deploying.'
          }
          action={createButton}
        />
      ) : (
        <Stack spacing={2}>
          <Stack
            direction={{ xs: 'column', md: 'row' }}
            spacing={2}
            sx={{ alignItems: { md: 'center' } }}
          >
            <TextField
              label="Search flags"
              value={searchText}
              onChange={(event) => onSearchChange(event.target.value)}
              inputRef={searchInput}
              placeholder="Name, key, or description"
              sx={{ width: { xs: '100%', md: 360 } }}
              slotProps={{
                input: {
                  startAdornment: (
                    <InputAdornment position="start">
                      <SearchRounded fontSize="small" />
                    </InputAdornment>
                  ),
                  endAdornment: (
                    <InputAdornment position="end">
                      <Box
                        component="kbd"
                        aria-hidden
                        sx={{
                          px: 0.75,
                          border: 1,
                          borderColor: 'divider',
                          borderRadius: 1,
                          fontSize: 12,
                          color: 'text.secondary',
                        }}
                      >
                        /
                      </Box>
                    </InputAdornment>
                  ),
                },
              }}
            />
            <FormControlLabel
              control={
                <Switch
                  checked={params.includeArchived}
                  onChange={(event) => update({ includeArchived: event.target.checked })}
                />
              }
              label="Show archived"
            />
          </Stack>
          {(params.tag !== null || pageTags.length > 0) && (
            <Stack
              direction="row"
              sx={{ gap: 0.75, flexWrap: 'wrap', alignItems: 'center' }}
              aria-label="Filter by tag"
              role="group"
            >
              <Typography variant="body2" color="text.secondary" sx={{ mr: 0.5 }}>
                Tags
              </Typography>
              {params.tag !== null && (
                <Chip
                  size="small"
                  color="primary"
                  label={params.tag}
                  onDelete={() => update({ tag: null })}
                  aria-label={`Tag filter ${params.tag}. Remove`}
                />
              )}
              {pageTags
                .filter((tag) => tag !== params.tag)
                .map((tag) => (
                  <Chip
                    key={tag}
                    size="small"
                    variant="outlined"
                    label={tag}
                    onClick={() => update({ tag })}
                  />
                ))}
            </Stack>
          )}
          <DataGrid
            aria-label="Flags"
            rows={flags.data?.items ?? []}
            columns={columns}
            loading={flags.isFetching}
            rowCount={flags.data?.totalCount ?? 0}
            paginationMode="server"
            paginationModel={{ page: params.page - 1, pageSize: params.pageSize }}
            onPaginationModelChange={(model) =>
              update({ page: model.page + 1, pageSize: model.pageSize })
            }
            pageSizeOptions={pageSizeOptions}
            rowHeight={64}
            autoHeight
            disableColumnMenu
            disableRowSelectionOnClick
            slots={{ noRowsOverlay: NoMatchesOverlay }}
            slotProps={{
              loadingOverlay: { variant: 'linear-progress', noRowsVariant: 'skeleton' },
            }}
          />
        </Stack>
      )}
      <CreateFlagDialog
        projectKey={projectKey}
        open={creating}
        onClose={() => setCreating(false)}
        knownTags={pageTags}
      />
    </>
  );
}
