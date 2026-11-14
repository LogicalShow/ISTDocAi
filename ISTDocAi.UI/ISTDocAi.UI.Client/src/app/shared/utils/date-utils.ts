import { addDays, format, isValid, parseISO } from "date-fns";

type DateInput = Date | string | number | null | undefined;

export function toStartOfDay(v: DateInput): Date | null {
    if (v == null || v === '') return null;
    const d = v instanceof Date ? new Date(v) : new Date(v);
    if (isNaN(d.getTime())) return null;
    d.setHours(0, 0, 0, 0);
    return d;
}

export function toEndOfDay(v: DateInput): Date | null {
    if (v == null || v === '') return null;
    const d = v instanceof Date ? new Date(v) : new Date(v);
    if (isNaN(d.getTime())) return null;
    d.setHours(23, 59, 59, 999);
    return d;
}

/** Formats a Date (or ISO string) to "YYYY-MM-DD" using local calendar fields.
 *  Returns undefined for null/undefined/invalid inputs.
 */
export function toLocalDateOnly(
  d: Date | string | null | undefined
): string | undefined {
  if (d == null) return undefined;
  
  const date = typeof d === 'string' ? parseISO(d) : d;
  if (!isValid(date)) return undefined;
  
  return format(date, 'yyyy-MM-dd');
}

/** Formats a Date to "YYYY-MM-DD" for the NEXT day.
 *  Use this for date range "To" filters to include the entire selected day.
 *  
 *  Example: User selects Oct 14 TO Oct 15
 *  - IssueDateFrom: toLocalDateOnly("2025-10-14") → "2025-10-14" (00:00:00)
 *  - IssueDateTo: toNextDayLocalDateOnly("2025-10-15") → "2025-10-16" (00:00:00)
 *  Result: Range includes all of Oct 14 and all of Oct 15
 */
export function toNextDayLocalDateOnly(
  d: Date | string | null | undefined
): string | undefined {
   if (d == null) return undefined;
  
  const date = typeof d === 'string' ? parseISO(d) : d;
  if (!isValid(date)) return undefined;
  
  const nextDay = addDays(date, 1);
  return format(nextDay, 'yyyy-MM-dd');
}

/** Formats a Date to ISO 8601 format "YYYY-MM-DDTHH:mm:ss" for API submission.
 *  This format is compatible with .NET's DateTime JSON deserialization.
 *  Returns undefined for null/undefined/invalid inputs.
 */
export function toLocalDateTime(
  d: Date | string | null | undefined
): string | undefined {
  if (d == null) return undefined;
  
  const date = typeof d === 'string' ? parseISO(d) : d;
  if (!isValid(date)) return undefined;
  
  return format(date, "yyyy-MM-dd'T'HH:mm:ss");
}

/** Truncates a Date object to minutes by setting seconds and milliseconds to 0.
 */
export function truncateToMinutes(date: Date | string | null): Date | null {
  if (!date) return null;
  const newDate = typeof date === 'string' ? new Date(date) : new Date(date);
  newDate.setSeconds(0);
  newDate.setMilliseconds(0);
  return newDate;
}
