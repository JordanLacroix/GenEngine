namespace GenEngine.Secrets;

/// <summary>
/// Resolves <c>env:NAME</c> against the process environment. This is the local and CI
/// implementation: it reads nothing from disk, writes nothing, and works in the read-only
/// non-root container. Values are injected by Compose or the hosting platform.
/// </summary>
/// <remarks>
/// <c>NAME</c> must match <c>[A-Z_][A-Z0-9_]*</c>. The uppercase constraint is deliberate: it
/// keeps references portable across shells and prevents a reference from pointing at an
/// arbitrary lowercase host variable by accident.
/// </remarks>
public sealed class EnvironmentSecretResolver : ISecretResolver
{
    private readonly Func<string, string?> readVariable;

    /// <summary>Reads from the real process environment.</summary>
    public EnvironmentSecretResolver()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    /// <summary>Reads through <paramref name="readVariable"/>; used by tests to avoid real secrets.</summary>
    public EnvironmentSecretResolver(Func<string, string?> readVariable) =>
        this.readVariable = readVariable ?? throw new ArgumentNullException(nameof(readVariable));

    /// <summary>The <c>env</c> scheme.</summary>
    public const string SchemeName = "env";

    /// <inheritdoc />
    public string Scheme => SchemeName;

    /// <inheritdoc />
    public ValueTask<SecretResolution> ResolveAsync(SecretReference reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsValidVariableName(reference.Identifier))
        {
            return ValueTask.FromResult(SecretResolution.Failed(SecretResolutionFailure.MalformedReference));
        }

        string? value = readVariable(reference.Identifier);

        return ValueTask.FromResult(string.IsNullOrEmpty(value)
            ? SecretResolution.Failed(SecretResolutionFailure.NotFound)
            : SecretResolution.Success(new SecretValue(value)));
    }

    private static bool IsValidVariableName(ReadOnlySpan<char> name)
    {
        if (name.Length == 0 || (!char.IsAsciiLetterUpper(name[0]) && name[0] != '_'))
        {
            return false;
        }

        foreach (char character in name)
        {
            if (!char.IsAsciiLetterUpper(character) && !char.IsAsciiDigit(character) && character != '_')
            {
                return false;
            }
        }

        return true;
    }
}