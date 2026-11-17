namespace CombinationGenerator.Application.Dtos;

/// <summary>Response of <c>GET /api/permutations/next</c>.</summary>
/// <param name="Index">1-based index of the returned permutation as decimal text; <c>null</c> when none is returned.</param>
/// <param name="Permutation">The permutation; <c>null</c> when none is returned.</param>
/// <param name="HasMore"><c>true</c> if another call to <c>next</c> would return a permutation.</param>
/// <param name="Message"><c>"No more combinations"</c> when nothing is returned, otherwise <c>null</c>.</param>
public sealed record NextPermutationResponse(
    string? Index,
    IReadOnlyList<int>? Permutation,
    bool HasMore,
    string? Message);
