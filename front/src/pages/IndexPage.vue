<template>
  <q-page class="dashboard-page q-pa-md">
    <!-- ============ KPI cards ============ -->
    <div class="row q-col-gutter-md">
      <div class="col-12 col-sm-6 col-md-3" v-for="kpi in kpis" :key="kpi.key">
        <q-card class="kpi-card cursor-pointer" flat @click="changePage(kpi.route)">
          <q-card-section class="row items-center no-wrap q-pa-md">
            <div class="kpi-icon" :class="`bg-${kpi.color}`">
              <q-icon :name="kpi.icon" size="26px" color="white" />
            </div>
            <div class="col q-ml-md">
              <div class="kpi-value">{{ kpi.value }}</div>
              <div class="kpi-label">{{ kpi.label }}</div>
            </div>
          </q-card-section>
          <q-card-section class="q-py-none q-px-md q-pb-sm">
            <div class="kpi-hint text-grey-7">{{ kpi.hint }}</div>
          </q-card-section>
        </q-card>
      </div>
    </div>

    <!-- ============ Configuration health ============ -->
    <div class="row q-col-gutter-md q-mt-sm">
      <div class="col-12 col-md-7">
        <q-card flat class="panel-card">
          <q-card-section class="panel-header">
            <div class="row items-center">
              <q-icon name="health_and_safety" size="20px" class="q-mr-sm text-primary" />
              <span class="panel-title">{{ $t('config_health') }}</span>
              <q-space />
              <q-chip
                dense
                size="sm"
                :color="healthSummary.critical > 0 ? 'negative' : (healthSummary.warnings > 0 ? 'warning' : 'positive')"
                text-color="white"
                :icon="healthSummary.critical > 0 ? 'error' : (healthSummary.warnings > 0 ? 'warning' : 'check_circle')"
              >
                {{ healthSummary.label }}
              </q-chip>
            </div>
          </q-card-section>
          <q-separator />
          <q-card-section class="q-pa-none">
            <q-list separator dense>
              <q-item v-for="item in healthChecks" :key="item.key" class="health-item">
                <q-item-section avatar class="q-pr-none" style="min-width: 32px">
                  <q-icon :name="item.icon" :color="item.color" size="18px" />
                </q-item-section>
                <q-item-section>
                  <q-item-label class="health-label">{{ item.label }}</q-item-label>
                </q-item-section>
                <q-item-section side>
                  <span class="health-value" :class="`text-${item.color}`">{{ item.value }}</span>
                </q-item-section>
              </q-item>
            </q-list>
          </q-card-section>
        </q-card>
      </div>

      <!-- ============ Routes by method ============ -->
      <div class="col-12 col-md-5">
        <q-card flat class="panel-card fit">
          <q-card-section class="panel-header">
            <div class="row items-center">
              <q-icon name="insights" size="20px" class="q-mr-sm text-primary" />
              <span class="panel-title">{{ $t('routes_by_method') }}</span>
            </div>
          </q-card-section>
          <q-separator />
          <q-card-section>
            <div v-if="methodStats.length === 0" class="text-grey-6 text-center q-pa-md">
              {{ $t('no_data') }}
            </div>
            <div v-for="m in methodStats" :key="m.method" class="method-row q-mb-sm">
              <div class="row items-center q-mb-xs">
                <q-badge :color="m.color" :label="m.method" class="method-badge" />
                <q-space />
                <span class="text-caption text-grey-7">{{ m.count }}</span>
              </div>
              <q-linear-progress
                :value="m.percent"
                :color="m.color"
                track-color="grey-3"
                rounded
                size="8px"
              />
            </div>
          </q-card-section>
        </q-card>
      </div>
    </div>

    <!-- ============ Downstream targets ============ -->
    <div class="row q-col-gutter-md q-mt-sm">
      <div class="col-12">
        <q-card flat class="panel-card">
          <q-card-section class="panel-header">
            <div class="row items-center">
              <q-icon name="dns" size="20px" class="q-mr-sm text-primary" />
              <span class="panel-title">{{ $t('downstream_targets') }}</span>
              <q-space />
              <span class="text-caption text-grey-7">{{ hosts.length }}</span>
            </div>
          </q-card-section>
          <q-separator />
          <q-card-section>
            <div v-if="hosts.length === 0" class="text-grey-6 text-center q-pa-md">
              {{ $t('no_data') }}
            </div>
            <div class="row q-col-gutter-sm">
              <div class="col-12 col-sm-6 col-lg-4" v-for="h in hosts" :key="h.key">
                <div class="host-row row items-center no-wrap">
                  <q-icon
                    :name="h.secure ? 'lock' : 'lock_open'"
                    :color="h.secure ? 'positive' : 'warning'"
                    size="16px"
                    class="q-mr-sm"
                  />
                  <div class="col ellipsis">
                    <div class="host-name ellipsis">{{ h.key }}</div>
                    <div class="text-caption text-grey-6">{{ h.scheme }} · {{ h.routes }} {{ $t('routes') }}</div>
                  </div>
                  <q-badge color="grey-3" text-color="grey-9" :label="h.routes" />
                </div>
              </div>
            </div>
          </q-card-section>
        </q-card>
      </div>
    </div>

    <!-- ============ Plugins & providers ============ -->
    <div class="row q-col-gutter-md q-mt-sm">
      <div class="col-12 col-md-6">
        <q-card flat class="panel-card fit">
          <q-card-section class="panel-header">
            <div class="row items-center">
              <q-icon name="extension" size="20px" class="q-mr-sm text-primary" />
              <span class="panel-title">{{ $t('plugins') }}</span>
              <q-space />
              <span class="text-caption text-grey-7">{{ plugins.length }}</span>
            </div>
          </q-card-section>
          <q-separator />
          <q-card-section>
            <div v-if="plugins.length === 0" class="text-grey-6 text-center q-pa-md">
              {{ $t('no_plugins_loaded') }}
            </div>
            <div v-for="p in plugins" :key="p.value" class="chip-row q-mb-xs">
              <q-chip dense square color="primary" text-color="white" :label="p.label" />
              <span class="text-caption text-grey-6">{{ p.value }}</span>
            </div>
          </q-card-section>
        </q-card>
      </div>

      <div class="col-12 col-md-6">
        <q-card flat class="panel-card fit">
          <q-card-section class="panel-header">
            <div class="row items-center">
              <q-icon name="verified_user" size="20px" class="q-mr-sm text-primary" />
              <span class="panel-title">{{ $t('auth_providers') }}</span>
              <q-space />
              <span class="text-caption text-grey-7">{{ providers.length }}</span>
            </div>
          </q-card-section>
          <q-separator />
          <q-card-section>
            <div v-if="providers.length === 0" class="text-grey-6 text-center q-pa-md">
              {{ $t('no_data') }}
            </div>
            <div v-for="(p, i) in providers" :key="p" class="chip-row q-mb-xs">
              <q-chip dense square :color="i === 0 ? 'primary' : 'grey-4'" :text-color="i === 0 ? 'white' : 'grey-9'" :label="p" />
              <span v-if="i === 0" class="text-caption text-grey-6">{{ $t('default_scheme') }}</span>
            </div>
          </q-card-section>
        </q-card>
      </div>
    </div>

    <!-- ============ Branding footer ============ -->
    <div class="row justify-center q-mt-xl q-mb-lg">
      <div class="text-center">
        <q-img
          error-src="~assets/logo.svg"
          :src="$store.branding.logoUrl"
          width="96px"
          class="brand-logo"
        />
        <div class="text-subtitle1 q-mt-md text-grey-7">{{ $store.branding.appName }}</div>
        <div v-if="$store.branding.version" class="text-caption text-grey-5">
          {{ $t('version') }} {{ $store.branding.version }}
        </div>
      </div>
    </div>
  </q-page>
</template>

<script>
import { MainStore } from '../stores/main'
const mainStore = MainStore()

const METHOD_COLORS = {
  GET: 'primary',
  POST: 'positive',
  PUT: 'warning',
  PATCH: 'orange',
  DELETE: 'negative',
  HEAD: 'grey-7',
  OPTIONS: 'grey-6'
}

export default {
  name: 'IndexPage',
  computed: {
    changePage () { return mainStore.changePage },
    aggregates () { return mainStore.getAggregates },
    endpoints () { return mainStore.getEnpoints },
    swaggers () { return mainStore.getSwagger },

    // ---- plugins & providers exposed by /admin-api/data ----
    plugins () {
      const list = mainStore.settings?.delegatingHandlers
      return Array.isArray(list) ? list : []
    },
    providers () {
      const list = mainStore.settings?.authenticationProviders
      return Array.isArray(list) ? list : []
    },

    // ---- routes ----
    routes () {
      return this.endpoints || []
    },

    routeStats () {
      const routes = this.routes
      let anonymous = 0
      let authenticated = 0
      let plainHttp = 0
      let tlsDisabled = 0
      let rateLimited = 0
      let cached = 0
      let withTimeout = 0
      let withQos = 0
      let withPlugin = 0
      let catchAll = 0

      for (const r of routes) {
        // Authentication
        const auth = r.AuthenticationOptions
        const isAnonymous = !auth || auth.AllowAnonymous === true
        if (isAnonymous) anonymous++
        else authenticated++

        // Transport security
        const scheme = (r.DownstreamScheme || '').toLowerCase()
        if (scheme === 'http') plainHttp++
        if (r.DangerousAcceptAnyServerCertificateValidator === true) tlsDisabled++

        // Resilience / performance
        if (r.RateLimitOptions && r.RateLimitOptions.EnableRateLimiting) rateLimited++
        if (r.CacheOptions && Number(r.CacheOptions.TtlSeconds) > 0) cached++
        if (r.Timeout) withTimeout++
        if (r.QoSOptions) withQos++
        if (Array.isArray(r.DelegatingHandlers) && r.DelegatingHandlers.length > 0) withPlugin++

        // Routing risk
        if ((r.UpstreamPathTemplate || '').includes('{catchAll}')) catchAll++
      }

      return { anonymous, authenticated, plainHttp, tlsDisabled, rateLimited, cached, withTimeout, withQos, withPlugin, catchAll, total: routes.length }
    },

    kpis () {
      return [
        {
          key: 'endpoints',
          label: this.$t('endpoint', 2),
          value: this.endpoints.length,
          hint: `${this.routeStats.authenticated} ${this.$t('protected_routes')}`,
          icon: 'dvr',
          color: 'primary',
          route: '/endpoint'
        },
        {
          key: 'swagger',
          label: this.$t('swagger'),
          value: this.swaggers.length,
          hint: this.swaggers.length ? this.$t('documented_apis') : this.$t('no_data'),
          icon: 'description',
          color: 'deep-purple',
          route: '/swagger'
        },
        {
          key: 'aggregates',
          label: this.$t('aggregation'),
          value: this.aggregates.length,
          hint: this.aggregates.length ? this.$t('combined_apis') : this.$t('no_data'),
          icon: 'join_inner',
          color: 'red-6',
          route: '/aggregation'
        },
        {
          key: 'plugins',
          label: this.$t('plugins'),
          value: this.plugins.length,
          hint: `${this.routeStats.withPlugin} ${this.$t('routes_with_plugin')}`,
          icon: 'extension',
          color: 'teal',
          route: '/endpoint'
        }
      ]
    },

    healthChecks () {
      const s = this.routeStats
      const rows = []

      rows.push({
        key: 'auth',
        label: this.$t('protected_routes'),
        value: `${s.authenticated}/${s.total}`,
        icon: s.anonymous > 0 ? 'warning' : 'check_circle',
        color: s.anonymous > 0 ? 'warning' : 'positive'
      })

      if (s.plainHttp > 0) {
        rows.push({
          key: 'http',
          label: this.$t('routes_plain_http'),
          value: s.plainHttp,
          icon: 'lock_open',
          color: 'warning'
        })
      }

      if (s.tlsDisabled > 0) {
        rows.push({
          key: 'tls',
          label: this.$t('routes_tls_disabled'),
          value: s.tlsDisabled,
          icon: 'gpp_bad',
          color: 'negative'
        })
      }

      if (s.catchAll > 0) {
        rows.push({
          key: 'catchall',
          label: this.$t('routes_catch_all'),
          value: s.catchAll,
          icon: 'alt_route',
          color: 'warning'
        })
      }

      rows.push({
        key: 'ratelimit',
        label: this.$t('routes_rate_limited'),
        value: s.rateLimited,
        icon: 'speed',
        color: s.rateLimited > 0 ? 'positive' : 'grey-6'
      })

      rows.push({
        key: 'cache',
        label: this.$t('routes_cached'),
        value: s.cached,
        icon: 'cached',
        color: s.cached > 0 ? 'positive' : 'grey-6'
      })

      rows.push({
        key: 'timeout',
        label: this.$t('routes_with_timeout'),
        value: s.withTimeout,
        icon: 'timer',
        color: s.total > 0 && s.withTimeout === s.total ? 'positive' : 'grey-6'
      })

      rows.push({
        key: 'qos',
        label: this.$t('routes_with_qos'),
        value: s.withQos,
        icon: 'shield',
        color: s.withQos > 0 ? 'positive' : 'grey-6'
      })

      return rows
    },

    healthSummary () {
      const critical = this.healthChecks.filter(c => c.color === 'negative').length
      const warnings = this.healthChecks.filter(c => c.color === 'warning').length

      if (critical > 0) return { critical, warnings, label: this.$t('needs_attention') }
      if (warnings > 0) return { critical, warnings, label: this.$t('review_suggested') }
      return { critical, warnings, label: this.$t('all_good') }
    },

    methodStats () {
      const counts = {}
      for (const r of this.routes) {
        const methods = Array.isArray(r.UpstreamHttpMethod) ? r.UpstreamHttpMethod : []
        for (const m of methods) {
          const key = String(m).toUpperCase()
          counts[key] = (counts[key] || 0) + 1
        }
      }

      const total = Object.values(counts).reduce((a, b) => a + b, 0) || 1

      return Object.keys(counts)
        .sort((a, b) => counts[b] - counts[a])
        .map(method => ({
          method,
          count: counts[method],
          percent: counts[method] / total,
          color: METHOD_COLORS[method] || 'grey-6'
        }))
    },

    hosts () {
      const map = new Map()

      for (const r of this.routes) {
        const scheme = (r.DownstreamScheme || 'http').toLowerCase()
        const targets = Array.isArray(r.DownstreamHostAndPorts) ? r.DownstreamHostAndPorts : []

        for (const t of targets) {
          if (!t || !t.Host) continue
          const key = t.Port ? `${t.Host}:${t.Port}` : t.Host
          const existing = map.get(key)

          if (existing) {
            existing.routes++
          } else {
            map.set(key, { key, scheme, secure: scheme === 'https', routes: 1 })
          }
        }
      }

      return Array.from(map.values()).sort((a, b) => b.routes - a.routes)
    }
  }
}
</script>

<style scoped>
.dashboard-page {
  max-width: 1500px;
  margin: 0 auto;
}

/* ---- KPI cards ---- */
.kpi-card {
  border-radius: 14px;
  border: 1px solid rgba(0, 0, 0, 0.06);
  background: #fff;
  transition: transform 0.18s ease, box-shadow 0.18s ease;
}

.kpi-card:hover {
  transform: translateY(-3px);
  box-shadow: 0 10px 24px rgba(0, 0, 0, 0.09);
}

.kpi-icon {
  width: 52px;
  height: 52px;
  border-radius: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex: 0 0 auto;
}

.kpi-value {
  font-size: 1.9rem;
  font-weight: 600;
  line-height: 1.1;
  color: #1f2937;
}

.kpi-label {
  font-size: 0.82rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: #6b7280;
  font-weight: 500;
}

.kpi-hint {
  font-size: 0.75rem;
  padding-top: 2px;
}

/* ---- Panels ---- */
.panel-card {
  border-radius: 14px;
  border: 1px solid rgba(0, 0, 0, 0.06);
  background: #fff;
  height: 100%;
}

.panel-header {
  padding: 12px 16px;
}

.panel-title {
  font-size: 0.9rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: #374151;
}

/* ---- Health list ---- */
.health-item {
  padding: 7px 16px;
  min-height: 40px;
}

.health-label {
  font-size: 0.85rem;
  color: #4b5563;
}

.health-value {
  font-size: 0.85rem;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

/* ---- Methods ---- */
.method-row {
  padding: 2px 0;
}

.method-badge {
  min-width: 64px;
  justify-content: center;
  font-size: 0.7rem;
  letter-spacing: 0.03em;
}

/* ---- Hosts ---- */
.host-row {
  border: 1px solid rgba(0, 0, 0, 0.07);
  border-radius: 10px;
  padding: 8px 12px;
  background: #fafafa;
}

.host-name {
  font-size: 0.83rem;
  font-weight: 500;
  color: #374151;
  font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
}

/* ---- Chips ---- */
.chip-row {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

/* ---- Branding ---- */
.brand-logo {
  opacity: 0.85;
  filter: grayscale(0.15);
}
</style>
