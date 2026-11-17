namespace CombinationGenerator.Domain.Sessions;

/// <summary>
/// One permutation together with its position in the lexicographic sequence.
/// </summary>
/// <param name="Index">
/// <b>0-based</b> lexicographic index (0 = identity). The Application layer adds 1 to obtain the display number.
/// </param>
/// <param name="Values">
/// The permutation of <c>1..n</c>. The array is a private copy owned by this item; it is not shared with the session.
/// Note that record equality compares this array by reference.
/// </param>
public sealed record PermutationItem(long Index, int[] Values);
