import { defineStore } from 'pinia'
import { MainStore } from '../stores/main'
import LoginApi from '../api/login'
const store = MainStore()

export const LoginStore = defineStore('LoginStore', {
  state: () => ({
    username: '',
    password: '',
    loading: false,
    error: ''
  }),
  getters: {},
  actions: {
    async login() {
      this.loading = true
      this.error = ''
      try {
        const api = new LoginApi()
        const apiResult = await api.login(this.username, this.password)
        store.saveAuthData(apiResult.access_token, apiResult.username);
        this.resetForm()
        store.changePage('/')
      } catch(error){
        console.log('Error: ', error);
        if(error.code == 'ERR_NETWORK')
          this.error = store.translate('network_error')
        else if(error.response && error.response.data)
           this.error = store.translate(error.response.data.code_error)
        else this.error = store.translate('unknown_error')
      }
      finally {
        this.loading = false
      }
    },
    resetForm() {
      this.username = ''
      this.password = ''
      this.error = ''
      this.email = ''
      this.message = ''
    }
  }
})
