namespace CombinationGenerator.Application.Exceptions;

/// <summary>
/// Base type for invalid client input. The Api maps it to a <c>400</c> validation ProblemDetails whose
/// <c>errors</c> object contains every entry of <see cref="Errors"/> (field → message), in order, and whose
/// <c>detail</c> is <see cref="Exception.Message"/> (the first error's message).
/// </summary>
public abstract class RequestValidationException : Exception
{
    /// <summary>Initializes a new instance for a single offending field.</summary>
    /// <param name="field">Name of the offending field as the client knows it (e.g. <c>n</c>, <c>pageSize</c>).</param>
    /// <param name="message">Human-readable validation message.</param>
    protected RequestValidationException(string field, string message)
        : base(message)
    {
        Field = field;
        Errors = [new KeyValuePair<string, string>(field, message)];
    }

    /// <summary>Initializes a new instance for several offending fields (at least one).</summary>
    /// <param name="errors">Field → message pairs, in the order they are reported.</param>
    private protected RequestValidationException(IReadOnlyList<KeyValuePair<string, string>> errors)
        : base(errors[0].Value)
    {
        Field = errors[0].Key;
        Errors = errors;
    }

    /// <summary>Name of the (first) offending field as the client knows it.</summary>
    public string Field { get; }

    /// <summary>Every offending field with its message, in reporting order; the first is <see cref="Field"/>.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Errors { get; }

    /// <summary>
    /// Combines independent validation failures: <c>null</c> if there are none, the failure itself if there is one,
    /// otherwise a <see cref="MultipleValidationErrorsException"/> listing all of them in the given order.
    /// </summary>
    /// <param name="failures">The individual failures; <c>null</c> entries (passed checks) are ignored.</param>
    /// <returns>The exception to throw, or <c>null</c> if the input is valid.</returns>
    public static RequestValidationException? Combine(params RequestValidationException?[] failures)
    {
        var present = failures.OfType<RequestValidationException>().ToList();
        return present.Count switch
        {
            0 => null,
            1 => present[0],
            _ => new MultipleValidationErrorsException(present),
        };
    }
}
