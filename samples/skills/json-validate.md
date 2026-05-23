---
id: data/json-validate
name: Validate JSON
version: 1.0.0
description: Validate JSON structure and report errors
tags:
  - data
  - validation
author: HermesNET Team
license: MIT
---

# JSON Validator

Validate JSON syntax and structure.

## Input
A JSON string or file content.

## Output
```json
{
  "valid": true,
  "errors": [],
  "prettified": "{...}"
}
```

If invalid:
```json
{
  "valid": false,
  "errors": [
    "Line 5: Unexpected token }",
    "Missing closing bracket"
  ]
}
```

## Example
Input: `{"name": "John", "age": 30}`

Output:
```json
{
  "valid": true,
  "errors": [],
  "prettified": "{\n  \"name\": \"John\",\n  \"age\": 30\n}"
}
```

## Notes
- Reports all validation errors, not just the first
- `prettified` field only present when `valid` is `true`
- Supports JSON5 comments with an optional flag
