import { Option } from '../models/option.model';

export const JOB_ORDER_STATUS_OPTIONS: readonly Option<number>[] = Object.freeze([
  { value: 1, label: 'Pending' },
  { value: 2, label: 'Resources Assigned' },
  { value: 3, label: 'In Progress' },
  { value: 4, label: 'On Hold' },
  { value: 5, label: 'Reactivate' },
  { value: 6, label: 'Completed' },
  { value: 7, label: 'Canceled' },
]);
