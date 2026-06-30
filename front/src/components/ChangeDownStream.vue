<template>
  <!-- notice dialogRef here -->
  <q-dialog ref="dialogRef" @hide="onDialogHide">
    <q-card class="q-dialog-plugin">
      <q-card-section>
        <q-form ref="form">
          <div class="row">
            <div class="text-subtitle1 col-12 q-px-xs q-mb-sm">{{$t('change_downstream')}}</div>
            <q-separator />
            <div class="col-12 col-sm-4 q-px-xs">
              <q-select outlined dense v-model="modelValue.sheme" :options="shemes" :label="$t('scheme')" lazy-rules 
                @update:model-value="change"
                :rules="[v=> v && v.length || $t('field_required')]"
              />
            </div>
            <div class="col-12 col-sm-8 q-px-xs">
              <q-input outlined dense v-model="modelValue.path" :label="$t('path')" lazy-rules
                @update:model-value="change"
                :rules="[v=> v && v.length || $t('field_required'), v=> validPath.test(v) || $t('invalid_path')]"
              />
            </div>
            <div class="col-12">
              <div class="row q-mb-sm">
                <div class="col-6 q-px-xs">{{$t('host_port')}}</div>
                <div class="col-6">
                  <q-btn flat  color="positive" size="sm" icon="add" class="float-right" @click="addHostPort">
                    <q-tooltip>{{$t('add')}}</q-tooltip>
                  </q-btn>
                </div>
              </div>
              <div class="row" v-for="(item, index) in modelValue.hostAndPorts" :key="index">
                <div class="col-5 col-sm-8 q-px-xs">
                  <q-input outlined dense v-model="item.Host" :label="$t('host')"
                    @update:model-value="change"
                    :rules="[v=> v && v.length || $t('field_required'), v=> validHost.test(v) || $t('invalid_domain')]"
                  />
                </div>
                <div class="col-3 col-sm-3 q-px-xs">
                  <q-input outlined dense type="number" v-model="item.Port" :label="$t('port')" lazy-rules
                    @update:model-value="change"
                    :rules="[v=> v > 0 || $t('field_required')]"
                  />
                </div>
                <div class="col-4 col-sm-1 q-pt-sm">
                  <q-btn v-show="modelValue.hostAndPorts.length>1" @click="removeHostPort(index)" flat color="negative" size="xs" icon="remove" class="float-left">
                    <q-tooltip>{{$t('remove_host_port')}}</q-tooltip>
                  </q-btn>
                </div>
              </div>
            </div>
          </div>
        </q-form>
      </q-card-section>
      <q-card-actions align="right">
        <q-btn flat color="secondary" label="Cancel" @click="onCancelClick" />
        <q-btn flat color="primary" label="OK" @click="onOKClick" />        
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
import { useDialogPluginComponent } from 'quasar'
import { ref } from 'vue';

export default {
  props: ['modelValue'],
  emits: [
    ...useDialogPluginComponent.emits,
    'update:modelValue'
  ],
  setup () {
    // REQUIRED; must be called inside of setup()
    const { dialogRef, onDialogHide, onDialogOK, onDialogCancel } = useDialogPluginComponent()
    let form = ref(null);
    return {
      dialogRef,
      onDialogHide,
      onOKClick () {
        form._value.validate().then(success=>{
          if(success)
            onDialogOK()
        })
        
      },
      onCancelClick: onDialogCancel,
      form
    }
  },
  data() {
    return {
      validPath: /^(\/[a-zA-Z0-9\-._~!$&'()*+,;=:@]+)*\/?$/,
      validHost: /(([a-zA-Z|\d*|\-|\_])+(\.[a-zA-Z]{2,})*)|((\d{1,3}\.){3}\d{1,3})/,
      shemes: ['http', 'https', 'ws', 'wss']
    }
  },
  methods:{
    change() {
      this.$emit('update:modelValue', this.modelValue)
    },
    addHostPort(){
      this.modelValue.hostAndPorts.push({
        Host: '',
        Port: 0
      })
      this.change()
    },
    removeHostPort(index){
      this.modelValue.hostAndPorts = this.modelValue.hostAndPorts.filter((x,idx)=> idx != index)
      this.change()
    },
  }
}
</script>