# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 1.0.x   | :white_check_mark: Current |

## Reporting a Vulnerability

**Email:** 64996768+mcp-tool-shop@users.noreply.github.com

1. **Do NOT** open a public issue for security vulnerabilities
2. Email the address above with a detailed description
3. Include steps to reproduce if applicable

### Response timeline

| Action | Target |
|--------|--------|
| Acknowledge report | 48 hours |
| Assess severity | 7 days |
| Release fix | 30 days |

## Scope

Deterministic Mouse Training Engine is a **local-first** .NET MAUI desktop game for cursor dexterity training.

- **Data accessed:** Mouse cursor coordinates (real-time during gameplay), MAUI local storage for session results and run history
- **Data NOT accessed:** No cloud sync. No telemetry. No analytics. No network calls. No authentication required
- **Permissions:** Standard .NET MAUI sandbox. File system for local data only. No elevated permissions required
- **No telemetry** is collected or sent
