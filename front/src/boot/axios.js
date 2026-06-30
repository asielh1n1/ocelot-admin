import { boot } from 'quasar/wrappers'
import axios from 'axios'
import { MainStore } from '../stores/main'


const api = axios.create({ baseURL: process.env.URL_API })

api.interceptors.request.use(function (config) {
  const mainStore = MainStore()
  // Do something before request is sent
  if (mainStore.accessToken) {
    config.headers.Authorization = 'Bearer ' + mainStore.accessToken
  }
  return config
}, function (error) {
  // Do something with request error
  return Promise.reject(error)
})

// Add a response interceptor
api.interceptors.response.use(function (response) {
  // Any status code that lie within the range of 2xx cause this function to trigger
  // Do something with response data
  return response
}, function (error) {
  const mainStore = MainStore()
  if (!error.response) {
    mainStore.changePage('/network-error')
  }else if (error.response && error.response.status === 401) {
    mainStore.logout()
  }
  else if(error.response && error.response.status === 403){
    mainStore.logout()
  }
  throw error
})


export default boot(({ app }) => {
  
  // for use inside Vue files (Options API) through this.$axios and this.$api

  app.config.globalProperties.$axios = axios
  // ^ ^ ^ this will allow you to use this.$axios (for Vue Options API form)
  //       so you won't necessarily have to import axios in each vue file

  app.config.globalProperties.$api = api
  // ^ ^ ^ this will allow you to use this.$api (for Vue Options API form)
  //       so you can easily perform requests against your app's API
})

export { api }
