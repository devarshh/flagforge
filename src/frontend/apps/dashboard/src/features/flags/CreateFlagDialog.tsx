import { zodResolver } from '@hookform/resolvers/zod';
import {
  Alert,
  Button,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  MenuItem,
  Stack,
  Switch,
  TextField,
  Typography,
} from '@mui/material';
import { Controller, useForm, useWatch } from 'react-hook-form';
import { useNavigate } from 'react-router';
import { z } from 'zod';
import { ApiError, errorMessage } from '../../api/errors';
import type { FlagType } from '../../api/types';
import { ChipInput } from '../../components/ChipInput';
import { useNotify } from '../../components/notify';
import { keyPattern, keyRequirement, suggestKey } from '../projects/keys';
import { VariationRowsEditor } from '../variations/VariationRowsEditor';
import {
  addVariationIssues,
  variationErrorReader,
  variationFieldName,
  variationRowSchema,
} from '../variations/variationSchema';
import { useCreateFlag } from './flagsApi';
import { tagsSchema } from './tags';
import { defaultVariationRows, flagTypeLabels, toVariationInputs } from './variationValues';

const flagTypes: FlagType[] = ['boolean', 'string', 'number', 'json'];

const schema = z
  .object({
    name: z
      .string()
      .trim()
      .min(1, 'Enter a name.')
      .max(100, 'Names can be at most 100 characters.'),
    key: z.string().regex(keyPattern, keyRequirement),
    description: z.string().max(1000, 'Descriptions can be at most 1000 characters.'),
    type: z.enum(['boolean', 'string', 'number', 'json']),
    variations: z.array(variationRowSchema),
    tags: tagsSchema,
    isPermanent: z.boolean(),
  })
  .superRefine((form, ctx) => {
    if (form.type !== 'boolean') {
      addVariationIssues(ctx, form.type, form.variations);
    }
  });

type CreateFlagForm = z.infer<typeof schema>;

const defaults: CreateFlagForm = {
  name: '',
  key: '',
  description: '',
  type: 'boolean',
  variations: [],
  tags: [],
  isPermanent: false,
};

export interface CreateFlagDialogProps {
  projectKey: string;
  open: boolean;
  onClose: () => void;
  /** Tags already used in the project, offered as suggestions. */
  knownTags?: readonly string[];
}

export function CreateFlagDialog({
  projectKey,
  open,
  onClose,
  knownTags = [],
}: CreateFlagDialogProps) {
  const createFlag = useCreateFlag(projectKey);
  const navigate = useNavigate();
  const notify = useNotify();
  const {
    register,
    control,
    handleSubmit,
    setValue,
    setError,
    getFieldState,
    reset,
    formState: { errors },
  } = useForm<CreateFlagForm>({ resolver: zodResolver(schema), defaultValues: defaults });
  const keyValue = useWatch({ control, name: 'key' });
  const type = useWatch({ control, name: 'type' });

  const close = () => {
    reset(defaults);
    createFlag.reset();
    onClose();
  };

  const onSubmit = handleSubmit((form) =>
    createFlag.mutate(
      {
        key: form.key,
        name: form.name.trim(),
        description: form.description.trim() || undefined,
        type: form.type,
        variations:
          form.type === 'boolean' ? undefined : toVariationInputs(form.type, form.variations),
        tags: form.tags,
        isPermanent: form.isPermanent,
      },
      {
        onSuccess: (flag) => {
          notify('Flag created');
          close();
          void navigate(`/projects/${projectKey}/flags/${flag.key}`);
        },
        onError: (error) => {
          if (!(error instanceof ApiError)) {
            return;
          }

          if (error.status === 409) {
            setError('key', { message: error.detail ?? error.title });
          }

          for (const [path, messages] of Object.entries(error.errors)) {
            const message = messages[0] ?? error.title;
            const variationField = variationFieldName(path);
            if (variationField) {
              setError(variationField, { message });
            } else if (path === 'key' || path === 'name' || path === 'description') {
              setError(path, { message });
            } else if (path.startsWith('tags')) {
              setError('tags', { message });
            }
          }
        },
      },
    ),
  );

  const nameField = register('name');
  const showGeneralError =
    createFlag.isError &&
    !(
      createFlag.error instanceof ApiError &&
      (createFlag.error.status === 409 || Object.keys(createFlag.error.errors).length > 0)
    );
  return (
    <Dialog open={open} onClose={close} maxWidth="md" fullWidth aria-labelledby="create-flag-title">
      <form noValidate onSubmit={(event) => void onSubmit(event)}>
        <DialogTitle id="create-flag-title">Create flag</DialogTitle>
        <DialogContent>
          <Stack spacing={2.5} sx={{ pt: 1 }}>
            {showGeneralError && <Alert severity="error">{errorMessage(createFlag.error)}</Alert>}
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
              <TextField
                label="Name"
                autoFocus
                fullWidth
                {...nameField}
                onChange={(event) => {
                  void nameField.onChange(event);
                  if (!getFieldState('key').isDirty) {
                    setValue('key', suggestKey(event.target.value));
                  }
                }}
                error={Boolean(errors.name)}
                helperText={errors.name?.message}
              />
              <TextField
                label="Key"
                fullWidth
                {...register('key')}
                value={keyValue}
                error={Boolean(errors.key)}
                helperText={
                  errors.key?.message ?? 'Your code uses the key. It cannot change later.'
                }
                slotProps={{ htmlInput: { className: 'mono', spellCheck: false } }}
              />
            </Stack>
            <TextField
              label="Description"
              multiline
              minRows={2}
              {...register('description')}
              error={Boolean(errors.description)}
              helperText={errors.description?.message}
            />
            <Controller
              control={control}
              name="type"
              render={({ field }) => (
                <TextField
                  select
                  label="Type"
                  value={field.value}
                  onChange={(event) => {
                    const next = event.target.value as FlagType;
                    field.onChange(next);
                    setValue('variations', defaultVariationRows(next));
                  }}
                  helperText={
                    type === 'boolean'
                      ? 'Serves true or false.'
                      : 'Define the values this flag can serve.'
                  }
                  sx={{ maxWidth: { sm: 240 } }}
                >
                  {flagTypes.map((option) => (
                    <MenuItem key={option} value={option}>
                      {flagTypeLabels[option]}
                    </MenuItem>
                  ))}
                </TextField>
              )}
            />
            {type !== 'boolean' && (
              <Stack spacing={1}>
                <Typography variant="subtitle2" component="h3">
                  Variations
                </Typography>
                <Controller
                  control={control}
                  name="variations"
                  render={({ field }) => (
                    <VariationRowsEditor
                      type={type}
                      rows={field.value}
                      onChange={field.onChange}
                      errorFor={variationErrorReader(errors.variations)}
                      listError={errors.variations?.root?.message}
                    />
                  )}
                />
              </Stack>
            )}
            <Controller
              control={control}
              name="tags"
              render={({ field }) => (
                <ChipInput
                  label="Tags"
                  value={field.value}
                  onChange={field.onChange}
                  options={knownTags}
                  placeholder="checkout, experiment"
                  error={Boolean(errors.tags)}
                  helperText={
                    errors.tags?.message ?? 'Up to 10 tags. Press Enter or type a comma to add one.'
                  }
                />
              )}
            />
            <Controller
              control={control}
              name="isPermanent"
              render={({ field }) => (
                <FormControlLabel
                  control={
                    <Switch
                      checked={field.value}
                      onChange={(event) => field.onChange(event.target.checked)}
                    />
                  }
                  label={
                    <span>
                      Permanent flag
                      <Typography
                        component="span"
                        variant="body2"
                        color="text.secondary"
                        sx={{ display: 'block' }}
                      >
                        For long-lived switches such as kill switches. Permanent flags are never
                        reported as stale.
                      </Typography>
                    </span>
                  }
                />
              )}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={close}>Cancel</Button>
          <Button type="submit" variant="contained" loading={createFlag.isPending}>
            Create flag
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
