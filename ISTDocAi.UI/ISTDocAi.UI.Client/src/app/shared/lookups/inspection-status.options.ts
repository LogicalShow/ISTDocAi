import { Option } from '../models/option.model';

export const INSPECTION_STATUS_OPTIONS: readonly Option<number>[] = Object.freeze([
  { value: 1, label: 'Accepted' },
  { value: 2, label: 'Rejected' },
]);
