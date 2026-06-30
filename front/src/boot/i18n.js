import { boot } from 'quasar/wrappers'
import i18n from 'src/i18n'

// "async" is optional;
// more info on params: https://v2.quasar.dev/quasar-cli/boot-files
export default boot(async ({ app }) => {
  // Tell app to use the I18n instance
  app.use(i18n)
})
