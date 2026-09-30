import { Autocomplete, TextField } from '@mui/material';
import { useState, type ClipboardEvent, type ReactNode } from 'react';
import { withMono } from './mono';

export interface ChipInputProps {
  label: string;
  value: readonly string[];
  onChange: (value: string[]) => void;
  /** Suggestions shown while typing; any other text is accepted too. */
  options?: readonly string[];
  placeholder?: string;
  helperText?: ReactNode;
  error?: boolean;
  disabled?: boolean;
  /** Use the code font for values (keys, identifiers). */
  mono?: boolean;
  id?: string;
}

const separators = /[,\n\r\t]+/;

function split(text: string): string[] {
  return text
    .split(separators)
    .map((part) => part.trim())
    .filter((part) => part.length > 0);
}

function merge(current: readonly string[], added: readonly string[]): string[] {
  return [...new Set([...current, ...added])];
}

/**
 * A multi-value text input shown as chips. Enter, comma, or blur adds the typed value; pasting comma- or
 * newline-separated text adds every entry; duplicates are dropped.
 */
export function ChipInput({
  label,
  value,
  onChange,
  options = [],
  placeholder,
  helperText,
  error = false,
  disabled = false,
  mono = false,
  id,
}: ChipInputProps) {
  const [inputValue, setInputValue] = useState('');

  const onPaste = (event: ClipboardEvent<HTMLInputElement>) => {
    const text = event.clipboardData.getData('text');
    if (separators.test(text)) {
      event.preventDefault();
      onChange(merge(value, split(text)));
      setInputValue('');
    }
  };

  return (
    <Autocomplete
      multiple
      freeSolo
      autoSelect
      id={id}
      disabled={disabled}
      options={options}
      value={value as string[]}
      inputValue={inputValue}
      onInputChange={(_, text, reason) => {
        if (reason === 'input' && separators.test(text)) {
          onChange(merge(value, split(text)));
          setInputValue('');
          return;
        }

        setInputValue(text);
      }}
      onChange={(_, next) => onChange(merge([], next.flatMap(split)))}
      filterSelectedOptions
      slotProps={{ chip: { size: 'small', className: mono ? 'mono' : undefined } }}
      renderInput={(params) => (
        <TextField
          {...params}
          label={label}
          placeholder={value.length === 0 ? placeholder : undefined}
          helperText={helperText}
          error={error}
          slotProps={{
            ...params.slotProps,
            htmlInput: {
              ...params.slotProps.htmlInput,
              onPaste,
              className: mono
                ? withMono(params.slotProps.htmlInput.className)
                : params.slotProps.htmlInput.className,
            },
          }}
        />
      )}
    />
  );
}
