<template>
  <q-layout view="hHh lpR fFf">
    <q-header elevated class="bg-primary text-white">
      <q-toolbar>
        <q-btn dense flat round icon="menu" @click="toggleLeftDrawer" />
        <q-toolbar-title class="row items-center no-wrap">
          <span class="ellipsis">{{ $store.branding.appName }}</span>
        </q-toolbar-title>
        <q-space />

        <q-btn flat round dense icon="update" @click="updateAll" size="md" class="q-mr-xs">
          <q-tooltip>{{ $t('update') }}</q-tooltip>
        </q-btn>

        <!-- Save: same compact round icon as always. When there are unsaved changes
             it turns white-on-primary and shows a counter badge at the top-right. -->
        <q-btn flat round dense size="md" icon="save" :class="configChanged ? 'text-white save-btn--active' : 'text-white'" :disable="!configChanged" @click="saveAllChanges">
          <q-badge v-if="configChanged" color="deep-orange" text-color="white" floating rounded class="save-count">
            {{ changeCount }}
          </q-badge>
          <q-tooltip>
            {{ configChanged ? $t('unsaved_changes', { count: changeCount }) : $t('no_changes') }}
          </q-tooltip>
        </q-btn>
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
      <div class="absolute-bottom text-center q-mb-sm text-caption drawer-footer">
        <span class="text-grey-6">{{ $store.branding.appName }}</span>
        <span v-if="$store.branding.version" class="text-grey-5"> · v{{ $store.branding.version }}</span>
      </div>
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
    configChanged() { return mainStore.configChanged },
    changeCount() { return mainStore.changeCount }
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
<style scoped>
.version-badge {
  font-size: 0.62rem;
  font-weight: 600;
  letter-spacing: 0.02em;
  padding: 2px 6px;
  opacity: 0.92;
}

/* ---- Save button (unsaved changes) ---- */
.save-btn--active {
  transition: none;
}

/* Compact counter pinned to the icon's top-right corner. Deep orange keeps it
   readable over any primary color injected from the backend, and the border
   separates it from the header background. */
.save-count {
  font-size: 0.6rem;
  font-weight: 700;
  min-width: 12px;
  height: 12px;
  line-height: 12px;
  padding: 0 4px;
  top: -2px;
  right: -2px;
  box-shadow: 0 0 0 1.5px rgba(255, 255, 255, 0.9);
}

.drawer-footer {
  padding-bottom: 4px;
  border-top: 1px solid rgba(0, 0, 0, 0.06);
  padding-top: 8px;
}
</style>
<style lang="sass">
// Active menu item uses the injected primary color (kept here because it targets
// Quasar's generated item classes, which scoped styles cannot reach).
.active-menu
  color: var(--q-primary) !important
  background: rgba(0, 0, 0, 0.05) !important
  font-weight: 500
</style>
