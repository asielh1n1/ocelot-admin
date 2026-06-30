<template>
  <q-dialog v-model="showDialog">
    <q-card style="width: 80vw;">
      <q-card-section>
        <div class="text-h6">{{$t(formMode)}} {{$t('aggregation')}}</div>
      </q-card-section>
      <q-separator />
      <q-card-section style="max-height:75vh" class="scroll">
        <q-form ref="form" greedy>
          <!-- Services Name -->
          <div class="row">
            <div class="col-12">
              <q-input :readonly="formMode=='update'" outlined dense v-model="value.UpstreamPathTemplate" :label="$t('upstream_path')" lazy-rules 
              :rules="[v=> v && v.length || $t('field_required'), v=> !existUpstream(v) || $t('key_already_exist'), v=> validPath.test(v) || $t('invalid_path')]"/>
            </div>
            <div class="col-12">
              <q-select multiple outlined dense v-model="value.RouteKeys" :options="routeKeys" :label="$t('router_key')" lazy-rules 
              :rules="[v=> v && v.length || $t('field_required')]" clearable/>
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
const mainStore = MainStore()

export default {
  name: 'ComponentAggregationForm',
  props: ['data', 'show', 'mode', 'existUpstream'],
  emits: ['update:show', , 'update:form'],
  watch: {
    data(newVal){
      this.value =  JSON.parse(JSON.stringify(newVal));
    }
  },
  computed: {
    showDialog: {
      get() {return this.show},
      set(value) {this.$emit('update:show', value)}
    },
    formMode(){return this.mode},
    routeKeys(){return mainStore.getEnpoints.filter(x=> x.Key).map(x=> x.Key)}
  },
  data(){
    return {
      value: {},
      validPath: /^(\/[a-zA-Z0-9\-._~!$&'()*+,;=:@\{\}\^\?]+)*\/?$/
    }
  },
  methods: {
    save(){
      this.$refs.form.validate().then(success=>{
        if(success)
          this.$emit('update:form', this.value)
      })
    }
  }
}
</script>
