namespace CombinationGenerator.Application.Exceptions;

/// <summary>
/// Thrown when the requested page is less than 1 or greater than the number of pages after the anchor.
/// </summary>
public sealed class InvalidPageException : RequestValidationException
{
    /// <summary>Name of the field as the client knows it.</summary>
    public const string FieldName = "page";

    /// <summary>Message for a page number below 1.</summary>
    public const string TooSmallMessage = "page must be greater than or equal to 1.";

    /// <summary>
    /// Message for a <c>page</c> query value that is not a 64-bit integer (e.g. <c>abc</c>, <c>1.5</c>, or beyond
    /// <see cref="long.MaxValue"/>). Used by the Api, which rejects such values before they reach this layer.
    /// </summary>
    public const string NotAnIntegerMessage = "page must be an integer greater than or equal to 1.";

    private InvalidPageException(long page, string message)
        : base(FieldName, message)
    {
        Page = page;
    }

    /// <summary>The rejected value.</summary>
    public long Page { get; }

    /// <summary>Creates the exception for a page number below 1.</summary>
    /// <param name="page">The rejected value.</param>
    /// <returns>The exception.</returns>
    public static InvalidPageException TooSmall(long page) => new(page, TooSmallMessage);

    /// <summary>Creates the exception for a page number beyond the last page.</summary>
    /// <remarks>
    /// Page 1 is always accepted (an empty page when nothing follows the anchor), so the reported upper bound is
    /// <c>max(1, totalPages)</c>: with zero pages the message reads "between 1 and 1", never "between 1 and 0".
    /// </remarks>
    /// <param name="page">The rejected value.</param>
    /// <param name="totalPages">The number of pages that exist.</param>
    /// <returns>The exception.</returns>
    public static InvalidPageException TooLarge(long page, long totalPages) =>
        new(page, $"page must be between 1 and {Math.Max(1, totalPages)}.");
}
