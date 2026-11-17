using CombinationGenerator.Application.Exceptions;
using CombinationGenerator.Domain.Sessions;

namespace CombinationGenerator.Application.Permutations;

/// <summary>
/// Session-independent checks of the paging query values. Each check returns the failure instead of throwing, so
/// that every offending field can be reported together (contract: "If several query values are invalid at once,
/// <c>errors</c> lists each offending field"). Used by <see cref="PermutationService.GetPageAsync"/> and by the Api for
/// values that bound successfully while another query value could not be parsed.
/// </summary>
/// <remarks>
/// The upper bound of <c>page</c> (<c>page &gt; totalPages</c>) depends on the session's anchor and is checked only
/// after the session has been looked up, so it is never combined with these checks.
/// </remarks>
public static class PagingValidation
{
    /// <summary>Checks <c>pageSize</c> against 1..1000.</summary>
    /// <param name="pageSize">The requested page size.</param>
    /// <returns>The failure, or <c>null</c> if valid.</returns>
    public static InvalidPageSizeException? CheckPageSize(int pageSize) =>
        pageSize < PermutationSession.MinPageSize || pageSize > PermutationSession.MaxPageSize
            ? new InvalidPageSizeException(pageSize)
            : null;

    /// <summary>Checks the lower bound of <c>page</c> (must be at least 1).</summary>
    /// <param name="page">The requested page.</param>
    /// <returns>The failure, or <c>null</c> if valid.</returns>
    public static InvalidPageException? CheckPageLowerBound(long page) =>
        page < 1 ? InvalidPageException.TooSmall(page) : null;

    /// <summary>
    /// Runs both checks; throws a single <see cref="InvalidPageSizeException"/> / <see cref="InvalidPageException"/>
    /// if one fails, or a <see cref="MultipleValidationErrorsException"/> (<c>pageSize</c> first) if both fail.
    /// </summary>
    /// <param name="pageSize">The requested page size.</param>
    /// <param name="page">The requested page.</param>
    public static void EnsureValid(int pageSize, long page)
    {
        var failure = RequestValidationException.Combine(CheckPageSize(pageSize), CheckPageLowerBound(page));
        if (failure is not null)
        {
            throw failure;
        }
    }
}
