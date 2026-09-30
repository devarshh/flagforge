import { z } from 'zod';

/** Flag tags: at most 10, each 1 to 32 characters (the server's rules). */
export const tagsSchema = z
  .array(z.string().trim().min(1).max(32, 'Tags are 1 to 32 characters.'))
  .max(10, 'Use at most 10 tags.');
