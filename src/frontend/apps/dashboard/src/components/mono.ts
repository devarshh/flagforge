/** Adds the code font to an input's classes without dropping the ones MUI set (Autocomplete sizes its input by class). */
export function withMono(className: string | undefined): string {
  return className ? `${className} mono` : 'mono';
}
