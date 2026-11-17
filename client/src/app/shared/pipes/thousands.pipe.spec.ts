import { ThousandsPipe } from './thousands.pipe';

describe('ThousandsPipe', () => {
  const pipe = new ThousandsPipe();

  it('formats small numbers without separators', () => {
    expect(pipe.transform('6')).toBe('6');
    expect(pipe.transform('720')).toBe('720');
  });

  it('formats numeric strings with comma separators', () => {
    expect(pipe.transform('1000')).toBe('1,000');
    expect(pipe.transform('3628800')).toBe('3,628,800');
  });

  it('keeps full precision beyond 2^53 (20!)', () => {
    expect(pipe.transform('2432902008176640000')).toBe('2,432,902,008,176,640,000');
  });

  it('accepts BigInt and number', () => {
    expect(pipe.transform(2432902008176640000n)).toBe('2,432,902,008,176,640,000');
    expect(pipe.transform(1234567)).toBe('1,234,567');
  });

  it('handles negatives and leading zeros', () => {
    expect(pipe.transform('-1234')).toBe('-1,234');
    expect(pipe.transform('0001234')).toBe('1,234');
    expect(pipe.transform('0')).toBe('0');
  });

  it('returns empty string for null/undefined and passes through non-integers', () => {
    expect(pipe.transform(null)).toBe('');
    expect(pipe.transform(undefined)).toBe('');
    expect(pipe.transform('abc')).toBe('abc');
  });

  it('supports a custom separator', () => {
    expect(pipe.transform('1234567', ' ')).toBe('1 234 567');
  });
});
