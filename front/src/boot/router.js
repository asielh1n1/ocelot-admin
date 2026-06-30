import { boot } from 'quasar/wrappers'
import { MainStore } from '../stores/main'
const mainStore = MainStore()

// "async" is optional;
// more info on params: https://v2.quasar.dev/quasar-cli/boot-files
export default boot(async ({ router }) => {
  router.beforeEach(async (to) => {
    if (!mainStore.branding.registrationCompleted && to.path !== '/register') {
      return '/register'
    }
    if (to.meta.requiresAuth && mainStore.isAuthorize && !Object.keys(mainStore.config).length) {
      await mainStore.initialize()
    }
    if (to.meta.requiresAuth && !mainStore.isAuthorize) {
      mainStore.logout()
    }
    return true
  })
})
