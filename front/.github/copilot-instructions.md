# API-Gateway-UI (Quasar + Vue 3) AI Instructions

## Big picture architecture
- Quasar SPA with Vue Router + Pinia. Entry point uses boot files in [quasar.config.js](quasar.config.js#L8-L40): i18n, axios, router.
- Data flow: pages/components read/write through `MainStore` and `LoginStore` in [src/stores/main.js](src/stores/main.js#L1-L199) and [src/stores/login.js](src/stores/login.js#L1-L49), which call `ApiGateway` in [src/api/api-gateway.js](src/api/api-gateway.js#L1-L25) using the shared `api` Axios instance.
- Auth + navigation: axios interceptor injects `Authorization: Bearer <token>` and redirects on 401/403/network errors in [src/boot/axios.js](src/boot/axios.js#L1-L48). Router guard in [src/boot/router.js](src/boot/router.js#L1-L18) enforces `meta.requiresAuth` routes from [src/router/routes.js](src/router/routes.js#L1-L48).
- Config shape is Ocelot-based; `MainStore` normalizes routes by adding `id` client-side and removes `id` before save in [src/stores/main.js](src/stores/main.js#L63-L122). Keep this behavior when modifying endpoints.

## Developer workflows
- Install: `npm install` or `yarn` (see [README.md](README.md#L7-L13)).
- Dev server: `quasar dev` (hash router, opens browser) ([README.md](README.md#L14-L20)).
- Lint: `npm run lint` / `yarn lint` ([README.md](README.md#L23-L29)).
- Build: `quasar build` outputs to `../wwwroot/admin` and serves under `/admin` with API base `/admin-api` in prod, `/admin-api` in dev is `http://localhost:4000/admin-api` ([quasar.config.js](quasar.config.js#L34-L73)).

## Project-specific conventions
- Pages/components use Options API and Pinia mapping; prefer consistent usage (see [src/pages/EndpointsPage.vue](src/pages/EndpointsPage.vue#L1-L227)).
- Dialog forms use `v-model:data` + `v-model:show` and emit `update:form` for save (examples: [src/components/RouteForm.vue](src/components/RouteForm.vue#L1-L216), [src/components/SwaggerForm.vue](src/components/SwaggerForm.vue#L1-L146), [src/components/AggregationForm.vue](src/components/AggregationForm.vue#L1-L79)).
- Key/value editor is centralized in [src/components/KeyValue.vue](src/components/KeyValue.vue#L1-L92): it converts objects to array entries and emits `update:modelValue` with an object. Use it for any map-like config fields.
- Validation helpers live in [src/util/validate.js](src/util/validate.js#L1-L20) and inline regexes in components; reuse when adding URL/path validation.
- i18n strings are required for UI labels; add keys to [src/i18n/en.js](src/i18n/en.js#L1-L200) (and [src/i18n/es.js](src/i18n/es.js)) and use `$t()`.

## Integrations & endpoints
- Backend API endpoints: `POST /login` for auth and `GET/POST /data` for config ([src/stores/login.js](src/stores/login.js#L13-L34), [src/api/api-gateway.js](src/api/api-gateway.js#L1-L25)).
- Session persistence uses `pinia-plugin-persistedstate` in `MainStore` with `sessionStorage` and a fixed `key` ([src/stores/main.js](src/stores/main.js#L163-L199)).