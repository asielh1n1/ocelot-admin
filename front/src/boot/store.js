import { boot } from 'quasar/wrappers'
import { MainStore } from '../stores/main'
const mainStore = MainStore()


export default boot(async ({ app }) => {
  app.config.globalProperties.$store = mainStore
})
