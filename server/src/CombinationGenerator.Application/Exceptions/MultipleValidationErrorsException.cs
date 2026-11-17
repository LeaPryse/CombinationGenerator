namespace CombinationGenerator.Application.Exceptions;

/// <summary>
/// Thrown when several input fields are invalid at once (e.g. <c>pageSize=0&amp;page=0</c>); the contract requires
/// <c>errors</c> to list each offending field. Created by <see cref="RequestValidationException.Combine"/>.
/// </summary>
public sealed class MultipleValidationErrorsException : RequestValidationException
{
    internal MultipleValidationErrorsException(IReadOnlyList<RequestValidationException> failures)
        : base(failures.SelectMany(f => f.Errors).ToList())
    {
        Failures = failures;
    }

    /// <summary>The individual failures, in reporting order.</summary>
    public IReadOnlyList<RequestValidationException> Failures { get; }
}
