/**
 * Trims all string values in an object, preserving non-string values
 * @param obj Object containing string values to trim
 * @returns New object with all string values trimmed
 */
export function trimObjectStrings<
  T extends Record<string, string | number | boolean | null | undefined>
>(obj: T): T {
    if (!obj) return obj;
    
    const result = { ...obj };
    
    (Object.keys(result) as (keyof T)[]).forEach(key => {
        if (typeof result[key] === 'string') {
            result[key] = result[key].trim() as T[keyof T];
        }
    });
    
    return result;
}
