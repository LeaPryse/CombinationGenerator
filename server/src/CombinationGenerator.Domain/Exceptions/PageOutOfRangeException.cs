namespace CombinationGenerator.Domain.Exceptions;

/// <summary>
/// Thrown when a requested page number is greater than the number of available pages
/// (page 1 is always accepted, even when there are no pages).
/// </summary>
public sealed class PageOutOfRangeException : DomainException
{
    /// <summary>Initializes a new instance.</summary>
    /// <param name="page">The requested (1-based) page number.</param>
    /// <param name="totalPages">The number of pages that exist.</param>
    public PageOutOfRangeException(long page, long totalPages)
        : base($"page must be between 1 and {totalPages}.")
    {
        Page = page;
        TotalPages = totalPages;
    }

    /// <summary>The requested (1-based) page number.</summary>
    public long Page { get; }

    /// <summary>The number of pages that exist.</summary>
    public long TotalPages { get; }
}
