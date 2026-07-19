using GenEngine.Configuration.Application;

namespace GenEngine.Services.Tests;

/// <summary>
/// The integrated help is only useful if it is exhaustive. These tests are the
/// detection mechanism the field catalogue promises: adding a field to the
/// configuration document without describing it fails here rather than shipping an
/// unlabelled input to an administrator.
/// </summary>
public sealed class ConfigurationFieldCatalogTests
{
    [Fact]
    public void EveryConfigurationFieldHasADescriptor()
    {
        IReadOnlyList<string> documentPaths = ConfigurationFieldCatalog.EnumerateDocumentPaths();
        HashSet<string> described = ConfigurationFieldCatalog.Descriptors.Select(static descriptor => descriptor.Path).ToHashSet(StringComparer.Ordinal);

        string[] missing = documentPaths.Where(path => !described.Contains(path)).ToArray();

        Assert.True(
            missing.Length == 0,
            $"Ces champs de configuration n'ont pas d'aide intégrée : {string.Join(", ", missing)}.");
    }

    [Fact]
    public void NoDescriptorDescribesAFieldThatNoLongerExists()
    {
        HashSet<string> documentPaths = ConfigurationFieldCatalog.EnumerateDocumentPaths().ToHashSet(StringComparer.Ordinal);

        string[] orphans = ConfigurationFieldCatalog.Descriptors
            .Select(static descriptor => descriptor.Path)
            .Where(path => !documentPaths.Contains(path))
            .ToArray();

        Assert.True(
            orphans.Length == 0,
            $"Ces descripteurs ne correspondent plus à aucun champ : {string.Join(", ", orphans)}.");
    }

    [Fact]
    public void DescriptorsArePubliableAndUniquelyKeyed()
    {
        Assert.Equal(
            ConfigurationFieldCatalog.Descriptors.Count,
            ConfigurationFieldCatalog.Descriptors.Select(static descriptor => descriptor.Path).Distinct(StringComparer.Ordinal).Count());
        Assert.All(ConfigurationFieldCatalog.Descriptors, static descriptor =>
        {
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Label));
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Description));
            Assert.False(string.IsNullOrWhiteSpace(descriptor.Example));
        });
    }

    [Fact]
    public void ADescriptorIsAddressableByItsFieldPath()
    {
        ConfigurationFieldDescriptor descriptor = Assert.IsType<ConfigurationFieldDescriptor>(ConfigurationFieldCatalog.Find("economy.currencyCode"));

        Assert.Equal("Code de la monnaie", descriptor.Label);
        Assert.Null(ConfigurationFieldCatalog.Find("economy.thereIsNoSuchField"));
    }
}