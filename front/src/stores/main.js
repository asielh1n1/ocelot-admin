import { defineStore } from 'pinia'
import ApiGateway from '../api/api-gateway'
import { Platform, Notify, Dialog, Loading } from 'quasar'
import i18n from '../i18n/index'
import help from '../util/help'; 
const { t } = i18n.global

const escapeHtml = (str) => String(str).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#x27;'}[c]))

const OcelotKey = {
  GlobalConfiguration : 'GlobalConfiguration',
  Enpoints : 'Routes',
  Swagger: 'SwaggerEndPoints',
  Aggregates: 'Aggregates',
}

/**
 * Debounce window for the autosave. Long enough to coalesce a burst of keystrokes or
 * a drag inside a form, short enough that the user perceives the change as saved.
 */
const AutoSaveDelayMs = 600

/**
 * Branding injected by the host into /admin/index.html (window.__APP_CONFIG__).
 *
 * The backend may serialise an unset value as "" (appsettings.json ships the keys as
 * empty strings), so a plain `||`/ternary would keep the empty string and `q-img` would
 * request an empty URL — the page itself — instead of falling back to the bundled logo.
 *
 * The fallback is a URL, not `~assets/logo.svg`: the `~` alias is resolved by the build
 * only for module imports, never at runtime. `logo.svg` lives in `public/`, so it is
 * copied as-is and is available at a stable path both in `quasar dev` (served from the
 * dev server) and after `quasar build` (served from wwwroot/admin by the gateway).
 */
const injectedConfig = window.__APP_CONFIG__ || {}
const brandingValue = (value, fallback) =>
  (value !== undefined && value !== null && String(value).trim() !== '') ? value : fallback
const defaultLogoUrl = `${import.meta.env.BASE_URL || '/'}logo.svg`

export const MainStore = defineStore('MainStore', {
  state: () => ({
    isAuthorize: false,
    accessToken: '',
    config: {},
    settings: {},
    leftDrawerOpen: !Platform.is.mobile,
    username: '',
    currentPage: '/',
    isLoading: false,
    configChanged: false,
    changeCount: 0,
    saving: false,
    branding: {
      appName: brandingValue(injectedConfig.appName, 'Ocelot Admin'),
      primaryColor: brandingValue(injectedConfig.primaryColor, '#03a9f4'),
      // URL of the bundled logo (public/logo.svg), resolved against the app base so it
      // works in `quasar dev` (/) and in the built app (/admin/).
      logoUrl: brandingValue(injectedConfig.logoUrl, defaultLogoUrl),
      version: brandingValue(injectedConfig.version, '1.0.0'),
      registrationCompleted: injectedConfig.registrationCompleted !== false
    }
  }),
  getters: {
    getGlobalConfig: (state) => state.config[OcelotKey.GlobalConfiguration] ? state.config[OcelotKey.GlobalConfiguration] : {},
    getEnpoints:(state)=> state.config[OcelotKey.Enpoints] ? state.config[OcelotKey.Enpoints] : [],
    getSwagger:(state)=> state.config[OcelotKey.Swagger] ? state.config[OcelotKey.Swagger] : [],
    getAggregates:(state)=> state.config[OcelotKey.Aggregates] ? state.config[OcelotKey.Aggregates] : [],
  },
  actions: {
    toggleLeftDrawer() {
      this.leftDrawerOpen = !this.leftDrawerOpen
    },
    async saveAuthData(token, username) {
      this.isAuthorize = true;
      this.accessToken = token;
      this.username = username  
    },
    logout() {
      this.isAuthorize = false;
      this.accessToken = '';
      this.username = ''
      this.config = {}
      this.settings = {}
      this.cancelPendingSave()
      this.resetChanges()
      this.changePage('/login')
    },
    async initialize() {
      this.loading(true)
      // A pending autosave would write stale data; drop it before reloading.
      this.cancelPendingSave()
      try {
        const apiGateway = new ApiGateway()
        const response = await apiGateway.getConfig()
        this.config = response.ocelot
        this.settings = response.settings
        this.config[OcelotKey.Enpoints] = this.config[OcelotKey.Enpoints].map(x=> {
          x.id = window.crypto.randomUUID()
          return x;
        })
        this.resetChanges()
      } finally { this.loading(false) }
    },
    changePage (path) {
      this.currentPage = path
      this.router.push(path)
    },
    translate(value, data) {
      if(data)
        return t(value , data)
      else return t(value)
    },
    toast(text, color, isHtml = false) {
      Notify.create({
        message: isHtml ? text : escapeHtml(text),
        color,
        position: 'top',
        html: isHtml
      })
    },
    confirm(text, title) {
      return new Promise((resolve, reject) => {
        Dialog.create({
          title: title || 'Information',
          message: escapeHtml(text),
          ok: {
            color: 'primary',
            flat: true
          },
          cancel: {
            color: 'blue-grey',
            flat: true
          },
          persistent: true
        })
          .onOk(() => {
            resolve(true)
          }).onCancel(() => {
            resolve(false)
          })
      })
    },
    loading(value) {
      this.isLoading = value
      if (value)
        Loading.show()
      else Loading.hide()
    },
    async save() {
      // Coalesce: while a request is in flight every caller (an autosave timer and,
      // on the Global Configuration page, the explicit button) awaits the same one
      // instead of posting a second, racy copy of the config.
      if (this.saving)
        return this._saving

      this.saving = true
      this.loading(true)
      // Snapshot the revision being saved before the request starts.
      const savedRevision = this.changeCount

      this._saving = (async () => {
        try {
          const apiGateway = new ApiGateway()
          let config = JSON.parse(JSON.stringify(this.config))
          config[OcelotKey.Enpoints] = config[OcelotKey.Enpoints].map(x=>{
            delete x.id
            return x
          })
          await apiGateway.saveConfig(config)

          // The user kept editing while the request was in flight: keep the dirty
          // flag so the scheduled autosave sends the newer version too.
          if (this.changeCount !== savedRevision)
            return

          this.toast('Data saved successfully', 'positive')
          this.resetChanges()
        } catch (error) {
          // Changes stay pending and the next edit retries them.
          if(error.response && error.response.status === 400){
            const safeMessage = error.response.data.map(msg => escapeHtml(msg)).join('<br>')
            return this.toast(safeMessage, 'negative', true)
          }
          this.toast('Error save data', 'negative')
        } finally {
          this.loading(false)
          this._saving = null
          this.saving = false
        }
      })()
      return this._saving
    },
    /**
     * Requests a debounced autosave. Every mutation calls this instead of turning on
     * a "pending changes" button: the config is persisted by itself.
     */
    scheduleAutoSave() {
      clearTimeout(this._saveTimer)
      this._saveTimer = setTimeout(() => { this.save() }, AutoSaveDelayMs)
    },
    /** Drops a scheduled autosave (used before reloading the configuration). */
    cancelPendingSave() {
      clearTimeout(this._saveTimer)
      this._saveTimer = null
    },
    setGlobalConfig(data) {
      this.registerChange()
      this.config[OcelotKey.GlobalConfiguration] = data
    },
    setEndpoints(endpoints){
      this.config[OcelotKey.Enpoints] = endpoints
    },
    setRoute(route) {
      const index =  this.config[OcelotKey.Enpoints].findIndex(x=> x.id == route.id)
      if(index == -1)
        return
      this.config[OcelotKey.Enpoints][index] = route
      this.registerChange()
      
    },
    addRoute(route) {
      if(!this.config[OcelotKey.Enpoints])
        this.config[OcelotKey.Enpoints] = []
      route.id = window.crypto.randomUUID()
      this.config[OcelotKey.Enpoints].push(route)
      this.registerChange()
    },
    deleteRoute(routes) {
      this.config[OcelotKey.Enpoints] = this.config[OcelotKey.Enpoints].filter(x=> !routes.some(y=> y.id == x.id))
      this.registerChange()
    },
    addSwagger(swagger){
      if(!this.config[OcelotKey.Swagger])
        this.config[OcelotKey.Swagger] = []
      this.config[OcelotKey.Swagger].push(swagger)
      this.registerChange()
    },
    setSwagger(swagger){
      const index =  this.config[OcelotKey.Swagger].findIndex(x=> x.Key == swagger.Key)
      this.config[OcelotKey.Swagger][index] = swagger
      this.registerChange()
    },
    deleteSwagger(swagger){
      this.config[OcelotKey.Swagger] = this.config[OcelotKey.Swagger].filter(x=> x.Key != swagger.Key)
      this.registerChange()
    },
    addAggregates(aggregates){
      if(!this.config[OcelotKey.Aggregates])
        this.config[OcelotKey.Aggregates] = []
      this.config[OcelotKey.Aggregates].push(aggregates)
      this.registerChange()
    },
    setAggregates(aggregates){
      const index =  this.config[OcelotKey.Aggregates].findIndex(x=> x.UpstreamPathTemplate == aggregates.UpstreamPathTemplate)
      this.config[OcelotKey.Aggregates][index] = aggregates
      this.registerChange()
    },
    deleteAggregates(aggregates){
      this.config[OcelotKey.Aggregates] = this.config[OcelotKey.Aggregates].filter(x=> x.UpstreamPathTemplate != aggregates.UpstreamPathTemplate)
      this.registerChange()
    },
    /**
     * Marks the config as dirty and schedules the autosave.
     * There is no "save" button any more: every mutation here is persisted shortly
     * after the user stops editing (see save()).
     */
    registerChange() {
      this.configChanged = true
      this.changeCount += 1
      this.scheduleAutoSave()
    },
    /** Clears the dirty state and the counter (after a successful save/reload). */
    resetChanges() {
      this.configChanged = false
      this.changeCount = 0
    },
    showHelp(title, helpKey){
      Dialog.create({
        title: `${t('help')}: ${title}`,
        message: help[helpKey] || 'Without help',
        html: true,
        style: Platform.is.mobile ? undefined : 'width: 70vw; max-width: 70vw;'
      })
    }
  },
  persist: {
    storage: sessionStorage,
    key: 'eda03e9d-94fa-4a0e-a2a9-0d46a782ad0e',
    pick: ['leftDrawerOpen', 'currentPage','accessToken','username','isAuthorize', 'settings']
  }
});
