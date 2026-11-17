using System.Diagnostics;
using CombinationGenerator.Domain.Permutations;

namespace CombinationGenerator.Domain.Tests.Permutations;

public class PermutationAlgorithmsTests
{
    private static readonly int[][] N3Sequence =
    [
        [1, 2, 3],
        [1, 3, 2],
        [2, 1, 3],
        [2, 3, 1],
        [3, 1, 2],
        [3, 2, 1],
    ];

    [Fact]
    public void NextPermutation_N3FromIdentity_YieldsSixPermutationsInLexicographicOrder()
    {
        var current = PermutationAlgorithms.Identity(3);
        var produced = new List<int[]> { (int[])current.Clone() };

        while (PermutationAlgorithms.NextPermutation(current))
        {
            produced.Add((int[])current.Clone());
        }

        produced.Should().BeEquivalentTo(N3Sequence, o => o.WithStrictOrdering());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void NextPermutation_IteratedFromIdentity_YieldsExactlyNFactorialDistinctPermutationsThenFalse(int n)
    {
        var current = PermutationAlgorithms.Identity(n);
        var seen = new HashSet<string> { string.Join(',', current) };
        long count = 1;

        while (PermutationAlgorithms.NextPermutation(current))
        {
            seen.Add(string.Join(',', current));
            count++;
        }

        count.Should().Be(PermutationAlgorithms.Factorial(n));
        seen.Should().HaveCount((int)count);
        current.Should().BeInDescendingOrder();
        PermutationAlgorithms.NextPermutation(current).Should().BeFalse();
    }

    [Fact]
    public void NextPermutation_LastPermutation_ReturnsFalseAndLeavesValuesUnchanged()
    {
        var values = new[] { 3, 2, 1 };

        var advanced = PermutationAlgorithms.NextPermutation(values);

        advanced.Should().BeFalse();
        values.Should().Equal(3, 2, 1);
    }

    [Fact]
    public void NextPermutation_EmptySpan_ReturnsFalse()
    {
        PermutationAlgorithms.NextPermutation(Span<int>.Empty).Should().BeFalse();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Unrank_EveryIndex_MatchesNextPermutationSequenceAndRanksBack(int n)
    {
        var expected = PermutationAlgorithms.Identity(n);
        var total = PermutationAlgorithms.Factorial(n);

        for (long i = 0; i < total; i++)
        {
            var unranked = PermutationAlgorithms.Unrank(n, i);

            unranked.Should().Equal(expected, $"index {i} for n={n}");
            PermutationAlgorithms.Rank(unranked).Should().Be(i);

            PermutationAlgorithms.NextPermutation(expected);
        }
    }

    [Fact]
    public void NextPermutation_OfUnrankAtRandomIndexN20_EqualsUnrankOfFollowingIndex()
    {
        var random = new Random(20_240_101);
        var total = PermutationAlgorithms.Factorial(20);

        for (var trial = 0; trial < 1000; trial++)
        {
            var i = random.NextInt64(0, total - 1);
            var current = PermutationAlgorithms.Unrank(20, i);

            PermutationAlgorithms.NextPermutation(current).Should().BeTrue();

            current.Should().Equal(PermutationAlgorithms.Unrank(20, i + 1), $"index {i}");
        }
    }

    [Fact]
    public void Rank_OfUnrankAtRandomIndexN20_ReturnsSameIndex()
    {
        var random = new Random(42);
        var total = PermutationAlgorithms.Factorial(20);

        for (var trial = 0; trial < 1000; trial++)
        {
            var i = random.NextInt64(0, total);

            PermutationAlgorithms.Rank(PermutationAlgorithms.Unrank(20, i)).Should().Be(i);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(20)]
    public void Unrank_Zero_ReturnsIdentity(int n)
    {
        PermutationAlgorithms.Unrank(n, 0).Should().Equal(Enumerable.Range(1, n));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(20)]
    public void Unrank_LastIndex_ReturnsDescending(int n)
    {
        var last = PermutationAlgorithms.Factorial(n) - 1;

        PermutationAlgorithms.Unrank(n, last).Should().Equal(Enumerable.Range(1, n).Reverse());
    }

    [Fact]
    public void Unrank_N20LastIndex_CompletesWellUnderOneMillisecond()
    {
        var last = PermutationAlgorithms.Factorial(20) - 1;
        PermutationAlgorithms.Unrank(20, last); // warm-up (JIT)
        const int iterations = 1000;

        var stopwatch = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            PermutationAlgorithms.Unrank(20, last);
        }

        stopwatch.Stop();

        // Average per call; total for 1000 calls must stay under 1000 ms (generous for CI machines).
        stopwatch.Elapsed.TotalMilliseconds.Should().BeLessThan(iterations);
    }

    [Fact]
    public void Factorial_20_Returns2432902008176640000()
    {
        PermutationAlgorithms.Factorial(20).Should().Be(2_432_902_008_176_640_000L);
    }

    [Theory]
    [InlineData(0, 1L)]
    [InlineData(1, 1L)]
    [InlineData(3, 6L)]
    [InlineData(10, 3_628_800L)]
    public void Factorial_SmallValues_ReturnsExpected(int n, long expected)
    {
        PermutationAlgorithms.Factorial(n).Should().Be(expected);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(21)]
    public void Factorial_OutOfRange_ThrowsArgumentOutOfRange(int n)
    {
        var act = () => PermutationAlgorithms.Factorial(n);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(21)]
    public void Unrank_InvalidN_ThrowsArgumentOutOfRange(int n)
    {
        var act = () => PermutationAlgorithms.Unrank(n, 0);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("n");
    }

    [Theory]
    [InlineData(3, -1L)]
    [InlineData(3, 6L)]
    [InlineData(20, long.MaxValue)]
    [InlineData(20, 2_432_902_008_176_640_000L)]
    public void Unrank_IndexOutOfRange_ThrowsArgumentOutOfRange(int n, long index)
    {
        var act = () => PermutationAlgorithms.Unrank(n, index);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("index");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Identity_InvalidN_ThrowsArgumentOutOfRange(int n)
    {
        var act = () => PermutationAlgorithms.Identity(n);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rank_EmptyOrTooLong_ThrowsArgumentOutOfRange()
    {
        var empty = () => PermutationAlgorithms.Rank(ReadOnlySpan<int>.Empty);
        var tooLong = () => PermutationAlgorithms.Rank(Enumerable.Range(1, 21).ToArray());

        empty.Should().Throw<ArgumentOutOfRangeException>();
        tooLong.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(new[] { 1, 1, 2 })]
    [InlineData(new[] { 0, 1, 2 })]
    [InlineData(new[] { 1, 2, 4 })]
    public void Rank_NotAPermutation_ThrowsArgumentException(int[] values)
    {
        var act = () => PermutationAlgorithms.Rank(values);

        act.Should().Throw<ArgumentException>().WithParameterName("permutation");
    }
}
