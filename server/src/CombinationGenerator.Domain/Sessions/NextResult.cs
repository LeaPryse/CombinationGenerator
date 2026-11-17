namespace CombinationGenerator.Domain.Sessions;

/// <summary>
/// Outcome of <see cref="PermutationSession.MoveNext"/>.
/// </summary>
/// <param name="Item">The permutation that became current, or <c>null</c> when the sequence was already exhausted.</param>
/// <param name="HasMore"><c>true</c> if a further call to <see cref="PermutationSession.MoveNext"/> would return a permutation.</param>
public sealed record NextResult(PermutationItem? Item, bool HasMore)
{
    /// <summary>The result returned when no permutation follows the current one.</summary>
    public static NextResult Exhausted { get; } = new(null, false);

    /// <summary><c>true</c> when no permutation was returned (the session was already on the last one).</summary>
    public bool IsExhausted => Item is null;
}
