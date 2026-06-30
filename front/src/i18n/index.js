import en from './locales/en'
import es from './locales/es'
import { createI18n } from 'vue-i18n'

const i18n = createI18n({
  locale: 'en',
  globalInjection: true,
  messages: { en, es }
})
export default i18n
