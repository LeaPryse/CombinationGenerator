namespace CombinationGenerator.Application.Dtos;

/// <summary>Response of <c>GET /api/permutations/all</c>.</summary>
/// <param name="Items">Permutations of this page, in order (possibly empty).</param>
/// <param name="Page">Echo of the requested page as decimal text.</param>
/// <param name="PageSize">Echo of the effective page size.</param>
/// <param name="TotalPages"><c>ceil(totalItems / pageSize)</c> as decimal text.</param>
/// <param name="TotalItems">Number of permutations after the anchor (<c>n! − anchorIndex</c>) as decimal text.</param>
/// <param name="FirstIndex">1-based index of the first permutation after the anchor; <c>null</c> when <paramref name="TotalItems"/> is <c>"0"</c>.</param>
/// <param name="Message"><c>"No more combinations"</c> when <paramref name="TotalItems"/> is <c>"0"</c>, otherwise <c>null</c>.</param>
public sealed record PermutationPageResponse(
    IReadOnlyList<PermutationItemDto> Items,
    string Page,
    int PageSize,
    string TotalPages,
    string TotalItems,
    string? FirstIndex,
    string? Message);
