namespace CombinationGenerator.Application.Dtos;

/// <summary>Body of <c>POST /api/permutations/start</c>.</summary>
/// <param name="N">Permutation size, <c>1..20</c>. A missing value binds as <c>0</c> and is rejected by validation.</param>
public sealed record StartSessionRequest(int N);
