import { zodResolver } from '@hookform/resolvers/zod';
import {
  Alert,
  Box,
  Button,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
} from '@mui/material';
import { useMemo } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { z } from 'zod';
import { ApiError, errorMessage } from '../../api/errors';
import type { Environment, Flag } from '../../api/types';
import { useCurrentUser } from '../../auth/authContext';
import { Section } from '../../components/Section';
import { useNotify } from '../../components/notify';
import { useReplaceVariations } from '../flags/flagsApi';
import { canEditFlags } from '../flags/permissions';
import {
  displayValue,
  toVariationInputs,
  toVariationRows,
  type VariationRow,
} from '../flags/variationValues';
import { environmentsUsing } from './references';
import { VariationRowsEditor } from './VariationRowsEditor';
import {
  addVariationIssues,
  variationErrorReader,
  variationFieldName,
  variationRowSchema,
} from './variationSchema';

export interface VariationsTabProps {
  projectKey: string;
  flag: Flag;
  environments: readonly Environment[];
}

export function VariationsTab({ projectKey, flag, environments }: VariationsTabProps) {
  if (flag.type === 'boolean') {
    return (
      <Section
        title="Variations"
        description="Boolean flags always serve true or false, so their variations cannot change."
      >
        <Table size="small" aria-label="Variations">
          <TableHead>
            <TableRow>
              <TableCell>Name</TableCell>
              <TableCell>Value</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {flag.variations.map((variation) => (
              <TableRow key={variation.id}>
                <TableCell>{variation.name}</TableCell>
                <TableCell className="mono">{displayValue(variation.value)}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Section>
    );
  }

  // Remount the form when the saved variations change, so it always starts from the server's list.
  return (
    <VariationsForm
      key={JSON.stringify(flag.variations)}
      projectKey={projectKey}
      flag={flag}
      environments={environments}
    />
  );
}

type VariationsForm = { variations: VariationRow[] };

function VariationsForm({ projectKey, flag, environments }: VariationsTabProps) {
  const user = useCurrentUser();
  const notify = useNotify();
  const replace = useReplaceVariations(projectKey, flag.key);
  const canEdit = canEditFlags(user.role) && !flag.isArchived;
  const schema = useMemo(
    () =>
      z
        .object({ variations: z.array(variationRowSchema) })
        .superRefine((form, ctx) => addVariationIssues(ctx, flag.type, form.variations)),
    [flag.type],
  );
  const environmentNames = useMemo(
    () => new Map(environments.map((environment) => [environment.key, environment.name])),
    [environments],
  );
  const {
    control,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isDirty },
  } = useForm<VariationsForm>({
    resolver: zodResolver(schema),
    defaultValues: { variations: toVariationRows(flag.type, flag.variations) },
  });

  const onSubmit = handleSubmit((form) =>
    replace.mutate(toVariationInputs(flag.type, form.variations), {
      onSuccess: () => notify('Variations saved'),
      onError: (error) => {
        if (error instanceof ApiError) {
          for (const [path, messages] of Object.entries(error.errors)) {
            const field = variationFieldName(path);
            if (field && messages[0]) {
              setError(field, { message: messages[0] });
            }
          }
        }
      },
    }),
  );

  const removeBlockedReason = (row: VariationRow) => {
    if (!row.id) {
      return null;
    }

    const usedIn = environmentsUsing(flag, environmentNames, row.id);
    return usedIn.length > 0
      ? `${row.name} is still served in ${usedIn.join(', ')}. Change the targeting there first.`
      : null;
  };

  return (
    <Section
      title="Variations"
      description="Changing a value updates every environment that serves it as soon as you save."
    >
      <Stack component="form" noValidate spacing={2} onSubmit={(event) => void onSubmit(event)}>
        {!canEdit && (
          <Alert severity="info">
            {flag.isArchived
              ? 'This flag is archived. Restore it before changing its variations.'
              : 'Viewers cannot change variations.'}
          </Alert>
        )}
        {replace.isError &&
          !(replace.error instanceof ApiError && Object.keys(replace.error.errors).length > 0) && (
            <Alert severity="error">{errorMessage(replace.error)}</Alert>
          )}
        <Controller
          control={control}
          name="variations"
          render={({ field }) => (
            <VariationRowsEditor
              type={flag.type}
              rows={field.value}
              onChange={field.onChange}
              errorFor={variationErrorReader(errors.variations)}
              listError={errors.variations?.root?.message}
              disabled={!canEdit}
              removeBlockedReason={removeBlockedReason}
            />
          )}
        />
        {canEdit && (
          <Box sx={{ display: 'flex', gap: 1 }}>
            <Button
              type="submit"
              variant="contained"
              disabled={!isDirty}
              loading={replace.isPending}
            >
              Save changes
            </Button>
            <Button disabled={!isDirty || replace.isPending} onClick={() => reset()}>
              Discard
            </Button>
          </Box>
        )}
      </Stack>
    </Section>
  );
}
