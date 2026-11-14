import { Option } from '../models/option.model';

export const DOCUMENT_STATUS_OPTIONS: readonly Option<number>[] = Object.freeze([
  { value: 1, label: 'Copy' },
  { value: 2, label: 'Original' },
  { value: 3, label: 'Certified True Copy' },
]);
