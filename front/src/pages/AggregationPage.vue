<template>
  <q-page padding>
    <q-card>
      <q-card-section class="q-pt-md q-pb-none">
        <div class="text-h6 q-pb-none">{{$t('aggregation_config')}}</div>
      </q-card-section>
      <q-card-section>
        <q-table 
          flat
          :rows="rows" 
          :columns="columns" 
          row-key="UpstreamPathTemplate"
          selection="single"
          v-model:selected="selected"
          :rows-per-page-options="[0]"
        >
          <template v-slot:top-left>
              <q-btn outline color="positive" icon="add" size="xs" @click="showFormAggregation({}, 'add')">
                <q-tooltip>{{$t('add')}}</q-tooltip>
              </q-btn>
              <q-btn outline :disable="selected.length != 1" class="q-ml-xs" color="info" icon="edit" size="xs" @click="showFormAggregation(selected[0], 'update')">
                <q-tooltip>{{$t('update')}}</q-tooltip>
              </q-btn>
              <q-btn outline :disable="selected.length !=1" class="q-ml-xs" color="negative" icon="delete" size="xs" @click="remove">
                <q-tooltip>{{$t('delete')}}</q-tooltip>
              </q-btn>
              <q-btn outline class="q-ml-xs" color="warning" icon="help_outline" size="xs" @click="$store.showHelp($t('aggregation_config'), 'aggregation')">
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
    <aggregation-form v-model:data="formAggregation.data" v-model:show="formAggregation.show" :mode="formAggregation.mode" @update:form="updateFormAggregation" :existUpstream="v=> rawData.some(x=> x.UpstreamPathTemplate == v && formAggregation.mode == 'add')"></aggregation-form>
  </q-page>
  
</template>

<script>
import { defineComponent } from 'vue'
import { MainStore } from '../stores/main'
import  AggregationForm  from '../components/AggregationForm.vue'
const mainStore = MainStore()

export default defineComponent({
  name: 'AggregationPage',
  components: { AggregationForm },
  data(){
    return {
      columns: [
        { name: 'UpstreamPathTemplate', align: 'left', label: this.$t('upstream_path'), field: 'UpstreamPathTemplate'},
        { name: 'RouteKeys', align: 'left', label: this.$t('router_key'), field: row=> row.RouteKeys.join(', ')},
      ],
      selected: [],
      filter: '',
      formAggregation: {
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
        return mainStore.getAggregates
      },
      set(val) {
        mainStore.setAggregates(val)
      }
    }
  },
  methods: {
    changeFilter(){
      if(!this.filter)
        this.rows = this.rawData
      else {
        const val = this.filter.toLowerCase()
        this.rows = this.rawData.filter(x=> x.UpstreamPathTemplate.includes(val))
      }
    },
    showFormAggregation(aggregate, mode){
      this.formAggregation.mode = mode
      this.formAggregation.data = aggregate
      this.formAggregation.show = true
    },
    updateFormAggregation(aggregate){
      if(this.formAggregation.mode == 'update'){
        mainStore.setAggregates(aggregate)
      }else if(this.formAggregation.mode == 'add'){
        mainStore.addAggregates(aggregate)
      }
      this.rows = mainStore.getAggregates
      this.formAggregation.show = false
    },
    remove() {
      this.$q.dialog({
        title: this.$t('confirm'),
        message: this.$t('confirm_delete_aggregation'),
        cancel: true,
        persistent: true
      }).onOk(() => {
        mainStore.deleteAggregates(this.selected[0])
        this.rows = this.rawData
        this.selected = []
      })
    }
  },
  mounted(){
    this.rows = this.rawData
  }
})
</script>
