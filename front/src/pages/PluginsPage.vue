<template>
  <q-page padding>
    <q-card>
      <q-card-section class="q-pt-md q-pb-none">
        <div class="text-h6 q-pb-none">{{ $t('plugins_config') }}</div>
        <div class="text-caption text-grey-7 q-mt-xs">{{ $t('plugin_upload_help') }}</div>
      </q-card-section>

      <q-card-section>
        <q-banner v-if="!loadedInfo.uploadEnabled" dense class="bg-orange-1 text-orange-9 q-mb-md">
          <template v-slot:avatar>
            <q-icon name="block" />
          </template>
          {{ $t('plugin_upload_disabled') }}
        </q-banner>
        <q-banner v-else-if="!loadedInfo.writable" dense class="bg-orange-1 text-orange-9 q-mb-md">
          <template v-slot:avatar>
            <q-icon name="lock" />
          </template>
          {{ $t('plugin_dir_readonly') }}
        </q-banner>

        <div class="row items-center q-mb-md">
          <q-file
            v-model="file"
            dense
            outlined
            accept=".dll"
            :label="$t('plugin_file')"
            :disable="uploading || !loadedInfo.uploadEnabled"
            class="col-grow"
          >
            <template v-slot:prepend>
              <q-icon name="extension" />
            </template>
          </q-file>
          <q-btn
            class="q-ml-sm"
            color="primary"
            icon="upload"
            :label="$t('upload_plugin')"
            :loading="uploading"
            :disable="!file || !loadedInfo.uploadEnabled"
            @click="upload"
          />
          <q-btn class="q-ml-xs" flat round icon="refresh" @click="load">
            <q-tooltip>{{ $t('update') }}</q-tooltip>
          </q-btn>
        </div>

        <q-table
          flat
          :rows="rows"
          :columns="columns"
          row-key="fileName"
          :rows-per-page-options="[0]"
          :no-data-label="$t('no_data')"
          :loading="loading"
        >
          <template v-slot:body-cell-loaded="props">
            <q-td :props="props">
              <q-badge v-if="props.row.loaded" color="positive" :label="$t('plugin_loaded')" />
              <q-badge v-else color="grey" :label="$t('plugin_pending_restart')" />
            </q-td>
          </template>
          <template v-slot:body-cell-plugins="props">
            <q-td :props="props">
              <q-badge
                v-for="plugin in props.row.plugins"
                :key="plugin.className"
                class="q-py-xs q-px-sm q-ml-xs"
                color="primary"
                text-color="white"
                :label="plugin.name + ' (' + plugin.version + ')'"
              >
                <q-tooltip>{{ plugin.className }}<span v-if="plugin.isGlobal"> · {{ $t('plugin_global') }}</span></q-tooltip>
              </q-badge>
            </q-td>
          </template>
          <template v-slot:body-cell-actions="props">
            <q-td :props="props" class="text-right">
              <q-btn
                flat
                round
                dense
                color="negative"
                icon="delete"
                :disable="uploading || !loadedInfo.uploadEnabled"
                @click="remove(props.row)"
              >
                <q-tooltip>{{ $t('delete') }}</q-tooltip>
              </q-btn>
            </q-td>
          </template>
        </q-table>

        <div class="text-caption text-grey-7 q-mt-md">
          {{ $t('plugin_restart_hint') }}
        </div>
      </q-card-section>
    </q-card>
  </q-page>
</template>

<script>
import { defineComponent } from 'vue'
import { MainStore } from '../stores/main'
import ApiGateway from '../api/api-gateway'

const mainStore = MainStore()

export default defineComponent({
  name: 'PluginsPage',
  data() {
    return {
      loading: false,
      uploading: false,
      file: null,
      rows: [],
      loadedInfo: { uploadEnabled: true, writable: true },
      columns: [
        { name: 'fileName', label: this.$t('plugin_file_name'), field: 'fileName', align: 'left' },
        { name: 'plugins', label: this.$t('plugin_name'), field: 'plugins', align: 'left' },
        { name: 'sizeBytes', label: this.$t('size'), field: 'sizeBytes', align: 'left', format: this.formatBytes },
        { name: 'lastWriteUtc', label: this.$t('update'), field: 'lastWriteUtc', align: 'left', format: this.formatDate },
        { name: 'loaded', label: this.$t('plugin_loaded'), field: 'loaded', align: 'left' },
        { name: 'actions', label: '', field: 'actions', align: 'right' }
      ]
    }
  },
  methods: {
    formatDate(value) {
      if (!value) return ''
      const date = new Date(value)
      return isNaN(date) ? '' : date.toLocaleString()
    },
    formatBytes(value) {
      if (!value) return ''
      if (value < 1024) return value + ' B'
      return (value / 1024).toFixed(1) + ' KB'
    },
    async load() {
      this.loading = true
      try {
        const apiGateway = new ApiGateway()
        const response = await apiGateway.getPlugins()
        this.rows = Array.isArray(response.files) ? response.files : []
        this.loadedInfo = {
          uploadEnabled: response.uploadEnabled !== false,
          writable: response.writable !== false
        }
      } catch (error) {
        this.rows = []
        this.showError(error)
      } finally {
        this.loading = false
      }
    },
    async upload() {
      if (!this.file) return
      this.uploading = true
      try {
        const apiGateway = new ApiGateway()
        const response = await apiGateway.uploadPlugin(this.file)
        if (response.warnings && response.warnings.length) {
          mainStore.toast(response.warnings.join(' '), 'warning')
        }
        mainStore.toast(response.needsRestart ? this.$t('plugin_needs_restart') : this.$t('plugin_uploaded', { name: response.fileName }), response.needsRestart ? 'warning' : 'positive')
        this.file = null
        await this.load()
      } catch (error) {
        this.showError(error)
      } finally {
        this.uploading = false
      }
    },
    remove(row) {
      this.$q.dialog({
        title: this.$t('confirm'),
        message: this.$t('confirm_delete_plugin', { name: row.fileName }),
        cancel: true,
        persistent: true
      }).onOk(async () => {
        try {
          const apiGateway = new ApiGateway()
          await apiGateway.deletePlugin(row.fileName)
          mainStore.toast(this.$t('plugin_needs_restart'), 'warning')
          await this.load()
        } catch (error) {
          this.showError(error)
        }
      })
    },
    showError(error) {
      const message = error?.response?.data?.message || this.$t('unknown_error')
      mainStore.toast(message, 'negative')
    }
  },
  mounted() {
    this.load()
  }
})
</script>
