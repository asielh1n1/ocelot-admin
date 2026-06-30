<template>
  <q-dialog v-model="showDialog">
    <q-card style="max-width: 80vw;">
      <q-card-section>
        <div class="text-h6">{{$t(formMode)}} {{$t('swagger')}}</div>
      </q-card-section>
      <q-separator />
      <q-card-section style="max-height:75vh" class="scroll">
        <q-form ref="form" greedy>
          <!-- Services Name -->
          <div class="row">
            <div class="col-12 col-sm-6 q-px-xs">
              <q-input :readonly="formMode=='update'" outlined dense v-model="value.Key" :label="$t('key')" lazy-rules 
              :rules="[v=> v && v.length || $t('field_required'), v=> !existKey(v) || $t('key_already_exist')]"
              />
            </div>
          </div>
          <q-separator class="q-mb-sm q-mx-sm"/>
          <div class="row">
            <div class="col-12">
              <div class="row q-mb-sm">
                <div class="col-6 q-px-xs">{{$t('service_config')}}</div>
                <div class="col-6">
                  <q-btn flat  color="positive" size="sm" icon="add" class="float-right" @click="addConfig">
                    <q-tooltip>{{$t('add')}}</q-tooltip>
                  </q-btn>
                </div>
              </div>
              <div class="row" v-for="(item, index) in value.Config" :key="index">
                <div class="col-12 col-sm-3 q-px-xs">
                  <q-input outlined dense v-model="item.Name" :label="$t('name')" 
                    :rules="[v=> v && v.length || $t('field_required')]"
                  />
                </div>
                <div class="col-12 col-sm-3 q-px-xs">
                  <q-input outlined dense v-model="item.Version" :label="$t('version')" lazy-rules 
                    :rules="[v=> v && v.length || $t('field_required')]"
                  />
                </div>
                <div class="col-10 col-sm-5 q-px-xs">
                  <q-input outlined dense v-model="item.Url" :label="$t('url')" lazy-rules 
                    :rules="[v=> v && v.length || $t('field_required'), v=> isValidUrl(v)|| $t('invalid_url')]"
                  />
                </div>
                <div class="col-2 col-sm-1 q-pt-sm">
                  <q-btn @click="removeConfig(index)" :disable="value.Config.length<=1" flat round color="negative" size="sm" icon="remove" class="float-left">
                    <q-tooltip>{{$t('delete')}}</q-tooltip>
                  </q-btn>
                </div>
                <q-separator class="q-mb-sm q-mx-sm"/>
              </div>
            </div>
          </div>
        </q-form>
      </q-card-section>
      <q-separator />
      <q-card-actions align="right">
        <q-btn flat :label="$t('cancel')" color="secondary" v-close-popup />
        <q-btn flat :label="$t(formMode)" color="primary" @click="save"/>
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
import { MainStore } from '../stores/main'
import  KeyValue  from './KeyValue.vue'
const mainStore = MainStore()

export default {
  name: 'ComponentSwaggerForm',
  components: { KeyValue },
  props: ['data', 'show', 'mode', 'existKey'],
  emits: ['update:show', , 'update:form'],
  watch: {
    data(newVal){
      this.value =  JSON.parse(JSON.stringify(newVal));
      if(!this.value.Config){
        this.value.Config = [{
          Name: '',
          Version: '',
          Url: ''
        }]
      }
    }
  },
  computed: {
    showDialog: {
      get() {return this.show},
      set(value) {this.$emit('update:show', value)}
    },
    formMode(){return this.mode}
  },
  data(){
    return {
      value: {},
    }
  },
  methods: {
    save(){
      this.$refs.form.validate().then(success=>{
        if(success)
          this.$emit('update:form', this.value)
      })
    },
    addConfig(){
      this.value.Config.push({
        Name: '',
        Version: '',
        Url: ''
      })
    },
    removeConfig(index){
      this.value.Config = this.value.Config.filter((x,idx)=> idx != index)
    },
    isValidUrl(url) {
      try {
        new URL(url);
        return true;
      } catch (err) {
        return false;
      }
    }
  }
}
</script>
