/**
 * Formats a Date as yyyy-MM-dd for the API.
 *
 * Built from the local date parts on purpose. `toISOString()` converts to UTC first, so a date
 * picked as the 15th in any timezone west of Greenwich is sent as the 14th — which is exactly
 * how a hire date silently shifts by a day. The API's DateOnly has no timezone, so the value
 * the user picked is the value it should receive.
 */
export function toIsoDate(date: Date): string {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');

  return `${year}-${month}-${day}`;
}

/**
 * Parses a yyyy-MM-dd string from the API into a local Date.
 *
 * The mirror of {@link toIsoDate}: `new Date('2024-01-15')` is parsed as UTC midnight and
 * renders as the 14th in western timezones, so the parts are passed to the constructor
 * individually instead.
 */
export function fromIsoDate(value: string): Date {
  const [year, month, day] = value.split('-').map(Number);

  return new Date(year!, month! - 1, day!);
}
