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
    branding: {
      appName: window.__APP_CONFIG__ ? window.__APP_CONFIG__.appName : 'QvaSoft Gateway',
      primaryColor: window.__APP_CONFIG__ ? window.__APP_CONFIG__.primaryColor : '#03a9f4',
      logoUrl: window.__APP_CONFIG__ ? window.__APP_CONFIG__.logoUrl : '~assets/logo.svg',
      registrationCompleted: window.__APP_CONFIG__ ? window.__APP_CONFIG__.registrationCompleted : true
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
      this.configChanged = false
      this.changePage('/login')
    },
    async initialize() {
      this.loading(true)
      try {
        const apiGateway = new ApiGateway()
        const response = await apiGateway.getConfig()
        this.config = response.ocelot
        this.settings = response.settings
        this.config[OcelotKey.Enpoints] = this.config[OcelotKey.Enpoints].map(x=> {
          x.id = window.crypto.randomUUID()
          return x;
        })
        this.configChanged = false
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
      this.loading(true)
      try {
        const apiGateway = new ApiGateway()
        let config = JSON.parse(JSON.stringify(this.config))
        config[OcelotKey.Enpoints] = config[OcelotKey.Enpoints].map(x=>{
          delete x.id
          return x
        })
        await apiGateway.saveConfig(config)
        this.toast('Data saved successfully', 'positive')
        this.configChanged = false
      } catch (error) {
        if(error.response.status === 400){
          const safeMessage = error.response.data.map(msg => escapeHtml(msg)).join('<br>')
          return this.toast(safeMessage, 'negative', true)
        }
        this.toast('Error save data', 'negative')
      } finally { this.loading(false) }
    },
    setGlobalConfig(data) {
      this.configChanged = true
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
      this.configChanged = true
      
    },
    addRoute(route) {
      if(!this.config[OcelotKey.Enpoints])
        this.config[OcelotKey.Enpoints] = []
      route.id = window.crypto.randomUUID()
      this.config[OcelotKey.Enpoints].push(route)
      this.configChanged = true
    },
    deleteRoute(routes) {
      this.config[OcelotKey.Enpoints] = this.config[OcelotKey.Enpoints].filter(x=> !routes.some(y=> y.id == x.id))
      this.configChanged = true
    },
    addSwagger(swagger){
      if(!this.config[OcelotKey.Swagger])
        this.config[OcelotKey.Swagger] = []
      this.config[OcelotKey.Swagger].push(swagger)
      this.configChanged = true
    },
    setSwagger(swagger){
      const index =  this.config[OcelotKey.Swagger].findIndex(x=> x.Key == swagger.Key)
      this.config[OcelotKey.Swagger][index] = swagger
      this.configChanged = true
    },
    deleteSwagger(swagger){
      this.config[OcelotKey.Swagger] = this.config[OcelotKey.Swagger].filter(x=> x.Key != swagger.Key)
      this.configChanged = true
    },
    addAggregates(aggregates){
      if(!this.config[OcelotKey.Aggregates])
        this.config[OcelotKey.Aggregates] = []
      this.config[OcelotKey.Aggregates].push(aggregates)
      this.configChanged = true
    },
    setAggregates(aggregates){
      const index =  this.config[OcelotKey.Aggregates].findIndex(x=> x.UpstreamPathTemplate == aggregates.UpstreamPathTemplate)
      this.config[OcelotKey.Aggregates][index] = aggregates
      this.configChanged = true
    },
    deleteAggregates(aggregates){
      this.config[OcelotKey.Aggregates] = this.config[OcelotKey.Aggregates].filter(x=> x.UpstreamPathTemplate != aggregates.UpstreamPathTemplate)
      this.configChanged = true
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
