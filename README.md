# Ocelot Admin
Administration for [Ocelot API Gateway](https://github.com/threemammals/ocelot)

## Installation & Setup for Development
These steps are for getting the project up and running on your local machine.

Clone repository
```
git clone https://github.com/asielh1n1/ocelot-admin.git
```

Navigate to the folder
```
cd /ocelot-admin
```

Execute the project
```
dotnet run
```

Open the browser to the URL
```
http://localhost:4000/admin
```

On first run there are no users, so the admin panel shows the **registration** form.
The first account created becomes `Administrator` and registration is disabled
permanently afterwards. Use the CLI (see *User management*) if you need more accounts.

### API-Gateway-UI (api-gateway-ui)

Graphical interface for managing the Ocelot API-Gateway

#### Navigate to the folder
```
cd /front
```

#### Install the dependencies
```bash
yarn
# or
npm install
```

#### Start the app in development mode (hot-code reloading, error reporting, etc.)
```bash
quasar dev
```

## Admin JWT signing key

The admin panel authenticates with a JWT issued by the backend. The signing key is
taken from `AppSetting:AdminApi:SigningKey`. There are only two possible cases:

1. **`SigningKey` is set** → that key is used to sign and validate admin tokens. Required
   for multi-instance deployments.
2. **`SigningKey` is empty** → a cryptographically random 512-bit key is generated at
   startup (in memory only). This is the default and is fine for a single instance, but
   the key is per-process: sessions are invalidated on restart and tokens are not shared
   with other instances.

### Single instance (default)

Leave `SigningKey` empty. A random key is generated in memory at startup and is never
written to disk. This is the safest option for a single instance, but note that:

- Every restart invalidates all admin sessions (users must log in again).
- The key is per-process, so it cannot be shared with other instances.

### Multiple instances (load balancer, Kubernetes, Docker Swarm)

Every instance must sign with the **same** key, otherwise a token issued by instance A
is rejected by instance B (users get random `401 Unauthorized` responses).

Set a shared key of **at least 32 characters**. Never commit it to `appsettings.json`;
inject it through an environment variable instead. ASP.NET Core maps double underscores
to the configuration hierarchy, so `AppSetting__AdminApi__SigningKey` binds to
`AppSetting:AdminApi:SigningKey`.

Generate a strong key:
```bash
# Linux / macOS
openssl rand -base64 64

# PowerShell
[Convert]::ToBase64String((1..64 | ForEach-Object { Get-Random -Maximum 256 }))
```

#### Docker
```bash
docker run -e AppSetting__AdminApi__SigningKey="<your-generated-key>" -p 4000:4000 ocelot-admin
```

#### Docker Compose
```yaml
services:
  gateway:
    image: ocelot-admin
    ports:
      - "4000:4000"
    environment:
      AppSetting__AdminApi__SigningKey: ${ADMIN_SIGNING_KEY}
```

Put `ADMIN_SIGNING_KEY=<your-generated-key>` in a `.env` file next to the compose file
(and make sure `.env` is listed in `.gitignore`).

#### Kubernetes
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: ocelot-admin-secrets
type: Opaque
stringData:
  signing-key: "<your-generated-key>"
---
# in the Deployment container spec:
env:
  - name: AppSetting__AdminApi__SigningKey
    valueFrom:
      secretKeyRef:
        name: ocelot-admin-secrets
        key: signing-key
```

## User management (CLI)

When the app is started with arguments it runs in CLI mode instead of starting the web
server:

```bash
dotnet run -- add            # create a user
dotnet run -- remove         # delete a user
dotnet run -- set-password   # reset a user password
dotnet run -- list           # list users and roles
```

## Documentation
[https://github.com/asielh1n1/ocelot-admin/wiki](https://github.com/asielh1n1/ocelot-admin/wiki)