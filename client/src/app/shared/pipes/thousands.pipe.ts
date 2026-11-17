import { Pipe, PipeTransform } from '@angular/core';

/**
 * Formats an integer given as a decimal string, BigInt or number as `X,XXX,XXX`.
 * Works on the digit string directly, so values beyond 2^53 keep full precision.
 * Non-integer input is returned unchanged.
 */
@Pipe({ name: 'thousands' })
export class ThousandsPipe implements PipeTransform {
  transform(value: string | bigint | number | null | undefined, separator = ','): string {
    if (value === null || value === undefined) return '';
    if (typeof value === 'number' && !Number.isSafeInteger(value)) return String(value);

    const text = value.toString().trim();
    const match = /^(-?)(\d+)$/.exec(text);
    if (!match) return text;

    const [, sign, digits] = match;
    const normalized = digits.replace(/^0+(?=\d)/, '');
    return sign + normalized.replace(/\B(?=(\d{3})+(?!\d))/g, separator);
  }
}
