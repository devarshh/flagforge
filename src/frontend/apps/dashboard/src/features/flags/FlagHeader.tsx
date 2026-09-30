import { Box, Chip, Link, Stack, Typography } from '@mui/material';
import ChevronLeftRounded from '@mui/icons-material/ChevronLeftRounded';
import { Link as RouterLink } from 'react-router';
import type { Environment, Flag } from '../../api/types';
import { useCurrentUser } from '../../auth/authContext';
import { CopyButton } from '../../components/CopyButton';
import { EnvironmentChip } from '../../components/EnvironmentChip';
import { ToneChip } from '../../components/ToneChip';
import { ArchiveFlagButton } from './ArchiveFlagButton';
import { FlagToggle } from './FlagToggle';
import { canEditFlags } from './permissions';
import { flagTypeLabels } from './variationValues';

export interface FlagHeaderProps {
  projectKey: string;
  flag: Flag;
  environments: readonly Environment[];
}

/** Name, key, type, tags, archive state, and the signal strip: one lamp per environment. */
export function FlagHeader({ projectKey, flag, environments }: FlagHeaderProps) {
  const user = useCurrentUser();
  return (
    <Stack spacing={2} sx={{ mb: 3 }}>
      <Link
        component={RouterLink}
        to={`/projects/${projectKey}/flags`}
        underline="hover"
        sx={{ display: 'inline-flex', alignItems: 'center', alignSelf: 'flex-start' }}
      >
        <ChevronLeftRounded fontSize="small" />
        Flags
      </Link>
      <Stack
        direction={{ xs: 'column', md: 'row' }}
        spacing={2}
        sx={{ justifyContent: 'space-between', alignItems: { md: 'flex-start' } }}
      >
        <Box sx={{ minWidth: 0 }}>
          <Typography variant="h2" component="h1" sx={{ wordBreak: 'break-word' }}>
            {flag.name}
          </Typography>
          <Stack
            direction="row"
            sx={{ alignItems: 'center', flexWrap: 'wrap', columnGap: 1, rowGap: 0.5, mt: 0.5 }}
          >
            <Stack direction="row" sx={{ alignItems: 'center' }}>
              <Typography className="mono" color="text.secondary">
                {flag.key}
              </Typography>
              <CopyButton value={flag.key} label="Copy flag key" />
            </Stack>
            <ToneChip label={flagTypeLabels[flag.type]} tone="neutral" />
            {flag.isArchived && <ToneChip label="Archived" tone="neutral" />}
            {flag.isPermanent && <ToneChip label="Permanent" tone="primary" />}
            {flag.tags.map((tag) => (
              <Chip key={tag} size="small" variant="outlined" label={tag} />
            ))}
          </Stack>
          {flag.description && (
            <Typography color="text.secondary" sx={{ mt: 1, maxWidth: 720 }}>
              {flag.description}
            </Typography>
          )}
        </Box>
        {canEditFlags(user.role) && <ArchiveFlagButton projectKey={projectKey} flag={flag} />}
      </Stack>
      <Stack
        direction="row"
        component="ul"
        aria-label="State in each environment"
        sx={{ listStyle: 'none', p: 0, m: 0, flexWrap: 'wrap', gap: 1.5 }}
      >
        {environments.map((environment) => {
          const state = flag.environments.find((item) => item.environmentKey === environment.key);
          return (
            <Stack
              component="li"
              key={environment.key}
              direction="row"
              spacing={1}
              sx={{
                alignItems: 'center',
                px: 1.25,
                py: 0.75,
                border: 1,
                borderColor: 'divider',
                borderRadius: 2,
                bgcolor: 'background.paper',
              }}
            >
              <EnvironmentChip
                name={environment.name}
                color={environment.color}
                isProtected={environment.isProtected}
              />
              {state && (
                <FlagToggle
                  projectKey={projectKey}
                  flagKey={flag.key}
                  environment={environment}
                  enabled={state.config.enabled}
                  archived={flag.isArchived}
                />
              )}
            </Stack>
          );
        })}
      </Stack>
    </Stack>
  );
}
