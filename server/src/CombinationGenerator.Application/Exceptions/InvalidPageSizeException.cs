using CombinationGenerator.Domain.Sessions;

namespace CombinationGenerator.Application.Exceptions;

/// <summary>Thrown when the requested page size is outside the supported range.</summary>
public sealed class InvalidPageSizeException : RequestValidationException
{
    /// <summary>Name of the field as the client knows it.</summary>
    public const string FieldName = "pageSize";

    /// <summary>
    /// The contract message for any invalid <c>pageSize</c>: out of range or not a 32-bit integer (the Api reuses it
    /// for query values that cannot be parsed).
    /// </summary>
    public static readonly string ErrorMessage =
        $"pageSize must be between {PermutationSession.MinPageSize} and {PermutationSession.MaxPageSize}.";

    /// <summary>Initializes a new instance.</summary>
    /// <param name="pageSize">The rejected value.</param>
    public InvalidPageSizeException(int pageSize)
        : base(FieldName, ErrorMessage)
    {
        PageSize = pageSize;
    }

    /// <summary>The rejected value.</summary>
    public int PageSize { get; }
}
