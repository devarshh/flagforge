import { IconButton, Tooltip } from '@mui/material';
import CheckRounded from '@mui/icons-material/CheckRounded';
import ContentCopyRounded from '@mui/icons-material/ContentCopyRounded';
import { useEffect, useState } from 'react';

export interface CopyButtonProps {
  value: string;
  /** What is copied, for the accessible name: "Copy flag key". */
  label?: string;
}

export function CopyButton({ value, label = 'Copy' }: CopyButtonProps) {
  const [copied, setCopied] = useState(false);
  useEffect(() => {
    if (!copied) {
      return undefined;
    }

    const timer = setTimeout(() => setCopied(false), 1500);
    return () => clearTimeout(timer);
  }, [copied]);

  return (
    <Tooltip title={copied ? 'Copied' : label}>
      <IconButton
        size="small"
        aria-label={label}
        onClick={(event) => {
          event.stopPropagation();
          void navigator.clipboard.writeText(value).then(() => setCopied(true));
        }}
      >
        {copied ? <CheckRounded fontSize="inherit" /> : <ContentCopyRounded fontSize="inherit" />}
      </IconButton>
    </Tooltip>
  );
}
