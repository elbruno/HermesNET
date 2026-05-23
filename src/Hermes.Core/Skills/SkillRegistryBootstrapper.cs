namespace Hermes.Core.Skills;

/// <summary>
/// Discovers all <c>.md</c> skill files in <c>config/skills/</c> (relative to
/// <see cref="AppContext.BaseDirectory"/>) and loads them into the
/// <see cref="ISkillRegistry"/> at application startup.
///
/// <list type="bullet">
///   <item>If the skills directory does not exist, startup succeeds silently (no skills loaded).</item>
///   <item>Version collision policy: first file wins — a <see cref="DuplicateSkillException"/>
///         is thrown if two files share the same skill ID.</item>
///   <item>M3 T33: an optional <see cref="ISkillProvider"/> can be injected to decouple
///         discovery/loading from the registry. Falls back to
///         <see cref="ISkillRegistry.LoadFromDirectoryAsync"/> when not provided.</item>
///   <item>M4 hook: skill versioning / namespacing strategy is deliberately deferred.
///         See <c>.squad/decisions/inbox/dallas-t17-skill-design.md</c>.</item>
/// </list>
/// </summary>
public sealed class SkillRegistryBootstrapper
{
    private readonly ISkillRegistry  _registry;
    private readonly ISkillProvider? _provider;
    private readonly string          _skillsDirectory;

    /// <param name="registry">Registry to populate.</param>
    /// <param name="skillsDirectory">
    ///   Override the default <c>config/skills</c> directory.
    ///   If <c>null</c>, resolves to <c>{AppContext.BaseDirectory}/config/skills</c>.
    /// </param>
    public SkillRegistryBootstrapper(ISkillRegistry registry, string? skillsDirectory = null)
        : this(registry, provider: null, skillsDirectory)
    {
    }

    /// <param name="registry">Registry to populate.</param>
    /// <param name="provider">
    ///   Optional <see cref="ISkillProvider"/> used for discovery and loading.
    ///   When provided, the bootstrapper calls
    ///   <see cref="ISkillProvider.DiscoverAsync"/> + <see cref="ISkillProvider.LoadAsync"/>
    ///   and registers each result individually.
    ///   When <c>null</c>, falls back to
    ///   <see cref="ISkillRegistry.LoadFromDirectoryAsync"/>.
    /// </param>
    /// <param name="skillsDirectory">
    ///   Override the default <c>config/skills</c> directory.
    ///   If <c>null</c>, resolves to <c>{AppContext.BaseDirectory}/config/skills</c>.
    /// </param>
    public SkillRegistryBootstrapper(
        ISkillRegistry  registry,
        ISkillProvider? provider,
        string?         skillsDirectory = null)
    {
        _registry        = registry ?? throw new ArgumentNullException(nameof(registry));
        _provider        = provider;
        _skillsDirectory = skillsDirectory
            ?? Path.Combine(AppContext.BaseDirectory, "config", "skills");
    }

    /// <summary>The resolved skills directory path used by this bootstrapper.</summary>
    public string SkillsDirectory => _skillsDirectory;

    /// <summary>
    /// Scans <see cref="SkillsDirectory"/> for <c>.md</c> files and registers
    /// each one with the registry.  Safe to call multiple times (idempotent via
    /// <see cref="ISkillRegistry.LoadFromDirectoryAsync"/>).
    ///
    /// Returns immediately without error if the directory does not exist.
    /// </summary>
    public async Task BootstrapAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(_skillsDirectory))
            return;

        if (_provider is not null)
        {
            await BootstrapWithProviderAsync(_provider, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _registry.LoadFromDirectoryAsync(_skillsDirectory).ConfigureAwait(false);
        }
    }

    // ── Provider-based bootstrap path ─────────────────────────────────────────

    private async Task BootstrapWithProviderAsync(
        ISkillProvider  provider,
        CancellationToken cancellationToken)
    {
        var paths = await provider.DiscoverAsync(_skillsDirectory, cancellationToken)
                                  .ConfigureAwait(false);

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var definition = await provider.LoadAsync(path, cancellationToken)
                                           .ConfigureAwait(false);

            // M3: SkillDescriptor is the only ISkillDefinition implementation.
            // M4 MAF adapters will return their own ISkillDefinition types, at which
            // point ISkillRegistry.RegisterSkillAsync will be updated to accept ISkillDefinition.
            if (definition is SkillDescriptor descriptor)
                await _registry.RegisterSkillAsync(descriptor).ConfigureAwait(false);
        }
    }
}
