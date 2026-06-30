<template>
  <input type="file" id="file" style="display: none" accept=".json"/>
  <q-page padding>
    <q-card>
      <q-card-section class="q-pt-md q-pb-none">
        <div class="text-h6 q-pb-none">{{$t('endpoint', 2)}}</div>
      </q-card-section>
      <q-card-section class="q-py-none">
        <q-table 
          flat
          :rows="rows" 
          :columns="columns" 
          row-key="id" 
          selection="multiple"
          v-model:selected="selected"
          :rows-per-page-options="[0]"
          class="q-px-none"
        >
          <template v-slot:top>
            <div class="row full-width">
              <div class="col-12 col-sm-9 q-pt-md">
                <q-btn outline color="positive" icon="add" size="xs" @click="showFormRoute({}, 'add')" class="q-mb-sm">
                  <q-tooltip>{{$t('add')}}</q-tooltip>
                </q-btn>
                <q-btn outline :disable="selected.length != 1" class="q-ml-xs q-mb-sm" color="info" icon="edit" size="xs" @click="showFormRoute(selected[0], 'update')">
                  <q-tooltip>{{$t('update')}}</q-tooltip>
                </q-btn>
                <q-btn outline :disable="!selected.length" class="q-ml-xs q-mb-sm" color="negative" icon="delete" size="xs" @click="remove">
                  <q-tooltip>{{$t('delete')}}</q-tooltip>
                </q-btn>
                <q-btn outline :disable="selected.length != 1" class="q-ml-xs q-mb-sm" color="warning" icon="difference" size="xs" @click="showFormRoute(selected[0], 'add')">
                  <q-tooltip>{{$t('duplicate')}}</q-tooltip>
                </q-btn>
                <q-btn outline :disable="!selected.length" class="q-ml-xs q-mb-sm" color="indigo" icon="logout" size="xs" @click="changeDownStream">
                  <q-tooltip>{{$t('change_downstream')}}</q-tooltip>
                </q-btn>
                <q-btn outline class="q-ml-xs q-mb-sm" color="teal" icon="file_download" size="xs" @click="exportEndpoint">
                  <q-tooltip>{{$t('export_endpoints')}}</q-tooltip>
                </q-btn>
                <q-btn outline class="q-ml-xs q-mb-sm" color="teal" icon="file_upload" size="xs" @click="openFileDialog">
                  <q-tooltip>{{$t('import_endpoints')}}</q-tooltip>
                </q-btn>
              </div>
              <div class="col-12 col-sm-3">
                <q-input dense v-model="filter" :placeholder="$t('search')" @update:model-value="changeFilter" class="full-width">
                  <template v-slot:append>
                    <q-icon bordered size="xs" class="q-ml-sm" :color="searchRegExp ? 'blue-grey-14' : 'blue-grey-5'" @click="changeSearchMode">
                      <q-tooltip>{{searchRegExp ? $t('click_skip_search_regular_expression') : $t('click_search_regular_expression') }}</q-tooltip>
                      <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="currentColor" class="bi bi-regex" viewBox="0 0 16 16">
                        <path fill-rule="evenodd" d="M3.05 3.05a7 7 0 0 0 0 9.9.5.5 0 0 1-.707.707 8 8 0 0 1 0-11.314.5.5 0 1 1 .707.707m9.9-.707a.5.5 0 0 1 .707 0 8 8 0 0 1 0 11.314.5.5 0 0 1-.707-.707 7 7 0 0 0 0-9.9.5.5 0 0 1 0-.707M6 11a1 1 0 1 1-2 0 1 1 0 0 1 2 0m5-6.5a.5.5 0 0 0-1 0v2.117L8.257 5.57a.5.5 0 0 0-.514.858L9.528 7.5 7.743 8.571a.5.5 0 1 0 .514.858L10 8.383V10.5a.5.5 0 1 0 1 0V8.383l1.743 1.046a.5.5 0 0 0 .514-.858L11.472 7.5l1.785-1.071a.5.5 0 1 0-.514-.858L11 6.617z"/>
                      </svg>
                    </q-icon>
                  </template>
                </q-input>
              </div>
            </div>
          </template>
          <template v-slot:body-cell-UpstreamHttpMethod="props">
            <q-td :props="props">
              <q-badge class="q-py-xs q-px-sm q-ml-xs" v-for="method in props.value" :key="method" :color="methodsColors[method]" text-color="white" :label="method" />
            </q-td>
          </template>
          <template v-slot:body-cell-DownstreamHttpMethod="props">
            <q-td :props="props">
              <q-badge class="q-py-xs q-px-sm q-ml-xs" :color="methodsColors[props.value]" text-color="white" :label="props.value" />
            </q-td>
          </template>
        </q-table>
      </q-card-section>
    </q-card>
    <form-route v-model:data="formRoute.data" v-model:show="formRoute.show" :mode="formRoute.mode" @update:form="updateFormRoute" :existEnpoint="existEnpoint" :existKey="existKey"></form-route>
  </q-page>
</template>

<script>
import { defineComponent } from 'vue'
import { exportFile } from 'quasar'
import  Route  from '../components/Route.vue'
import  ChangeDownStream  from '../components/ChangeDownStream.vue'
import  FormRoute  from '../components/RouteForm.vue'
import { MainStore } from '../stores/main'
import { Validate } from '../util/validate'
const mainStore = MainStore()

export default defineComponent({
  components: { Route, FormRoute },
  name: 'GlobalConfigPage',
  data(){
    return {
      columns: [
        { name: 'UpstreamHttpMethod', align: 'left', label: this.$t('upstream_method'), field: 'UpstreamHttpMethod'},
        { name: 'UpstreamPathTemplate', align: 'left', label: this.$t('upstream_path'), field: 'UpstreamPathTemplate'},
        { name: 'DownstreamHttpMethod', align: 'left', label: this.$t('downstream_method'), field: 'DownstreamHttpMethod'},
        { name: 'DownstreamPathTemplate', align: 'left', label: this.$t('downstream_path'), field: 'DownstreamPathTemplate'},
        { name: 'DownstreamHostAndPorts', align: 'left', label: this.$t('downstream_host_port'), field: row=> row.DownstreamHostAndPorts.map(x=> x.Host+':'+x.Port).join(', ')},
        { name: 'Key', align: 'left', label: this.$t('service_key'), field: 'Key'},
      ],
      rows: [],
      selected: [],
      formRoute: {
        show: false,
        data: {},
        mode: 'update'
      },
      filter: '',
      searchRegExp: false,
      methodsColors:{
        'Get': 'info',
        'Post': 'light-green',
        'Put': 'amber',
        'Delete': 'red'
      },
      changeDownStreamOptions: {
        sheme: 'http',
        path: '',
        hostAndPorts: [{Host:'', Port: 0}]
      }
    }
  },
  watch: {
    endpoints(){
      this.changeFilter()
    }
  },
  computed: {
    isUrl(){ return Validate.isUrl },
    endpoints() { return mainStore.getEnpoints }
  },
  methods: {
    showFormRoute(route, mode){
      this.formRoute.mode = mode
      this.formRoute.data = route
      if(mode == 'add')
        this.formRoute.data.id = window.crypto.randomUUID()
      this.formRoute.show = true
      
    },
    updateFormRoute(route) {      
      if(this.formRoute.mode == 'update'){
        mainStore.setRoute(route)
      }else if(this.formRoute.mode == 'add'){
        mainStore.addRoute(route)
      }
      this.formRoute.show = false
    },
    remove(){
      this.$q.dialog({
        title: this.$t('confirm'),
        message: this.$t('confirm_delete_endpoint'),
        cancel: true,
        persistent: true
      }).onOk(() => {
        mainStore.deleteRoute(this.selected)
        this.selected = []
      })
    },
    changeSearchMode(){
      this.searchRegExp = !this.searchRegExp
    },
    changeFilter(){
      this.selected = []
      if(!this.filter){
        this.rows = this.endpoints
      }else if(this.filter && this.searchRegExp){
        const re = new RegExp(this.filter);
        this.rows = this.endpoints.filter(x=> 
          re.test(x.UpstreamPathTemplate)
          || re.test(x.DownstreamPathTemplate)
          || re.test(x.Key)
          || re.test(x.DownstreamHostAndPorts.map(x=> x.Host+':'+x.Port).join(':'))
        )
      }else{
        const search = this.filter.toLocaleLowerCase()
        this.rows = this.endpoints.filter(x=> 
          x.UpstreamPathTemplate.includes(search)
          || x.DownstreamPathTemplate.includes(search)
          || x.Key.includes(search)
          || x.DownstreamHostAndPorts.map(x=> x.Host+':'+x.Port).join(':').includes(search)
        )
      }
    },
    changeDownStream() {
      this.changeDownStreamOptions = {
        sheme: 'http',
        path: '',
        hostAndPorts: [{Host:'', Port: 0}]
      }
      this.$q.dialog({
        component: ChangeDownStream,
        componentProps: {
          modelValue: this.changeDownStreamOptions
        }
      }).onOk(data => {
        let routes = JSON.parse(JSON.stringify(this.selected))
        routes.forEach(x=>{
          x.DownstreamPathTemplate = this.changeDownStreamOptions.path
          x.DownstreamScheme = this.changeDownStreamOptions.sheme
          x.DownstreamHostAndPorts = this.changeDownStreamOptions.hostAndPorts
          mainStore.setRoute(x)
        })
      })
    },
    existKey(endpoint){
      return this.endpoints.some(x=> x.id!= endpoint.id && x.Key == endpoint.Key)
    },
    existEnpoint(endpoint){
      return this.endpoints.some(x=> x.id!= endpoint.id &&  x.UpstreamPathTemplate == endpoint.UpstreamPathTemplate && x.UpstreamHttpMethod.every(y=> endpoint.UpstreamHttpMethod && endpoint.UpstreamHttpMethod.some(j=> j==y)))
    },
    exportEndpoint(){
      let status = null;
      let content = null
      if(this.selected.length) {
        content = JSON.parse(JSON.stringify(this.selected))
      } else {
        content = JSON.parse(JSON.stringify(this.rows))
      }
      content = content.map(x=>{
        delete x.id
        return x
      })
      status = exportFile('Endpoints.json', JSON.stringify(content))
      if (!status) {
        mainStore.toast(this.$t('error_export'), 'negative')
      }
    },
    openFileDialog () {
      const fileDom = window.document.getElementById('file')
      fileDom.click()
    },
    importEndpoints(json){
      mainStore.loading(true)
      let result = []
      try {
        for(let i in json){
          if(!this.existEnpoint(json[i]))
            result.push(json[i])
        }
        if(result.length)
          mainStore.setEndpoints(this.rows.concat(result))
      } catch (error) {
        console.error('Import Endpoint:', error.message);
        mainStore.toast(this.$t('invalid_endpoint_format'), 'negative')
      }
      finally{ mainStore.loading(false) }
    }
  },
  mounted(){
    const fileDom = window.document.getElementById('file')
    const _this = this
    fileDom.addEventListener('change', function (event) {
      const input = event.target
      const reader = new FileReader()
      reader.onload = function () {
        try {
          _this.importEndpoints(JSON.parse(reader.result))
        } catch (error) {
          mainStore.toast(this.$t('incorrect_json'), 'negative')
        } 
        fileDom.value = ''
      }
      reader.readAsText(input.files[0])
    }, false)
    this.changeFilter()
  }
})
</script>
