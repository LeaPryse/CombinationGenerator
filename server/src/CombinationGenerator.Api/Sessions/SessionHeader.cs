namespace CombinationGenerator.Api.Sessions;

/// <summary>
/// The <c>X-Session-Id</c> HTTP header that carries the session id. Transport-level concern, hence in the Api layer:
/// the Application layer only ever sees a parsed <see cref="Guid"/>.
/// </summary>
public static class SessionHeader
{
    /// <summary>Name of the HTTP header.</summary>
    public const string Name = "X-Session-Id";

    /// <summary>Contract message for a missing or invalid header.</summary>
    public const string ErrorMessage = $"The {Name} header is required and must be a GUID.";

    /// <summary>Parses the header value.</summary>
    /// <param name="value">The raw header value (null when the header is absent).</param>
    /// <returns>The session id.</returns>
    /// <exception cref="InvalidSessionHeaderException">The header is missing or not a GUID.</exception>
    public static Guid Parse(string? value) =>
        Guid.TryParse(value, out var id) ? id : throw new InvalidSessionHeaderException();
}
