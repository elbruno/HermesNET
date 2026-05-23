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
///   <item>M3 hook: skill versioning / namespacing strategy is deliberately deferred.
///         See <c>.squad/decisions/inbox/dallas-t17-skill-design.md</c>.</item>
/// </list>
/// </summary>
public sealed class SkillRegistryBootstrapper
{
    private readonly ISkillRegistry _registry;
    private readonly string         _skillsDirectory;

    /// <param name="registry">Registry to populate.</param>
    /// <param name="skillsDirectory">
    ///   Override the default <c>config/skills</c> directory.
    ///   If <c>null</c>, resolves to <c>{AppContext.BaseDirectory}/config/skills</c>.
    /// </param>
    public SkillRegistryBootstrapper(ISkillRegistry registry, string? skillsDirectory = null)
    {
        _registry        = registry ?? throw new ArgumentNullException(nameof(registry));
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

        await _registry.LoadFromDirectoryAsync(_skillsDirectory).ConfigureAwait(false);
    }
}
