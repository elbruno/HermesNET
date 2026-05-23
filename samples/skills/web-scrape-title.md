---
id: web/scrape-title
name: Scrape Web Page Title
version: 1.0.0
description: Extract the title and meta description from a webpage
tags:
  - web
  - scraping
author: HermesNET Team
license: MIT
---

# Web Page Title Scraper

Extract the title and meta description from a website.

## Input
A URL: `https://example.com`

## Output
```json
{
  "url": "https://example.com",
  "title": "Example Domain",
  "description": "Example website for demonstration purposes",
  "status": 200
}
```

## Example
Input: `https://www.github.com`

Output:
```json
{
  "url": "https://www.github.com",
  "title": "GitHub: Where the world builds software",
  "description": "GitHub is where over 100 million developers shape the future of software...",
  "status": 200
}
```

## Notes
- Requires network access
- Returns HTTP status code
- Handles redirects (follows final URL)
