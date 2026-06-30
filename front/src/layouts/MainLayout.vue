<template>
  <q-layout view="hHh lpR fFf">
    <q-header elevated class="bg-primary text-white">
      <q-toolbar>
        <q-btn dense flat round icon="menu" @click="toggleLeftDrawer" />
        <q-toolbar-title>
          <span>{{ $store.branding.appName }}</span> 
        </q-toolbar-title>
        <q-space />
        <q-icon @click="updateAll" name="update" size="sm" class="cursor-pointer q-mr-md">
          <q-tooltip>{{ $t('update') }}</q-tooltip>
        </q-icon>
        <q-icon @click="saveAllChanges" :color="configChanged ? 'red': 'white'" :name="configChanged ? 'save_as': 'save'" size="sm" class="cursor-pointer q-mr-md">
          <q-tooltip>{{ $t('save') }}</q-tooltip>
        </q-icon>
      </q-toolbar>
    </q-header>

    <q-drawer show-if-above v-model="leftDrawerOpen" side="left" bordered>
      <q-scroll-area class="fit">
        <q-list class="rounded-borders text-primary">
          <q-item class="q-mt-sm">
            <q-item-section avatar>
              <q-avatar size="48px" font-size="40px" text-color="white" icon="person" color="blue-grey-4"></q-avatar>
            </q-item-section>
            <q-item-section>
              <q-item-label class="text-black text-weight-regular">{{ user || 'Administrator' }}</q-item-label>
            </q-item-section>
          </q-item>
          <q-item clickable v-ripple :active="currentPage == '/'" @click="changePage('/')" active-class="active-menu" class="q-mt-sm">
            <q-item-section avatar>
              <q-icon name="home" />
            </q-item-section>
            <q-item-section>{{$t('dashboard')}}</q-item-section>
          </q-item>
          <q-item clickable v-ripple :active="currentPage == '/endpoint'" @click="changePage('/endpoint')" active-class="active-menu">
            <q-item-section avatar>
              <q-icon name="dvr" />
            </q-item-section>
            <q-item-section>{{$t('endpoint', 2)}}</q-item-section>
          </q-item>
          <q-item clickable v-ripple :active="currentPage == '/swagger'" @click="changePage('/swagger')" active-class="active-menu">
            <q-item-section avatar>
              <q-icon name="description" />
            </q-item-section>
            <q-item-section>{{$t('swagger_config')}}</q-item-section>
          </q-item>
          <q-item clickable v-ripple :active="currentPage == '/aggregation'" @click="changePage('/aggregation')" active-class="active-menu">
            <q-item-section avatar>
              <q-icon name="join_inner" />
            </q-item-section>
            <q-item-section>{{$t('aggregation')}}</q-item-section>
          </q-item>
          <q-item clickable v-ripple :active="currentPage == '/global-config'" @click="changePage('/global-config')" active-class="active-menu">
            <q-item-section avatar>
              <q-icon name="settings" />
            </q-item-section>
            <q-item-section>{{$t('global_config', 2)}}</q-item-section>
          </q-item>
          <q-item clickable v-ripple @click="logout('/')">
            <q-item-section avatar>
              <q-icon name="logout" />
            </q-item-section>
            <q-item-section>{{$t('exit')}}</q-item-section>
          </q-item>
        </q-list>
      </q-scroll-area>
      <div class="absolute-bottom text-center q-mb-sm text-caption">{{ $t('powered_QvaSoft') }}</div>
    </q-drawer>

    <q-page-container>
      <router-view />
    </q-page-container>
  
  </q-layout>
</template>

<script>
import { MainStore } from '../stores/main'
const mainStore = MainStore()

export default {
  computed: {
    leftDrawerOpen: {
      get () { return mainStore.leftDrawerOpen },
      set (val) {
        mainStore.leftDrawerOpen = val
      }
    },
    toggleLeftDrawer () { return mainStore.toggleLeftDrawer },
    user() {return mainStore.username },
    currentPage() { return mainStore.currentPage },
    changePage() { return mainStore.changePage },
    configChanged() { return mainStore.configChanged }
  },
  methods: {
    async logout() {
      const result = await mainStore.confirm(this.$t('confirm_logout'))
      if(result)
        mainStore.logout()
    },
    saveAllChanges() {
      if(this.configChanged)
        mainStore.save()
    },
    async updateAll() {
      if(this.configChanged){
        const result = await mainStore.confirm(this.$t('ask_about_update'))
        if(result)
          mainStore.initialize()
      } else mainStore.initialize() 
    }
  }
}
</script>
<style lang="sass">
.active-menu
  color: white !important
  background: #90a4ae !important
</style>
