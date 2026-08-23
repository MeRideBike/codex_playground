# Security Policy

## Supported versions

Security fixes are applied to the latest version on the `main` branch. This is a pre-1.0 project; no older release branches are currently supported.

## Reporting a vulnerability

Please **do not** open a public issue for a suspected vulnerability. Instead, use [GitHub's private vulnerability reporting form](https://github.com/MeRideBike/codex_playground/security/advisories/new). Include:

- A clear description of the impact.
- Steps to reproduce or a minimal proof of concept.
- Affected version, commit, or deployment details.
- Any suggested mitigation, if known.

We aim to acknowledge reports within 7 days and will work with reporters on a remediation and disclosure timeline. Please allow a fix to be released before publicly discussing a vulnerability.

## Security boundaries

Milestone 1 intentionally has no host-shell, repository, or network-tool capability and requires no external AI credentials. Future providers must use environment variables or .NET user secrets; credentials must never be committed. Any future tool capability requires a narrowly defined interface, explicit permission policy, audit record, tests, and a documented threat review.
