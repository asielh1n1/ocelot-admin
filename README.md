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

## Run with Docker

The published image is available at GitHub Container Registry:

```bash
docker run -d \
  --name ocelot-admin \
  -p 4000:8080 \
  ghcr.io/asielh1n1/ocelot-admin:latest
```

Open <http://localhost:4000/admin>.

For a persistent identity database, a plugins folder and the signing key, use the
provided `docker-compose.yml`:

```bash
# Optional: put ADMIN_SIGNING_KEY=<your-key> in a .env file next to it
docker compose up -d
```

> **About `SigningKey`:** it is optional. When `AppSetting:AdminApi:SigningKey` is empty,
> the application generates a random key at startup. Set it only when running more than
> one instance, so all of them share the same key. See the
> [wiki](https://github.com/asielh1n1/ocelot-admin/wiki) for details.

### Building the image locally

```bash
docker build -t ocelot-admin .
docker run -d -p 4000:8080 ocelot-admin
```

## Documentation
[https://github.com/asielh1n1/ocelot-admin/wiki](https://github.com/asielh1n1/ocelot-admin/wiki)