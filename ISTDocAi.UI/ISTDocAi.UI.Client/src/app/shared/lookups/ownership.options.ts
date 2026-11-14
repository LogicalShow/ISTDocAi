import { Option } from '../models/option.model';

export const OWNERSHIP_OPTIONS: readonly Option<boolean>[] = Object.freeze([
  { value: true, label: 'Owned' },
  { value: false, label: 'Rented' },
]);
