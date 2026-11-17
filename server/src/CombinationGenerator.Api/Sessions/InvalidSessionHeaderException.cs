using CombinationGenerator.Application.Exceptions;

namespace CombinationGenerator.Api.Sessions;

/// <summary>
/// Thrown when the <c>X-Session-Id</c> header is missing or is not a GUID. Mapped to the standard 400 validation
/// ProblemDetails with <c>errors["X-Session-Id"]</c>.
/// </summary>
public sealed class InvalidSessionHeaderException : RequestValidationException
{
    /// <summary>Initializes a new instance.</summary>
    public InvalidSessionHeaderException()
        : base(SessionHeader.Name, SessionHeader.ErrorMessage)
    {
    }
}
