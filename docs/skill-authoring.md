# How to Write a HermesNET Skill

A skill is a reusable, well-defined task that HermesNET can execute.

## Skill Format

Every skill is a Markdown file with YAML front-matter:

```markdown
---
id: <namespace>/<skill-name>
name: <Human-Readable Name>
version: 1.0.0
description: <One-line description>
tags:
  - <category1>
  - <category2>
author: <Your Name>
license: MIT
---

# {Skill Name}

<Skill description and documentation>
```

## YAML Front-Matter Fields

| Field | Required | Type | Example |
|-------|----------|------|---------|
| `id` | Yes | String | `math/sum` |
| `name` | Yes | String | `Sum Numbers` |
| `version` | Yes | String | `1.0.0` |
| `description` | Yes | String | `Calculate the sum of numbers` |
| `tags` | Yes | Array | `["math", "arithmetic"]` |
| `author` | No | String | `Your Name` |
| `license` | No | String | `MIT` |

### ID Convention

Use a **namespace/skill-name** format:

- `math/sum` — math category, sum skill
- `text/summarize` — text category, summarize skill
- `system/disk-usage` — system category, disk usage skill
- `web/scrape-title` — web category, scrape title skill

The namespace helps organize skills into logical categories.

## Skill Body

After the YAML front-matter, write the skill documentation in Markdown.

### Recommended Sections

1. **Overview** — What does this skill do?
2. **Input** — What does the skill expect as input?
3. **Output** — What does the skill produce?
4. **Example(s)** — Real-world example with input/output
5. **Notes** — Edge cases, limitations, tips

### Example Template

```markdown
# Calculate Sum

Calculate the sum of multiple numbers.

## Input
Comma-separated numbers (e.g., `3, 5, 7`)

## Output
The sum as a single number (e.g., `15`)

## Examples

### Example 1: Positive integers
Input: `10, 20, 30`
Output: `60`

### Example 2: Negative numbers
Input: `-5, 10, 3`
Output: `8`

### Example 3: Decimals
Input: `1.5, 2.5, 3`
Output: `7`

## Notes
- Handles negative numbers
- Supports decimal values
- Returns a single number (integer or float)
```

## Registering Your Skill

Place your skill file in `config/skills/`:

```
HermesNET/
  config/
    skills/
      math-sum.md
      text-summarize.md
      custom-skill.md       ← Your new skill
```

When you run HermesNET, it automatically discovers and registers all skills in `config/skills/`.

## Testing Your Skill

1. **List skills:** `hermes skill list`
   - Should show your new skill

2. **View skill:** `hermes skill show math/sum`
   - Should display your full skill definition

3. **Use in chat:** Ask HermesNET to use your skill
   - Example: "Use the sum skill to calculate 5 + 10 + 15"

## Best Practices

1. **Be descriptive** — Write clear input/output descriptions
2. **Provide examples** — Real-world examples are invaluable
3. **Document edge cases** — What happens with empty input? Negative numbers?
4. **Keep it focused** — One skill = one well-defined task
5. **Use consistent formatting** — Follow the template above
6. **Version your skills** — Increment version when making changes

## Common Skill Categories

- `math/*` — Arithmetic, calculations
- `text/*` — Text processing, summarization
- `system/*` — System info, diagnostics
- `web/*` — Web scraping, HTTP requests
- `data/*` — JSON validation, CSV parsing
- `file/*` — File operations
- `code/*` — Code analysis, linting

## Sharing Your Skill

1. Create a Markdown file (`.md`)
2. Follow the format above
3. Place in `config/skills/`
4. Commit and push to your fork/branch
5. Submit a PR to contribute back to HermesNET

---

## Example: Step-by-Step

### Goal: Create a skill to convert Celsius to Fahrenheit

**Step 1: Create the file**
```
config/skills/math-celsius-to-fahrenheit.md
```

**Step 2: Write the skill**
```markdown
---
id: math/celsius-to-fahrenheit
name: Celsius to Fahrenheit Converter
version: 1.0.0
description: Convert temperature from Celsius to Fahrenheit
tags:
  - math
  - temperature
author: Your Name
license: MIT
---

# Celsius to Fahrenheit Converter

Convert a temperature value from Celsius to Fahrenheit scale.

## Formula
°F = (°C × 9/5) + 32

## Input
A temperature in Celsius (e.g., `0`, `25`, `-40`)

## Output
The equivalent temperature in Fahrenheit

## Examples

### Example 1: Room temperature
Input: `20` (Celsius)
Output: `68` (Fahrenheit)

### Example 2: Freezing point
Input: `0` (Celsius)
Output: `32` (Fahrenheit)

### Example 3: Boiling point
Input: `100` (Celsius)
Output: `212` (Fahrenheit)

## Notes
- Works with negative temperatures
- Returns integer (rounded)
```

**Step 3: Verify**
```bash
hermes skill show math/celsius-to-fahrenheit
```

Done! 🎉
