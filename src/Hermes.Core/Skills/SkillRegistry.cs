using System.Collections.Concurrent;
using System.Text;

namespace Hermes.Core.Skills;

/// <summary>
/// In-memory, file-backed skill registry backed by a <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// for O(1) lookup by skill ID.
///
/// <list type="bullet">
///   <item>Skills are keyed by their ID (case-insensitive).</item>
///   <item>Loading is idempotent — files already loaded are skipped on subsequent calls.</item>
///   <item>Only <c>.md</c> files are processed; all other extensions are ignored.</item>
///   <item>Duplicate skill IDs across files throw <see cref="DuplicateSkillException"/>.</item>
///   <item>Skills with schema major version ≠ 1 are loaded but produce a <see cref="LoadWarnings"/> entry.</item>
/// </list>
/// </summary>
public sealed class SkillRegistry : ISkillRegistry
{
    private const int SupportedSchemaMajor = 1;

    private readonly ConcurrentDictionary<string, SkillDescriptor> _skills
        = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _loadedFiles
        = new(StringComparer.OrdinalIgnoreCase);

    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly List<string> _warnings  = [];
    private readonly MarkdownSkillParser _parser = new();

    // ── ISkillRegistry ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public IReadOnlyList<string> LoadWarnings => _warnings.AsReadOnly();

    /// <inheritdoc/>
    public Task<SkillDescriptor> GetSkillAsync(string skillId)
    {
        if (_skills.TryGetValue(skillId, out var skill))
            return Task.FromResult(skill);

        throw new KeyNotFoundException($"Skill '{skillId}' not found in registry.");
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<SkillDescriptor>> ListSkillsAsync()
        => Task.FromResult<IReadOnlyList<SkillDescriptor>>([.. _skills.Values]);

    /// <inheritdoc/>
    public Task<SkillDescriptor?> FindByNameAsync(string name)
    {
        var found = _skills.Values.FirstOrDefault(s =>
            string.Equals(s.Id,          name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s.Name,        name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(s.Description, name, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult<SkillDescriptor?>(found);
    }

    /// <inheritdoc/>
    public Task<SkillValidationResult> ValidateAsync(string skillId)
    {
        if (!_skills.TryGetValue(skillId, out var skill))
            return Task.FromResult(
                new SkillValidationResult([$"Skill '{skillId}' not found in registry."], []));

        var errors   = new List<string>();
        var warnings = new List<string>();

        if (skill.SchemaVersion is null)
        {
            errors.Add("Missing schema version.");
        }
        else if (skill.SchemaVersion.Major != SupportedSchemaMajor)
        {
            errors.Add(
                $"Unsupported schema version {skill.SchemaVersion}. " +
                $"Expected major version {SupportedSchemaMajor}.");
        }

        return errors.Count == 0 && warnings.Count == 0
            ? Task.FromResult(SkillValidationResult.Success)
            : Task.FromResult(new SkillValidationResult(errors, warnings));
    }

    /// <inheritdoc/>
    public async Task LoadFromDirectoryAsync(string skillsDirectory)
    {
        if (!Directory.Exists(skillsDirectory))
            throw new DirectoryNotFoundException(
                $"Skills directory not found: {skillsDirectory}");

        await _loadLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var files = Directory.GetFiles(skillsDirectory, "*.md", SearchOption.TopDirectoryOnly);

            foreach (var filePath in files)
            {
                // Idempotency: skip files that were already loaded.
                if (_loadedFiles.Contains(filePath))
                    continue;

                var content    = await File.ReadAllTextAsync(filePath, Encoding.UTF8)
                                           .ConfigureAwait(false);
                var descriptor = _parser.Parse(content); // throws SkillParseException on bad input

                var skillId = descriptor.Id ?? descriptor.Name;

                if (_skills.ContainsKey(skillId))
                    throw new DuplicateSkillException(skillId);

                // Warn about unexpected schema major version but still load the skill.
                if (descriptor.SchemaVersion?.Major != SupportedSchemaMajor)
                {
                    _warnings.Add(
                        $"Skill '{skillId}' has unexpected schema version " +
                        $"{descriptor.SchemaVersion} (expected major {SupportedSchemaMajor}). " +
                        "Loaded with warning.");
                }

                _skills[skillId] = descriptor;
                _loadedFiles.Add(filePath);
            }
        }
        finally
        {
            _loadLock.Release();
        }
    }
}
