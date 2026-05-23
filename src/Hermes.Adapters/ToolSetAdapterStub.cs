using Hermes.Core.Skills;

namespace Hermes.Adapters;

/// <summary>
/// Pass-through stub implementation of <see cref="IToolSetAdapter"/>.
/// No MAF dependency — M4 will provide the real implementation.
/// </summary>
public sealed class ToolSetAdapterStub : IToolSetAdapter
{
    /// <inheritdoc/>
    public string ToMafToolName(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            throw new ArgumentException("Skill ID must not be empty.", nameof(skillId));

        // Normalise: lowercase, replace non-alphanumeric (except dash) with dash
        return System.Text.RegularExpressions.Regex
            .Replace(skillId.ToLowerInvariant(), @"[^a-z0-9\-]", "-")
            .Trim('-');
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MafToolDescriptor>> ProjectToolSetAsync(
        ISkillRegistry registry,
        CancellationToken cancellationToken = default)
    {
        var skills = await registry.ListSkillsAsync().ConfigureAwait(false);
        return skills.Select(ToMafToolDescriptor).ToList();
    }

    /// <inheritdoc/>
    public MafToolDescriptor ToMafToolDescriptor(SkillDescriptor skill)
    {
        return new MafToolDescriptor
        {
            ToolName = ToMafToolName(skill.Id ?? skill.Name),
            Description = skill.Description,
            SourceSkillId = skill.Id ?? skill.Name,
            Category = skill.Category,
            Metadata = skill.Metadata ?? new Dictionary<string, string>()
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> ValidateToolSetMappingAsync(
        ISkillRegistry registry,
        CancellationToken cancellationToken = default)
    {
        var skills = await registry.ListSkillsAsync().ConfigureAwait(false);
        var failures = new List<string>();

        foreach (var skill in skills)
        {
            var id = skill.Id ?? skill.Name;
            if (string.IsNullOrWhiteSpace(id))
                failures.Add("(unnamed skill)");
            else if (string.IsNullOrWhiteSpace(skill.Description))
                failures.Add(id);
        }

        return failures;
    }
}
