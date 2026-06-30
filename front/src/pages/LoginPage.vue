<template>
  <q-layout class="bg-white" view="lHh Lpr lFf">
    <q-page-container>
      <q-page class="flex flex-center">
        <div class="row full-width">
          <div class="col-10 offset-1 col-sm-8 offset-sm-2 col-md-4 offset-md-4 col-lg-2 offset-lg-5">
            <q-form ref="form" greedy >
              <q-img error-src="~/assets/logo.svg" :src="$store.branding.logoUrl" class="items-center" style="height: 80px; max-width: 80px;display: block;margin-left: auto;margin-right: auto;"/>
              <div class="text-h5 text-center q-my-md text-grey-9">{{ $store.branding.appName }}</div>
              <p v-show="error" class="text-negative text-center">{{error}}</p>
              <q-input v-model="username" @keyup.enter="login()" autocomplete="username" :label="$t('username')+' *'" lazy-rules :rules="[ val => val && val.length > 0 || $t('required_field')]"></q-input>
              <q-input type="password" @keyup.enter="login()" v-model="password" autocomplete="current-password" :label="$t('password')+' *'" lazy-rules :rules="[ val => val && val.length > 0 || $t('required_field')]"/>
              <div class="row">
                <div class="col-12 q-mt-md q-mb-lg">
                  <q-btn @click="login()" class="full-width" color="primary"  :loading="loading">
                    <span class="q-mr-xs">{{$t('login')}}</span>
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
import { mapWritableState } from 'pinia'
import { LoginStore } from '../stores/login'
const store = LoginStore()

export default defineComponent({
  name: 'LoginPage',
  computed: {
    ...mapWritableState(LoginStore, ['organization', 'username', 'password', 'loading', 'error']),
  },
  methods: {
    async login () {
      const result = await this.$refs.form.validate()
      if (!result) return
      store.login()
    }
  },
  mounted () {
    store.resetForm()
  }
})
</script>
