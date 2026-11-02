# Installing Ocelot Admin from GitHub Container Registry

Complete guide to deploying the gateway with the admin panel using the published
GHCR image. Written for first-time installers.

---

## Contents

1. [What you are installing](#1-what-you-are-installing)
2. [Prerequisites](#2-prerequisites)
3. [Pull the image](#3-pull-the-image)
4. [Quick start](#4-quick-start)
5. [First run: creating the admin account](#5-first-run-creating-the-admin-account)
6. [Files inside the container](#6-files-inside-the-container)
7. [Environment variables reference](#7-environment-variables-reference)
8. [Authentication providers](#8-authentication-providers)
9. [Plugins](#9-plugins)
10. [Docker Compose explained](#10-docker-compose-explained)
11. [Persistence and backups](#11-persistence-and-backups)
12. [Day-to-day operations](#12-day-to-day-operations)
13. [Troubleshooting](#13-troubleshooting)
14. [Production checklist](#14-production-checklist)

---

## 1. What you are installing

A single Linux container running:

- **An Ocelot API Gateway** that routes requests to your backend services using
  declarative JSON configuration.
- **A web admin panel** at `/admin` for editing routes, aggregates, authentication
  providers and plugins at runtime.
- **A SQLite database** holding the admin panel users, created automatically on first
  start.
- **A plugin system** that loads `.dll` files from a mounted directory.
- **A health endpoint** at `/health` for Docker and orchestrators.

No external database, message broker or cache is required.

### Request flow

```
┌──────────────┐        :4000        ┌─────────────────────────────┐
│   Browser    │ ──────────────────► │   Container ocelot-admin    │
│   or client  │                     │                             │
└──────────────┘                     │  Kestrel on :4000           │
                                     │    ├── /health      public  │
                                     │    ├── /admin       panel   │
                                     │    ├── /admin-api   API     │
                                     │    └── /*           routed  │
                                     │                             │
                                     │  /app/data/identity.db      │
                                     │  /app/ocelot.json           │
                                     │  /app/Plugins/*.dll         │
                                     └─────────────┬───────────────┘
                                                   │
                                          Downstream services
```

---

## 2. Prerequisites

| Requirement | Version | Check |
|-------------|---------|-------|
| Docker Engine | 20.10 or newer | `docker --version` |
| Docker Compose | v2 (plugin) | `docker compose version` |
| Outbound HTTPS | to `ghcr.io` | — |

The image is **multi-architecture** (`linux/amd64` and `linux/arm64`), so it runs on
standard x86 servers, Apple Silicon Macs and 64-bit Raspberry Pi boards alike.

> **Windows users:** run these commands from WSL 2 or Git Bash. The examples use
> POSIX shell syntax.

---

## 3. Pull the image

The image is public. Pulling does not require authentication:

```bash
docker pull ghcr.io/asielh1n1/ocelot-admin:1.0.0
```

If you get `denied` or `unauthorized`, authenticate with a GitHub personal access
token that has the `read:packages` scope:

```bash
echo "$GITHUB_TOKEN" | docker login ghcr.io -u YOUR_GITHUB_USERNAME --password-stdin
```

Check which version you have:

```bash
docker image inspect ghcr.io/asielh1n1/ocelot-admin:1.0.0 \
  --format '{{index .Config.Labels "org.opencontainers.image.version"}}'
```

> **Always pin a specific tag in production.** With `latest`, a routine
> `docker compose pull` can upgrade you unexpectedly.

---

## 4. Quick start

### Step 1 — Create a working directory

```bash
mkdir -p ocelot-admin && cd ocelot-admin
mkdir -p Plugins data
```

### Step 2 — Create the `.env` file

This file holds values you do not want to commit to git.

```bash
cat > .env <<'EOF'
# Admin JWT signing key. Minimum 32 characters.
# Leave empty for a single instance; a random per-process key is generated.
ADMIN_SIGNING_KEY=
APP_NAME=Ocelot Admin
PRIMARY_COLOR=#03a9f4
ASPNETCORE_ENVIRONMENT=Production
EOF
```

Generate a strong key if you plan to run more than one replica:

```bash
openssl rand -base64 48
```

Paste the output as the value of `ADMIN_SIGNING_KEY`, then protect the file:

```bash
chmod 600 .env
```

### Step 3 — Create `docker-compose.yml`

```yaml
services:
  gateway:
    image: ghcr.io/asielh1n1/ocelot-admin:1.0.0
    container_name: ocelot-admin
    restart: unless-stopped
    ports:
      - "4000:4000"
    environment:
      AppSetting__AdminApi__SigningKey: ${ADMIN_SIGNING_KEY:-}
      AppSetting__AdminApi__AppName: ${APP_NAME:-Ocelot Admin}
      AppSetting__AdminApi__PrimaryColor: ${PRIMARY_COLOR:-#03a9f4}
      ConnectionStrings__DefaultConnection: "Data Source=/app/data/identity.db"
      ASPNETCORE_ENVIRONMENT: ${ASPNETCORE_ENVIRONMENT:-Production}
    volumes:
      - ocelot-identity:/app/data
      - ./Plugins:/app/Plugins:ro
    healthcheck:
      test: ["CMD", "wget", "--no-verbose", "--tries=1", "--spider", "http://localhost:4000/health"]
      interval: 30s
      timeout: 5s
      retries: 3
      start_period: 20s
    logging:
      driver: json-file
      options:
        max-size: "10m"
        max-file: "3"

volumes:
  ocelot-identity:
```

### Step 4 — Start the container

```bash
docker compose up -d
docker compose logs -f gateway
```

Wait for these two lines:

```json
{"LogLevel":"Information","Category":"Microsoft.Hosting.Lifetime","Message":"Now listening on: http://[::]:4000"}
{"LogLevel":"Information","Category":"Microsoft.Hosting.Lifetime","Message":"Application started."}
```

Press `Ctrl+C` to stop following the logs. The container keeps running.

### Step 5 — Verify

```bash
curl http://localhost:4000/health
```

Expected response:

```json
{"status":"healthy","version":"1.0.0"}
```

Now open <http://localhost:4000/admin> in a browser.

---

## 5. First run: creating the admin account

On the very first start the panel detects that no users exist and shows a **setup
screen**. This is the only moment when you can create an account from the UI.

| Field | Required | Notes |
|-------|----------|-------|
| Full name | Yes | Display name for the administrator. |
| Email | Yes | Also used as the login username. |
| Password | Yes | Minimum 8 characters, with an uppercase letter, a lowercase letter and a digit. |

Creating the account assigns the `Administrator` role and returns a session token.

> **Registration closes permanently** as soon as one user exists. After that,
> `POST /admin-api/register` responds `400 registration_disabled`. This is a
> deliberate security measure and cannot be disabled from the panel.

### Recovering access

There is no password reset by email. If you lose the credentials, use the built-in CLI
inside the container:

```bash
# Create an additional administrator
docker exec -it ocelot-admin dotnet ApiGateway.dll add

# Change a password
docker exec -it ocelot-admin dotnet ApiGateway.dll set-password

# List users
docker exec -it ocelot-admin dotnet ApiGateway.dll list

# Remove a user
docker exec -it ocelot-admin dotnet ApiGateway.dll remove
```

Each command prompts for the values interactively.

### How credentials are stored

Passwords are hashed with ASP.NET Identity (PBKDF2 with a high iteration count).
Session tokens are HMAC-SHA256 signed JWTs.

---

## 6. Files inside the container

| Path | Contents | Survives restart | Survives recreation |
|------|----------|------------------|---------------------|
| `/app` | Binaries and `appsettings.json` | Yes | No — recreated from the image |
| `/app/data/identity.db` | Users and roles | Yes | **Only with a volume** |
| `/app/ocelot.json` | Gateway routes and configuration | Yes | **No by default** |
| `/app/Plugins/*.dll` | Loaded plugins | Yes | Only with a mount |

### About `ocelot.json`

This is the file the admin panel edits. It is **not shipped inside the image**: if it is
missing, the application generates an empty one at startup.

That means **without an extra volume you will lose all routes when the container is
recreated** — a `docker compose down` followed by `up`, or an image upgrade.

To persist it, add a second volume and point the app at it:

```yaml
    volumes:
      - ocelot-identity:/app/data
      - ocelot-config:/app/config
      - ./Plugins:/app/Plugins:ro
    environment:
      AppSetting__OcelotConfigPath: /app/config/ocelot.json
```

> If your version does not support `AppSetting__OcelotConfigPath`, bind-mount the file
> onto the default location instead. It must exist before the container starts:
>
> ```yaml
>       - ./data/ocelot.json:/app/ocelot.json
> ```

---

## 7. Environment variables reference

ASP.NET Core reads configuration from environment variables using `__` (double
underscore) as the section separator. `AppSetting__AdminApi__SigningKey` is the same key
as `AppSetting:AdminApi:SigningKey` in `appsettings.json`.

### 7.1 Admin panel — `AppSetting__AdminApi__*`

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `AppSetting__AdminApi__Enable` | `true` / `false` | `true` | Enables the panel and the `/admin-api` endpoints. With `false` those paths return `404`. |
| `AppSetting__AdminApi__SigningKey` | string | empty | HMAC key used to sign panel JWTs. **Minimum 32 characters.** See below. |
| `AppSetting__AdminApi__AppName` | string | `Ocelot Admin` | Title shown in the panel and in the browser tab. |
| `AppSetting__AdminApi__PrimaryColor` | CSS color | `#03a9f4` | Accent color, injected into the frontend when `index.html` is served. Accepts `#rgb`, `#rrggbb` or a CSS color name. |
| `AppSetting__AdminApi__LogoUrl` | URL | `~/assets/logo.svg` | Panel logo. Relative path (`~/assets/...`) or absolute URL. |

#### About `SigningKey`

- **Empty** — the application generates a random 512-bit key in memory.
  - Suitable for a **single instance**.
  - Sessions are invalidated on every restart.
  - Does **not** work with multiple replicas: each would sign with a different key.
- **Set** — must be **32 characters or longer**, otherwise startup fails with:

  ```
  AppSetting:AdminApi:SigningKey must be at least 32 characters long,
  or left empty to use a random per-process key.
  ```

- Never hardcode it in `docker-compose.yml`. Use `.env` and add that file to
  `.gitignore`.

### 7.2 CORS — `AppSetting__AllowedCorsOrigins__*`

Allowed origins for cross-origin requests to the panel.

| Variable | Example |
|----------|---------|
| `AppSetting__AllowedCorsOrigins__0` | `https://admin.example.com` |
| `AppSetting__AllowedCorsOrigins__1` | `https://other.example.com` |

Behavior:

| Situation | Result |
|-----------|--------|
| Empty list, `ASPNETCORE_ENVIRONMENT=Production` | **No external origin is allowed.** The panel only works when accessed from the same host. |
| Empty list, `ASPNETCORE_ENVIRONMENT=Development` | Any origin is allowed. |
| List with entries | Only those origins are allowed. |

If you serve the panel from a different domain, you **must** declare it:

```yaml
    environment:
      AppSetting__AllowedCorsOrigins__0: "https://admin.example.com"
```

### 7.3 Host binding

| Variable | Image default | Description |
|----------|---------------|-------------|
| `ASPNETCORE_URLS` | `http://+:4000` | Listen address. Must match `EXPOSE 4000`. Change only if you know what you are doing. |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Runtime environment. **Do not use `Development` in production** — it enables open CORS and verbose logging. |

> A `Kestrel:Endpoints` block in `appsettings.json` **takes precedence** over
> `ASPNETCORE_URLS`. The image ships that block pinned to `http://*:4000`, so the gateway
> always listens on **4000** inside the container, whether it is started with
> `dotnet run`, `dotnet ApiGateway.dll` or Docker.
>
> To use a different internal port, override the Kestrel value and `EXPOSE` together:
>
> ```bash
> docker run -p 5000:5000 \
>   -e Kestrel__Endpoints__Http__Url="http://+:5000" \
>   ghcr.io/asielh1n1/ocelot-admin:latest
> ```
>
> Changing only `ASPNETCORE_URLS` has no effect while the Kestrel block exists.

### 7.4 Database

| Variable | Default | Description |
|----------|---------|-------------|
| `ConnectionStrings__DefaultConnection` | `Data Source=identity.db` | SQLite connection string. In the container it should point to `/app/data/identity.db` so it lands inside the volume. |

The database is created and migrated automatically at startup. There is no manual step.

### 7.5 Logging

| Variable | Example | Description |
|----------|---------|-------------|
| `Logging__LogLevel__Default` | `Warning` | Global level. Values: `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`. |
| `Logging__LogLevel__ApiGateway` | `Information` | Level for the application code only. |
| `Logging__LogLevel__Ocelot` | `Warning` | Level for the Ocelot library. `Debug` is extremely verbose. |
| `Logging__LogLevel__Microsoft.EntityFrameworkCore` | `Warning` | `Information` logs every SQL statement. |

Container output is structured JSON, ready for Loki, ELK or CloudWatch.

### 7.6 Plugins — `PluginMetadata__*`

Each plugin reads its own subsection. The name must match the plugin's `Name`
property, or its class name as a fallback.

```yaml
    environment:
      PluginMetadata__MyPlugin__HeaderName: "X-Custom"
      PluginMetadata__MyPlugin__HeaderValue: "value"
```

This is equivalent to:

```json
{
  "PluginMetadata": {
    "MyPlugin": { "HeaderName": "X-Custom", "HeaderValue": "value" }
  }
}
```

---

## 8. Authentication providers

Ocelot requires that **every provider referenced by a route is registered**, otherwise
the gateway refuses to start. The list lives in `AppSetting:AuthenticationProviders`.

### The typical startup failure

```
Unable to start Ocelot, errors are:
FileValidationFailedError: ... 'JWT' is unsupported authentication provider
```

It means a route requests the provider `JWT`, but `AppSetting:AuthenticationProviders`
is empty. Two fixes:

1. Declare the provider (below).
2. Remove `AuthenticationOptions.AuthenticationProviderKeys` from that route.

### Provider fields

| Field | Applies to | Description |
|-------|------------|-------------|
| `Key` | all | Scheme name. This is the value routes reference. **Required.** |
| `Type` | all | `jwt`, `oidc` or `oauth2`. **Required.** |
| `SecretKey` | `jwt` | Symmetric HMAC secret. |
| `Issuer` | `jwt` | Expected issuer. If empty, issuer is not validated. |
| `Audience` | `jwt` / `oidc` | Expected audience. If empty, audience is not validated. |
| `ValidateLifetime` | `jwt` / `oidc` | Validates `exp` and `nbf`. Keep it `true`. |
| `CertificatePath` | `jwt` | Path to a `.pfx` / `.pem` with a private key. Alternative to `SecretKey`. |
| `CertificatePassword` | `jwt` | Certificate password, if any. |
| `CertificateThumbprint` | `jwt` | Loads the certificate from the `LocalMachine/My` store. |
| `JwksUri` | `jwt` | JWKS endpoint. Keys are cached for 1 hour. |
| `Authority` | `oidc` | OIDC provider URL, used to discover the JWKS. |
| `ClientId` | `oidc` | Client ID used for audience validation. |

### Example: single symmetric JWT provider

```yaml
    environment:
      AppSetting__AuthenticationProviders__0__Key: "JWT"
      AppSetting__AuthenticationProviders__0__Type: "jwt"
      AppSetting__AuthenticationProviders__0__SecretKey: "${JWT_SECRET}"
      AppSetting__AuthenticationProviders__0__Issuer: "https://auth.example.com"
      AppSetting__AuthenticationProviders__0__Audience: "my-api"
      AppSetting__AuthenticationProviders__0__ValidateLifetime: "true"
```

### Example: asymmetric JWKS (no shared secret)

```yaml
      AppSetting__AuthenticationProviders__0__Key: "JWT"
      AppSetting__AuthenticationProviders__0__Type: "jwt"
      AppSetting__AuthenticationProviders__0__JwksUri: "https://auth.example.com/.well-known/jwks.json"
```

### Example: OIDC (Keycloak, Auth0, Entra ID, …)

```yaml
      AppSetting__AuthenticationProviders__0__Key: "IdentityServer"
      AppSetting__AuthenticationProviders__0__Type: "oidc"
      AppSetting__AuthenticationProviders__0__Authority: "https://login.example.com"
      AppSetting__AuthenticationProviders__0__ClientId: "my-client"
      AppSetting__AuthenticationProviders__0__Audience: "my-api"
      AppSetting__AuthenticationProviders__0__ValidateLifetime: "true"
```

### Certificates in production

With `CertificatePath`, mount the certificate into the container:

```yaml
    volumes:
      - ./certs:/app/certs:ro
    environment:
      AppSetting__AuthenticationProviders__0__CertificatePath: "/app/certs/signing.pfx"
      AppSetting__AuthenticationProviders__0__CertificatePassword: "${CERT_PASSWORD}"
```

> The container runs as a **non-root user**. A certificate with `600` permissions owned
> by root will fail to load. Adjust the owner, or use `640` with a shared group.

---

## 9. Plugins

Plugins are `DelegatingHandler` implementations of `IPlugin` from the
`ocelot-admin-plugin` package. They load **at startup**, so adding one requires a restart.

### Installing a plugin

```bash
# 1. Build the plugin on the host
dotnet build -c Release MyPlugin.csproj

# 2. Copy ONLY the plugin DLL into ./Plugins
cp bin/Release/net10.0/MyPlugin.dll ./Plugins/

# 3. Add its settings to .env
#    PluginMetadata__MyPlugin__MyKey: "value"

# 4. Restart so it is loaded
docker compose restart gateway
```

### Critical rule

**Never copy `ApiGateway.Contracts.dll` into `Plugins/`.** The host already has it
loaded; a second copy creates two types with the same name and every `IsAssignableFrom`
check fails. The plugin loads but is silently ignored.

In the plugin's `.csproj`:

```xml
<PackageReference Include="ocelot-admin-plugin" Version="1.0.0">
  <ExcludeAssets>runtime</ExcludeAssets>
  <Private>false</Private>
</PackageReference>
```

### Verifying the load

```bash
docker compose logs gateway | grep -i plugin
```

You should see:

```
Loaded plugin MyPlugin (MyPluginHandler) v1.0.0.0, global: False
```

If you see `does not implement IPlugin`, the plugin was compiled against an older
contract version.

### Instance state

Ocelot creates its own handler instances to serve requests. The instance that receives
`InitializeAsync` **is not the same** one that handles traffic. Any shared state must be
`static`.

### Read-only mount

```yaml
      - ./Plugins:/app/Plugins:ro
```

`:ro` prevents the container from modifying your DLLs. If the directory does not exist
on the host, Docker creates it empty — that is not an error.

---

## 10. Docker Compose explained

Line by line, for the compose file in [§4](#4-quick-start):

| Key | Value | Why |
|-----|-------|-----|
| `image` | `ghcr.io/asielh1n1/ocelot-admin:1.0.0` | Pinned version. Avoids surprise upgrades from `latest`. |
| `restart` | `unless-stopped` | Restarts after crashes and host reboots, but respects a manual `docker compose stop`. |
| `ports` | `"4000:4000"` | Publishes host port 4000 to container port 4000. Change only the left side. |
| `healthcheck` | `wget --spider /health` | Docker marks the container `unhealthy` if it fails. `wget` exists in the image; `curl` does not. |
| `logging` | `json-file`, `10m`, `3` | Rotates logs, capped at 30 MB total. Without it a chatty container fills the disk. |

### Changing the host port

```yaml
    ports:
      - "8081:4000"      # http://localhost:8081
```

If you put a reverse proxy in front, bind to loopback so the port is not exposed
publicly:

```yaml
    ports:
      - "127.0.0.1:8081:4000"
```

### Building instead of pulling

```yaml
    build:
      context: .
      dockerfile: Dockerfile
    image: ocelot-admin:local
```

Useful for development. In production prefer the published image, which is built by CI
with the tests passing.

---

## 11. Persistence and backups

### What to persist

| Data | Path | Volume |
|------|------|--------|
| Users and roles | `/app/data/identity.db` | `ocelot-identity` |
| Gateway routes | `/app/ocelot.json` | ⚠️ **not persisted by default** |
| Plugins | `/app/Plugins` | bind mount |

If you edit routes from the panel, **add a volume for `ocelot.json`** or you will lose
them when the container is recreated. See [§6](#6-files-inside-the-container).

### Database backup (online)

SQLite does not require stopping the container when using its backup API:

```bash
docker exec ocelot-admin \
  sqlite3 /app/data/identity.db ".backup '/app/data/backup.db'"

docker cp ocelot-admin:/app/data/backup.db ./identity-$(date +%F).db
docker exec ocelot-admin rm /app/data/backup.db
```

### Full volume backup (offline)

```bash
docker compose stop gateway
docker run --rm -v ocelot-identity:/data -v "$PWD:/backup" alpine \
  tar czf /backup/identity-$(date +%F).tar.gz -C /data .
docker compose start gateway
```

### Restore

```bash
docker compose down
docker run --rm -v ocelot-identity:/data -v "$PWD:/backup" alpine \
  sh -c "rm -rf /data/* && tar xzf /backup/identity-2026-10-21.tar.gz -C /data"
docker compose up -d
```

---

## 12. Day-to-day operations

### Logs

```bash
docker compose logs -f gateway            # follow live
docker compose logs --tail=200 gateway    # last 200 lines
docker compose logs gateway | grep -i error
```

### Health

```bash
docker compose ps
```

| Status | Meaning |
|--------|---------|
| `healthy` | `/health` returns 200. |
| `starting` | Still inside `start_period` (20 s). |
| `unhealthy` | The healthcheck failed 3 times. Check the logs. |

```bash
curl -s http://localhost:4000/health
# {"status":"healthy","version":"1.0.0"}
```

### Upgrading to a new version

```bash
# 1. Back up first
docker compose stop gateway
docker run --rm -v ocelot-identity:/data -v "$PWD:/backup" alpine \
  tar czf /backup/identity-pre-update.tar.gz -C /data .

# 2. Change the image tag in docker-compose.yml (e.g. to 1.0.2)

# 3. Pull and recreate
docker compose pull
docker compose up -d

# 4. Verify
docker compose logs -f gateway
curl http://localhost:4000/health
```

Database migrations are applied automatically at startup.

### Restart without losing anything

```bash
docker compose restart gateway
```

---

## 13. Troubleshooting

### The container restarts in a loop

```bash
docker compose logs --tail=50 gateway
```

| Message | Cause | Fix |
|---------|-------|-----|
| `SigningKey must be at least 32 characters long` | Key too short | Use 32+ characters, or leave it empty |
| `is unsupported authentication provider` | A route references an unregistered provider | Declare it under `AppSetting__AuthenticationProviders__N__*` |
| `Unable to find a route` | Not an error: a request matched no route | Check the route's `UpstreamPathTemplate` |
| `Permission denied` on write | Volume owned by root while the container is non-root | Recreate the volume, or fix ownership |

### `dependency failed to start: container is unhealthy`

The healthcheck never passed. Usually one of two things:

1. The internal port changed (`ASPNETCORE_URLS`) but the healthcheck still targets 4000.
2. The application fails to start due to a configuration error. Read the logs.

### The panel is not reachable at `/admin`

| Check | Command |
|-------|---------|
| The container is running | `docker compose ps` |
| The published port is correct | `docker compose port gateway 4000` |
| `AdminApi:Enable` is not `false` | `docker exec ocelot-admin env \| grep AdminApi` |
| You are using the exact path | `/admin` works; so do `/admin/` and `/Admin` |

### `401 Unauthorized` on `/admin-api/data`

Expected without a session. The panel obtains a token on login. If you are calling the
API manually, include the header:

```bash
curl -H "Authorization: Bearer $TOKEN" http://localhost:4000/admin-api/data
```

### CORS errors in the browser

Declare the panel's origin:

```yaml
    environment:
      AppSetting__AllowedCorsOrigins__0: "http://localhost:4000"
```

### Registration returns `registration_disabled`

At least one user already exists. Initial registration is available only once. Create
users from the CLI:

```bash
docker exec -it ocelot-admin dotnet ApiGateway.dll add
```

### Routes disappeared after `docker compose up -d`

`ocelot.json` was not persisted. See [§6](#6-files-inside-the-container).

### Inspecting the effective configuration

```bash
docker exec ocelot-admin cat /app/appsettings.json
docker exec ocelot-admin cat /app/ocelot.json
docker exec ocelot-admin env | grep -E '^(AppSetting|PluginMetadata|ConnectionStrings)'
```

---

## 14. Production checklist

- [ ] Image tag pinned (`1.0.0`), not `latest`.
- [ ] `ASPNETCORE_ENVIRONMENT=Production`.
- [ ] `AppSetting__AdminApi__SigningKey` with 32+ characters **if you run more than one replica**.
- [ ] The key lives in `.env`, and `.env` is listed in `.gitignore`.
- [ ] `AppSetting__AllowedCorsOrigins__N` set to the real panel origins.
- [ ] A persistent volume for `/app/data`.
- [ ] A volume or bind mount for `ocelot.json` if routes are edited from the panel.
- [ ] Automated backup of `identity.db`.
- [ ] A reverse proxy providing TLS in front (the container serves plain HTTP).
- [ ] Log rotation configured (included in the suggested compose file).
- [ ] Healthcheck enabled and monitored.
- [ ] Administrator account created, password stored in a password manager.
- [ ] Initial registration verified as closed: `POST /admin-api/register` → `400`.

---

## Appendix A — Running without Docker Compose

```bash
docker run -d \
  --name ocelot-admin \
  --restart unless-stopped \
  -p 4000:4000 \
  -e AppSetting__AdminApi__SigningKey="your-key-of-at-least-32-characters" \
  -e AppSetting__AdminApi__AppName="My Gateway" \
  -e ConnectionStrings__DefaultConnection="Data Source=/app/data/identity.db" \
  -v ocelot-identity:/app/data \
  -v "$PWD/Plugins:/app/Plugins:ro" \
  ghcr.io/asielh1n1/ocelot-admin:1.0.0
```

Verify:

```bash
docker logs -f ocelot-admin
curl http://localhost:4000/health
```

Stop and remove (keeping the volume):

```bash
docker stop ocelot-admin && docker rm ocelot-admin
```

Remove everything, including data:

```bash
docker rm -f ocelot-admin
docker volume rm ocelot-identity
```

---

## Appendix B — Building the image locally

If you prefer to build from source instead of pulling:

```bash
git clone https://github.com/asielh1n1/ocelot-admin.git
cd ocelot-admin

docker build -t ocelot-admin:local .
docker run -d --name ocelot-admin -p 4000:4000 \
  -v ocelot-identity:/app/data \
  ocelot-admin:local
```

The Dockerfile is multi-stage: it builds the frontend and the .NET project, then copies
only the published output into a runtime image running as a non-root user.

---

## Appendix C — Available image tags

| Tag | Meaning |
|-----|---------|
| `1.0.0`, `1.0.1`, … | Immutable release builds. **Recommended.** |
| `latest` | Most recent stable release. Moves without notice. |
| `main` | Tip of the main branch. Unstable; for testing only. |

List published tags with the GitHub CLI:

```bash
gh api /user/packages/container/ocelot-admin/versions \
  --jq '.[].metadata.container.tags[]'
```
