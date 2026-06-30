<template>
  <q-page padding>
    <q-card>
      <q-card-section class="q-pt-md q-pb-none">
        <div class="text-h6 q-pb-none">{{$t('swagger_config')}}</div>
      </q-card-section>
      <q-card-section>
        <q-table 
          flat
          :rows="rows" 
          :columns="columns" 
          row-key="Key"
          selection="single"
          v-model:selected="selected"
          :rows-per-page-options="[0]"
        >
          <template v-slot:top-left>
              <q-btn outline color="positive" icon="add" size="xs" @click="showFormSwagger({}, 'add')">
                <q-tooltip>{{$t('add')}}</q-tooltip>
              </q-btn>
              <q-btn outline :disable="selected.length != 1" class="q-ml-xs" color="info" icon="edit" size="xs" @click="showFormSwagger(selected[0], 'update')">
                <q-tooltip>{{$t('update')}}</q-tooltip>
              </q-btn>
              <q-btn outline :disable="selected.length !=1" class="q-ml-xs" color="negative" icon="delete" size="xs" @click="remove">
                <q-tooltip>{{$t('delete')}}</q-tooltip>
              </q-btn>
              <q-btn outline class="q-ml-xs" color="warning" icon="help_outline" size="xs" @click="$store.showHelp($t('swagger_config'), 'swagger')">
                <q-tooltip>{{$t('help')}}</q-tooltip>
              </q-btn>
          </template>
          <template v-slot:top-right>
            <q-input dense debounce="300" v-model="filter" :placeholder="$t('search')" @update:model-value="changeFilter">
              <template v-slot:append>
                <q-icon name="search"></q-icon>
              </template>
            </q-input>
          </template>
          <template v-slot:body-cell-Config="props">
            <q-td :props="props">
              <q-badge class="q-py-xs q-px-sm q-ml-xs" v-for="cfg in props.value" :key="cfg.Name" color="positive" text-color="white" :label="cfg.Name+' ( '+cfg.Version+' )'" />
            </q-td>
          </template>
        </q-table>
      </q-card-section>
    </q-card>
    <swagger-form v-model:data="formSwagger.data" v-model:show="formSwagger.show" :mode="formSwagger.mode" @update:form="updateFormSwagger" :existKey="v=> rows.some(x=> x.Key == v && formSwagger.mode == 'add')"></swagger-form>
  </q-page>
  
</template>

<script>
import { defineComponent } from 'vue'
import { MainStore } from '../stores/main'
import  SwaggerForm  from '../components/SwaggerForm.vue'
const mainStore = MainStore()

export default defineComponent({
  name: 'SwaggerPage',
  components: { SwaggerForm },
  data(){
    return {
      columns: [
        { name: 'Key', align: 'left', label: this.$t('key'), field: 'Key'},
        { name: 'Config', align: 'left', label: this.$t('service_name'), field: 'Config'},
      ],
      selected: [],
      filter: '',
      formSwagger: {
        show: false,
        data: {},
        mode: 'update'
      },
      rows: []
    }
  },
  computed: {
    rawData: { 
      get() {
        return mainStore.getSwagger
      },
      set(val) {
        mainStore.setSwagger(val)
      }
    }
  },
  methods: {
    changeFilter(){
      console.log('Filter:', this.filter);
      if(!this.filter)
        this.rows = this.rawData
      else {
        const val = this.filter.toLowerCase()
        this.rows = this.rawData.filter(x=> x.Key.includes(val))
      }
    },
    showFormSwagger(swagger, mode){
      this.formSwagger.mode = mode
      this.formSwagger.data = swagger
      this.formSwagger.show = true
    },
    updateFormSwagger(swagger){
      if(this.formSwagger.mode == 'update'){
        mainStore.setSwagger(swagger)
      }else if(this.formSwagger.mode == 'add'){
        mainStore.addSwagger(swagger)
      }
      this.rows = this.rawData
      this.formSwagger.show = false
    },
    remove() {
      this.$q.dialog({
        title: this.$t('confirm'),
        message: this.$t('confirm_delete_swagger'),
        cancel: true,
        persistent: true
      }).onOk(() => {
        mainStore.deleteSwagger(this.selected[0])
        this.selected = []
        this.rows = this.rawData
      })
    }
  },
  mounted(){
    this.rows = this.rawData
  }
})
</script>
