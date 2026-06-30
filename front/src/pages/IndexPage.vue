<template>
  <q-page class="q-pa-md">
    <div class="row">
      <div class="col-12 col-md-4 q-pa-xs">
        <q-card class="my-card">
          <q-card-section horizontal class="q-mb-none">
            <q-card-section class="col-5 flex flex-left">
              <q-avatar color="primary" rounded text-color="white" icon="dvr"  class="mt-icon" size="80px"/>
            </q-card-section>
            <q-card-section class="q-pt-xs col-7">
              <div class="row">
                <div class="col-12">
                  <div class="text-h4 q-mt-sm float-right">{{endpoints.length}}</div>
                </div>
                <div class="col-12">
                  <div class="text-subtitle1 q-mt-sm float-right">{{$t('endpoint')}}</div>
                </div>
              </div>
            </q-card-section>
          </q-card-section>
          <q-separator />
          <q-card-actions align="right" class="q-pa-xs">
            <q-btn  size="sm" @click="changePage('/endpoint')" class="q-ml-sm" color="blue-14" flat dense>{{$t('view_all')}}</q-btn>
          </q-card-actions>
        </q-card>
      </div>
      <div class="col-12 col-md-4 q-pa-xs">
        <q-card class="my-card">
          <q-card-section horizontal class="q-mb-none">
            <q-card-section class="col-5 flex flex-left">
              <q-avatar color="deep-purple" rounded text-color="white" icon="description"  class="mt-icon" size="80px"/>
            </q-card-section>
            <q-card-section class="q-pt-xs col-7">
              <div class="row">
                <div class="col-12">
                  <div class="text-h4 q-mt-sm float-right">{{swaggers.length}}</div>
                </div>
                <div class="col-12">
                  <div class="text-subtitle1 q-mt-sm float-right">{{$t('swagger')}}</div>
                </div>
              </div>
            </q-card-section>
          </q-card-section>
          <q-separator />
          <q-card-actions align="right" class="q-pa-xs">
            <q-btn  size="sm" @click="changePage('/swagger')" class="q-ml-sm" color="blue-14" flat dense>{{$t('view_all')}}</q-btn>
          </q-card-actions>
        </q-card>
      </div>
      <div class="col-12 col-md-4 q-pa-xs">
        <q-card class="my-card">
          <q-card-section horizontal class="q-mb-none">
            <q-card-section class="col-5 flex flex-left">
              <q-avatar color="red-6" rounded text-color="white" icon="join_inner"  class="mt-icon" size="80px"/>
            </q-card-section>
            <q-card-section class="q-pt-xs col-7">
              <div class="row">
                <div class="col-12">
                  <div class="text-h4 q-mt-sm float-right">{{aggregates.length}}</div>
                </div>
                <div class="col-12">
                  <div class="text-subtitle1 q-mt-sm float-right">{{$t('aggregation')}}</div>
                </div>
              </div>
            </q-card-section>
          </q-card-section>
          <q-separator />
          <q-card-actions align="right" class="q-pa-xs">
            <q-btn  size="sm" @click="changePage('/aggregation')" class="q-ml-sm" color="blue-14" flat dense>{{$t('view_all')}}</q-btn>
          </q-card-actions>
        </q-card>
      </div>
      <div class="col-12 full-width q-pt-xl">
        <q-img error-src="~/assets/logo.svg" :src="$store.branding.logoUrl" width="150px"  class="relative-position q-mt-xl" style="left: 50% !important;margin-left: -75px;"/>
        <div class="text-h6 text-center q-mt-md text-blue-grey">{{ $store.branding.appName }}</div>
      </div>
    </div>
  </q-page>
</template>

<script>
import { MainStore } from '../stores/main'
const mainStore = MainStore()
export default {
  computed: {
    changePage() { return mainStore.changePage },
    aggregates() { return mainStore.getAggregates },
    endpoints() { return mainStore.getEnpoints },
    swaggers() { return mainStore.getSwagger },
  }
}
</script>
