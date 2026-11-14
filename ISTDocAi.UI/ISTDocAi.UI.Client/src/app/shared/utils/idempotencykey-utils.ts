export function generateIdempotencyKey(): string {
    return crypto.randomUUID(); // Uses built-in secure UUID generator
  }
