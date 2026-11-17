namespace CombinationGenerator.Application.Dtos;

/// <summary>One permutation of a page.</summary>
/// <param name="Index">1-based index as decimal text.</param>
/// <param name="Permutation">The permutation of <c>1..n</c>.</param>
public sealed record PermutationItemDto(string Index, IReadOnlyList<int> Permutation);
