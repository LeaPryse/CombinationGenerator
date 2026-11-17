import { PermutationPipe } from './permutation.pipe';

describe('PermutationPipe', () => {
  const pipe = new PermutationPipe();

  it('formats a permutation as [a,b,c]', () => {
    expect(pipe.transform([1, 2, 3])).toBe('[1,2,3]');
  });

  it('returns empty string for null', () => {
    expect(pipe.transform(null)).toBe('');
  });
});
