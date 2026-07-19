using System.Diagnostics.CodeAnalysis;

namespace GenEngine.Secrets;

/// <summary>
/// A parsed, opaque pointer to a secret held outside the configuration document.
/// Grammar: <c>scheme:identifier</c>.
/// <list type="bullet">
///   <item><description><c>scheme</c> matches <c>[a-z][a-z0-9-]*</c> (lowercase, ASCII).</description></item>
///   <item><description><c>identifier</c> is scheme specific, non empty, free of whitespace and control characters.</description></item>
/// </list>
/// A reference never carries the secret itself. It is safe to persist, but it is not
/// published to clients: <see cref="System.Object.ToString"/> stays available for
/// diagnostics inside a trusted process only, never for error messages.
/// </summary>
public readonly record struct SecretReference
{
    /// <summary>Longest accepted raw reference; keeps parsing bounded.</summary>
    public const int MaximumLength = 512;

    private SecretReference(string scheme, string identifier)
    {
        Scheme = scheme;
        Identifier = identifier;
    }

    /// <summary>The lowercase scheme, for example <c>env</c> or <c>vault</c>.</summary>
    public string Scheme { get; }

    /// <summary>The scheme specific locator, for example an environment variable name.</summary>
    public string Identifier { get; }

    /// <summary>
    /// Parses <paramref name="value"/> against the reference grammar. Returns <see langword="false"/>
    /// instead of throwing so a malformed reference degrades into "not configured" rather than
    /// surfacing its own text through an exception message.
    /// </summary>
    public static bool TryParse(string? value, out SecretReference reference)
    {
        reference = default;

        if (string.IsNullOrWhiteSpace(value) || value.Length > MaximumLength)
        {
            return false;
        }

        int separator = value.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0 || separator == value.Length - 1)
        {
            return false;
        }

        string scheme = value[..separator];
        string identifier = value[(separator + 1)..];

        if (!IsValidScheme(scheme) || !IsValidIdentifier(identifier))
        {
            return false;
        }

        reference = new SecretReference(scheme, identifier);
        return true;
    }

    private static bool IsValidScheme(ReadOnlySpan<char> scheme)
    {
        if (scheme.Length == 0 || !char.IsAsciiLetterLower(scheme[0]))
        {
            return false;
        }

        foreach (char character in scheme)
        {
            if (!char.IsAsciiLetterLower(character) && !char.IsAsciiDigit(character) && character != '-')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidIdentifier(ReadOnlySpan<char> identifier)
    {
        if (identifier.Length == 0)
        {
            return false;
        }

        foreach (char character in identifier)
        {
            if (char.IsWhiteSpace(character) || char.IsControl(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Canonical <c>scheme:identifier</c> form. Contains no secret material.</summary>
    public override string ToString() => $"{Scheme}:{Identifier}";
}

/// <summary>Marks the grammar validity of a raw reference without echoing it.</summary>
public static class SecretReferenceGrammar
{
    /// <summary>Returns whether <paramref name="value"/> is a well formed reference.</summary>
    public static bool IsWellFormed([NotNullWhen(true)] string? value) =>
        SecretReference.TryParse(value, out _);
}