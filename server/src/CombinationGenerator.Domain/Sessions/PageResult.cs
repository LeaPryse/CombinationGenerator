namespace CombinationGenerator.Domain.Sessions;

/// <summary>
/// One page of the permutations that follow the paging anchor, as returned by <see cref="PermutationSession.GetPage"/>.
/// </summary>
/// <param name="Items">The permutations of this page, in order (possibly empty).</param>
/// <param name="Page">Echo of the requested 1-based page number.</param>
/// <param name="PageSize">Echo of the page size.</param>
/// <param name="TotalItems">Number of permutations after the anchor: <c>n! − <see cref="PermutationSession.PagingAnchor"/></c>.</param>
/// <param name="TotalPages"><c>ceil(TotalItems / PageSize)</c>; 0 when <paramref name="TotalItems"/> is 0.</param>
/// <param name="FirstIndex">
/// <b>0-based</b> index of the first permutation after the anchor (page 1, item 1), or <c>null</c> when
/// <paramref name="TotalItems"/> is 0. Numerically it equals the 1-based index of the anchor permutation itself.
/// </param>
public sealed record PageResult(
    IReadOnlyList<PermutationItem> Items,
    long Page,
    int PageSize,
    long TotalItems,
    long TotalPages,
    long? FirstIndex)
{
    /// <summary><c>true</c> when nothing follows the anchor (the page has no items).</summary>
    public bool IsEmpty => Items.Count == 0;
}
