import {
  Box,
  Button,
  Chip,
  IconButton,
  Paper,
  Stack,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import AddRounded from '@mui/icons-material/AddRounded';
import ArrowDownwardRounded from '@mui/icons-material/ArrowDownwardRounded';
import ArrowUpwardRounded from '@mui/icons-material/ArrowUpwardRounded';
import ContentCopyRounded from '@mui/icons-material/ContentCopyRounded';
import DeleteOutlineRounded from '@mui/icons-material/DeleteOutlineRounded';
import type { Dispatch, ReactNode } from 'react';
import type { Variation } from '../../api/types';
import { ClauseRow } from './ClauseRow';
import { createClause, duplicateRuleAction, type DraftAction, type DraftRule } from './draft';
import { ServeEditor } from './ServeEditor';
import { errorAt, errorUnder, type DraftErrors } from './validation';

export interface RulePanelProps {
  rule: DraftRule;
  index: number;
  count: number;
  variations: readonly Variation[];
  errors: DraftErrors;
  disabled: boolean;
  /** The test panel's last evaluation matched this rule. */
  highlighted: boolean;
  dispatch: Dispatch<DraftAction>;
}

const maxClauses = 10;

export function RulePanel({
  rule,
  index,
  count,
  variations,
  errors,
  disabled,
  highlighted,
  dispatch,
}: RulePanelProps) {
  const path = `rules[${index}]`;
  const hasErrors = errorUnder(errors, path) !== undefined;
  const title = `Rule ${index + 1}`;
  const clausesError = errorAt(errors, `${path}.clauses`);
  const action = (label: string, icon: ReactNode, onClick: () => void, enabled = true) => (
    <Tooltip title={label}>
      <span>
        <IconButton
          aria-label={`${label}: ${title}`}
          disabled={disabled || !enabled}
          onClick={onClick}
          size="small"
        >
          {icon}
        </IconButton>
      </span>
    </Tooltip>
  );

  return (
    <Paper
      variant="outlined"
      component="section"
      aria-label={title}
      sx={{
        p: 2,
        borderRadius: 2,
        borderWidth: highlighted ? 2 : 1,
        borderColor: highlighted ? 'primary.main' : hasErrors ? 'error.main' : 'divider',
      }}
    >
      <Stack spacing={2}>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
          <Typography variant="h4" component="h3" sx={{ whiteSpace: 'nowrap' }}>
            {title}
          </Typography>
          {highlighted && <Chip size="small" color="primary" label="Matched in test" />}
          <Box sx={{ flexGrow: 1 }} />
          {action(
            'Move up',
            <ArrowUpwardRounded fontSize="small" />,
            () => dispatch({ type: 'moveRule', ruleId: rule.id, offset: -1 }),
            index > 0,
          )}
          {action(
            'Move down',
            <ArrowDownwardRounded fontSize="small" />,
            () => dispatch({ type: 'moveRule', ruleId: rule.id, offset: 1 }),
            index < count - 1,
          )}
          {action('Duplicate', <ContentCopyRounded fontSize="small" />, () =>
            dispatch(duplicateRuleAction(rule)),
          )}
          {action('Delete', <DeleteOutlineRounded fontSize="small" />, () =>
            dispatch({ type: 'removeRule', ruleId: rule.id }),
          )}
        </Stack>
        <TextField
          label="Description (optional)"
          value={rule.description}
          disabled={disabled}
          onChange={(event) =>
            dispatch({
              type: 'setRuleDescription',
              ruleId: rule.id,
              description: event.target.value,
            })
          }
          error={
            Boolean(errorAt(errors, `${path}.description`)) ||
            Boolean(errorAt(errors, `${path}.id`))
          }
          helperText={errorAt(errors, `${path}.description`) ?? errorAt(errors, `${path}.id`)}
          placeholder="For example: Enterprise customers"
        />
        <Stack spacing={1.5}>
          <Typography variant="subtitle2" component="h4">
            If every condition matches
          </Typography>
          {rule.clauses.map((clause, clauseIndex) => (
            <Box key={clause.uid}>
              {clauseIndex > 0 && (
                <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
                  and
                </Typography>
              )}
              <ClauseRow
                clause={clause}
                index={clauseIndex}
                path={`${path}.clauses[${clauseIndex}]`}
                errors={errors}
                disabled={disabled}
                canRemove={rule.clauses.length > 1}
                onChange={(changes) =>
                  dispatch({ type: 'updateClause', ruleId: rule.id, uid: clause.uid, changes })
                }
                onRemove={() =>
                  dispatch({ type: 'removeClause', ruleId: rule.id, uid: clause.uid })
                }
              />
            </Box>
          ))}
          {clausesError && (
            <Typography variant="body2" color="error">
              {clausesError}
            </Typography>
          )}
          <Box>
            <Button
              size="small"
              startIcon={<AddRounded />}
              disabled={disabled || rule.clauses.length >= maxClauses}
              onClick={() =>
                dispatch({ type: 'addClause', ruleId: rule.id, clause: createClause() })
              }
            >
              Add condition
            </Button>
          </Box>
        </Stack>
        <Stack spacing={0.5}>
          <Typography variant="subtitle2" component="h4">
            Serve
          </Typography>
          <ServeEditor
            label={`${title} serves`}
            serve={rule.serve}
            onChange={(serve) =>
              dispatch({ type: 'setServe', location: { kind: 'rule', ruleId: rule.id }, serve })
            }
            variations={variations}
            errors={errors}
            path={`${path}.serve`}
            disabled={disabled}
          />
        </Stack>
      </Stack>
    </Paper>
  );
}
