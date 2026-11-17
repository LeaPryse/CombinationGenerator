namespace CombinationGenerator.Domain.Exceptions;

/// <summary>
/// Base type for violations of domain rules that the caller can correct (mapped to HTTP 400 by the Api).
/// </summary>
public abstract class DomainException : Exception
{
    /// <summary>Initializes a new instance with the given message.</summary>
    /// <param name="message">Human-readable description of the violated rule.</param>
    protected DomainException(string message)
        : base(message)
    {
    }
}
