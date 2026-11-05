# Security Policy

## Supported versions

Only the latest released minor version receives security fixes.

| Version | Supported |
|---|---|
| 1.x (latest) | Yes |
| older | No |

## Reporting a vulnerability

**Do not open a public issue for security problems.**

Report privately through GitHub's
[private vulnerability reporting](https://docs.github.com/en/code-security/security-advisories/guidance-on-reporting-and-writing/privately-reporting-a-security-vulnerability)
(Security → Advisories → *Report a vulnerability*).

Please include:

- Affected version or commit
- A description of the issue and its impact
- Steps to reproduce, or a proof of concept
- Any suggested mitigation

You can expect an acknowledgement within 7 days and a status update within 30 days.
Credit will be given in the advisory unless you prefer to remain anonymous.

## Deploying securely

This project is an API gateway: its security depends on how it is configured.
A default deployment is **not** hardened for internet exposure. Before going to
production, review the following.

### Required configuration

| Setting | Why |
|---|---|
| `AppSetting__AdminApi__SigningKey` | Without it, a random per-process key is used: admin sessions break on restart and are not shared across instances. Use at least 32 characters, injected through the environment |
| `AppSetting__AllowedCorsOrigins` | Leave it empty in production. Any non-empty value must list exact origins; wildcards are not supported |
| TLS termination | The container listens on plain HTTP. Terminate TLS in a reverse proxy or ingress, and never expose port 4000 directly to the internet |

### Never commit

The following are git-ignored and must stay out of the repository:

- `certs/`, `*.p12`, `*.pfx`, `*.pem` — signing certificates
- `identity.db*` — the SQLite identity database with password hashes
- `ocelot.json` — may contain internal hostnames
- Any non-empty `SecretKey` in `appsettings*.json`

The CI pipeline fails the build if a release artifact contains any of these.

### Plugin upload (admin panel)

`AppSetting:AdminApi:AllowPluginUpload` (default `true`) lets an authenticated
administrator upload and delete plugin assemblies from the Plugins page.

- **Any administrator can execute code in the gateway process.** A plugin is a managed
  assembly loaded with `Assembly.LoadFrom` and no isolation, so it runs with the gateway's
  identity, filesystem access and network reach. Treat the administrative role as
  equivalent to shell access on the host, and disable the feature
  (`AppSetting__AdminApi__AllowPluginUpload=false`) when it is not needed. In that mode the
  whole `/admin-api/plugins` endpoint is rejected with `403`, so a stolen admin token cannot
  reach it either.
- Uploads are validated before being stored (managed assembly, implements `IPlugin`,
  unique names, host assemblies refused, file name sanitized) and written through a
  staging folder, so an invalid or truncated file never replaces a working plugin. This
  protects availability, **not** integrity: validation cannot detect a malicious plugin.
- The feature is only reachable with a valid admin token and requires a writable plugins
  directory; with an immutable image or a read-only mount every upload is refused.
- Files copied into the plugins directory by other means keep working. They are loaded at
  startup; the panel only lists them.

### Known limitations

- **Plain HTTP by default.** TLS is expected to be handled by an upstream proxy.
- **SQLite for identity.** Suitable for a single instance; use an external database
  for horizontally scaled deployments.
- **Registration endpoint.** The first account created becomes Administrator.
  Deploy behind a trusted network or complete registration before exposing the
  service publicly.

## Scope

In scope: authentication bypass, privilege escalation, injection, SSRF through
gateway configuration, secret disclosure, denial of service.

Out of scope: findings that require a compromised host, missing hardening headers
with no demonstrated impact, and reports about third-party dependencies (report
those upstream).
