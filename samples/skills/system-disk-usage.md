---
id: system/disk-usage
name: Check Disk Usage
version: 1.0.0
description: Get disk usage statistics for the current system
tags:
  - system
  - diagnostics
author: HermesNET Team
license: MIT
---

# System Disk Usage

Report disk usage statistics.

## Input
Optional: specific drive or path (e.g., `C:\`, `/home`)

## Output
Table showing:
- Drive/Partition
- Total size
- Used space
- Free space
- Percentage used

## Example Output
```
Drive: C:\
Total: 500 GB
Used: 350 GB (70%)
Free: 150 GB (30%)
```

## Notes
- Returns all drives if no path specified
- Useful for monitoring storage capacity
- Helps identify disk full warnings
