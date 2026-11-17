namespace CombinationGenerator.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Independent oracle (does not reuse production code): brute-force lexicographic permutations for small n.
/// </summary>
public static class Reference
{
    public static readonly int[][] N3 =
    [
        [1, 2, 3],
        [1, 3, 2],
        [2, 1, 3],
        [2, 3, 1],
        [3, 1, 2],
        [3, 2, 1],
    ];

    public static List<int[]> AllPermutations(int n)
    {
        var result = new List<int[]>();
        Recurse(Enumerable.Range(1, n).ToList(), [], result);
        return result;
    }

    private static void Recurse(List<int> remaining, List<int> prefix, List<int[]> result)
    {
        if (remaining.Count == 0)
        {
            result.Add([.. prefix]);
            return;
        }

        // remaining is kept sorted, so iterating in order yields lexicographic order.
        foreach (var value in remaining.ToList())
        {
            remaining.Remove(value);
            prefix.Add(value);
            Recurse(remaining, prefix, result);
            prefix.RemoveAt(prefix.Count - 1);
            var insertAt = remaining.FindIndex(v => v > value);
            remaining.Insert(insertAt < 0 ? remaining.Count : insertAt, value);
        }
    }
}
