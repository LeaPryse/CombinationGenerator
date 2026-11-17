namespace CombinationGenerator.Application.Dtos;

/// <summary>Response of <c>POST /api/permutations/start</c>.</summary>
/// <param name="SessionId">Identifier to send in the <c>X-Session-Id</c> header on every subsequent call.</param>
/// <param name="N">Echo of the requested permutation size.</param>
/// <param name="TotalCount"><c>n!</c> as decimal text (it may exceed 2^53).</param>
public sealed record StartSessionResponse(Guid SessionId, int N, string TotalCount);
