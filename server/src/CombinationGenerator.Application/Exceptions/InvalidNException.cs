using CombinationGenerator.Domain.Permutations;

namespace CombinationGenerator.Application.Exceptions;

/// <summary>Thrown when the requested permutation size <c>n</c> is outside the supported range.</summary>
public sealed class InvalidNException : RequestValidationException
{
    /// <summary>Name of the field as the client knows it.</summary>
    public const string FieldName = "n";

    /// <summary>
    /// The contract message for any invalid <c>n</c>: out of range, missing, or not a JSON integer (the Api reuses it
    /// for malformed request bodies).
    /// </summary>
    public static readonly string ErrorMessage =
        $"n must be between {PermutationAlgorithms.MinN} and {PermutationAlgorithms.MaxN}.";

    /// <summary>Initializes a new instance.</summary>
    /// <param name="n">The rejected value.</param>
    public InvalidNException(int n)
        : base(FieldName, ErrorMessage)
    {
        N = n;
    }

    /// <summary>The rejected value.</summary>
    public int N { get; }
}
