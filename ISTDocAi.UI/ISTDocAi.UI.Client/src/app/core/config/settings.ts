/**
 * Global application settings
 */
export class Settings {
  /**
   * Minimum number of characters required to trigger server-side search in select dropdowns.
   * - 0: Load data immediately when dropdown opens
   * - 1: Load after typing 1 letter
   * - 3: Load after typing 3 letters (default)
   */
  static readonly searchLength: number = 0;

  /**
   * Default page size for data loading from the backend.
   * - 50: Load 50 records per page (default)
   */
  static readonly DataSize: number = 50;
}
