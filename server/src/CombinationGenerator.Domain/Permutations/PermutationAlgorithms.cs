namespace CombinationGenerator.Domain.Permutations;

/// <summary>
/// Pure, stateless algorithms on permutations of <c>[1..n]</c> in lexicographic order.
/// </summary>
/// <remarks>
/// All indices handled here are <b>0-based</b> (index 0 is the identity permutation).
/// Conversion to the 1-based display numbers of the API happens in the Application layer.
/// Nothing is ever precomputed except the 21-entry factorial table.
/// </remarks>
public static class PermutationAlgorithms
{
    /// <summary>Smallest supported permutation size.</summary>
    public const int MinN = 1;

    /// <summary>
    /// Largest supported permutation size. <c>20! = 2,432,902,008,176,640,000</c> fits in <see cref="long"/>;
    /// <c>21!</c> would overflow.
    /// </summary>
    public const int MaxN = 20;

    private static readonly long[] Factorials = BuildFactorials();

    /// <summary>
    /// Returns <paramref name="n"/>! from a precomputed table. O(1).
    /// </summary>
    /// <param name="n">Value in <c>0..<see cref="MaxN"/></c> (<c>0! = 1</c> is accepted for convenience).</param>
    /// <returns>The factorial of <paramref name="n"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is negative or greater than <see cref="MaxN"/>.</exception>
    public static long Factorial(int n)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(n, MaxN);
        return Factorials[n];
    }

    /// <summary>
    /// Returns the identity permutation <c>[1, 2, …, n]</c> (index 0). O(n).
    /// </summary>
    /// <param name="n">Permutation size in <c><see cref="MinN"/>..<see cref="MaxN"/></c>.</param>
    /// <returns>A new array containing <c>1..n</c> in ascending order.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is outside the supported range.</exception>
    public static int[] Identity(int n)
    {
        ValidateN(n);
        var result = new int[n];
        for (var i = 0; i < n; i++)
        {
            result[i] = i + 1;
        }

        return result;
    }

    /// <summary>
    /// Transforms <paramref name="values"/> in place into its lexicographic successor.
    /// O(n) worst case, amortized O(1); no allocation.
    /// </summary>
    /// <param name="values">The permutation to advance. Any sequence of comparable integers is accepted.</param>
    /// <returns>
    /// <c>true</c> if a successor existed and <paramref name="values"/> now holds it;
    /// <c>false</c> if <paramref name="values"/> was already the last (descending) permutation, in which case it is left unchanged.
    /// </returns>
    public static bool NextPermutation(Span<int> values)
    {
        var i = values.Length - 2;
        while (i >= 0 && values[i] >= values[i + 1])
        {
            i--;
        }

        if (i < 0)
        {
            return false;
        }

        var j = values.Length - 1;
        while (values[j] <= values[i])
        {
            j--;
        }

        (values[i], values[j]) = (values[j], values[i]);
        values[(i + 1)..].Reverse();
        return true;
    }

    /// <summary>
    /// Computes the permutation of <c>[1..n]</c> at the given 0-based lexicographic <paramref name="index"/>
    /// using the factorial number system (Lehmer code). O(n²) time, O(n) memory — independent of <c>n!</c>,
    /// so any index (e.g. the last one for n = 20) is reached directly.
    /// </summary>
    /// <param name="n">Permutation size in <c><see cref="MinN"/>..<see cref="MaxN"/></c>.</param>
    /// <param name="index">0-based index in <c>0..n!−1</c>.</param>
    /// <returns>A new array holding the permutation.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> or <paramref name="index"/> is out of range.</exception>
    public static int[] Unrank(int n, long index)
    {
        ValidateN(n);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Factorials[n]);

        Span<int> available = stackalloc int[n];
        for (var i = 0; i < n; i++)
        {
            available[i] = i + 1;
        }

        var result = new int[n];
        var remaining = n;
        for (var pos = 0; pos < n; pos++)
        {
            var f = Factorials[n - 1 - pos];
            var k = (int)(index / f);
            index %= f;

            result[pos] = available[k];
            available[(k + 1)..remaining].CopyTo(available[k..]);
            remaining--;
        }

        return result;
    }

    /// <summary>
    /// Computes the 0-based lexicographic index of a permutation of <c>[1..n]</c>; the inverse of
    /// <see cref="Unrank"/>. O(n²) time, no allocation.
    /// </summary>
    /// <param name="permutation">A permutation of <c>1..n</c> with <c>n</c> in <c><see cref="MinN"/>..<see cref="MaxN"/></c>.</param>
    /// <returns>The 0-based index in <c>0..n!−1</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The length is outside the supported range.</exception>
    /// <exception cref="ArgumentException"><paramref name="permutation"/> is not a permutation of <c>1..n</c>.</exception>
    public static long Rank(ReadOnlySpan<int> permutation)
    {
        var n = permutation.Length;
        ArgumentOutOfRangeException.ThrowIfLessThan(n, MinN, nameof(permutation));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(n, MaxN, nameof(permutation));

        Span<bool> seen = stackalloc bool[n + 1];
        foreach (var value in permutation)
        {
            if (value < 1 || value > n || seen[value])
            {
                throw new ArgumentException($"The values must be a permutation of 1..{n}.", nameof(permutation));
            }

            seen[value] = true;
        }

        long rank = 0;
        for (var i = 0; i < n; i++)
        {
            var smallerToTheRight = 0;
            for (var j = i + 1; j < n; j++)
            {
                if (permutation[j] < permutation[i])
                {
                    smallerToTheRight++;
                }
            }

            rank += smallerToTheRight * Factorials[n - 1 - i];
        }

        return rank;
    }

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> unless <paramref name="n"/> is in
    /// <c><see cref="MinN"/>..<see cref="MaxN"/></c>. O(1).
    /// </summary>
    /// <param name="n">The permutation size to validate.</param>
    /// <param name="paramName">Parameter name reported in the exception.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is out of range.</exception>
    public static void ValidateN(int n, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(n))] string? paramName = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(n, MinN, paramName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(n, MaxN, paramName);
    }

    private static long[] BuildFactorials()
    {
        var table = new long[MaxN + 1];
        table[0] = 1;
        for (var i = 1; i <= MaxN; i++)
        {
            table[i] = checked(table[i - 1] * i);
        }

        return table;
    }
}
