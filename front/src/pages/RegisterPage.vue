<template>
  <q-layout class="bg-white" view="lHh Lpr lFf">
    <q-page-container>
      <q-page class="flex flex-center">
        <div class="row full-width">
          <div class="col-10 offset-1 col-sm-8 offset-sm-2 col-md-4 offset-md-4 col-lg-2 offset-lg-5">
            <q-form ref="form" greedy>
              <q-img error-src="~/assets/logo.svg" :src="$store.branding.logoUrl" class="items-center" style="height: 80px; max-width: 80px;display: block;margin-left: auto;margin-right: auto;"/>
              <div class="text-h5 text-center q-my-md text-grey-9">{{ $t('register_title') }}</div>
              <p v-show="error" class="text-negative text-center">{{ error }}</p>
              <q-input
                v-model="form.email"
                type="email"
                :label="$t('email') + ' *'"
                lazy-rules
                :rules="[
                  val => val && val.length > 0 || $t('required_field'),
                  val => /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(val) || $t('invalid_email')
                ]"
              />
              <q-input
                v-model="form.fullName"
                :label="$t('full_name') + ' *'"
                lazy-rules
                :rules="[val => val && val.length > 0 || $t('required_field')]"
              />
              <q-select
                v-model="form.clientType"
                :options="clientTypeOptions"
                :label="$t('client_type') + ' *'"
                emit-value
                map-options
                lazy-rules
                :rules="[val => !!val || $t('required_field')]"
              />
              <q-input
                v-model="form.password"
                type="password"
                :label="$t('password') + ' *'"
                lazy-rules
                :rules="[
                  val => val && val.length > 0 || $t('required_field'),
                  val => val && val.length >= 8 && /[A-Z]/.test(val) && /[a-z]/.test(val) && /[0-9]/.test(val) || $t('password_policy')
                ]"
                @keyup.enter="register()"
              />
              <div class="row">
                <div class="col-12 q-mt-md q-mb-lg">
                  <q-btn
                    @click="register()"
                    class="full-width q-mt-md"
                    color="primary"
                    :loading="loading"
                    @keyup.enter="register()"
                  >
                    <span class="q-mr-xs">{{ $t('register') }}</span>
                  </q-btn>
                </div>
              </div>
            </q-form>
          </div>
        </div>
      </q-page>
    </q-page-container>
  </q-layout>
</template>

<script>
import { defineComponent } from 'vue'
import { MainStore } from '../stores/main'
import RegisterApi from '../api/register'

const mainStore = MainStore()

export default defineComponent({
  name: 'RegisterPage',
  data() {
    return {
      form: {
        email: '',
        fullName: '',
        clientType: '',
        password: ''
      },
      loading: false,
      error: '',
      clientTypeOptions: [
        { label: this.$t('personal'), value: 'personal' },
        { label: this.$t('company'), value: 'company' }
      ]
    }
  },
  methods: {
    async register() {
      const valid = await this.$refs.form.validate()
      if (!valid) return

      this.loading = true
      this.error = ''
      try {
        const api = new RegisterApi()
        const result = await api.register(this.form)
        mainStore.branding.registrationCompleted = true
        await mainStore.saveAuthData(result.access_token, result.username)
        mainStore.changePage('/')
      } catch (error) {
        if (error.code === 'ERR_NETWORK') {
          this.error = mainStore.translate('network_error')
        } else if (error.response && error.response.data && error.response.data.code_error) {
          this.error = mainStore.translate(error.response.data.code_error)
        } else {
          this.error = mainStore.translate('unknown_error')
        }
      } finally {
        this.loading = false
      }
    }
  }
})
</script>
