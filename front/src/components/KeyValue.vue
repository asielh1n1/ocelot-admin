<template>
  <div class="row">
    <div class="text-subtitle2 col-6 q-px-xs">{{$t(title)}}</div>
    <div class="col-6 q-px-xs">
      <q-btn flat v-show="help" round color="warning" size="sm" icon="help_outline" class="q-ml-sm float-right" @click="$store.showHelp($t(title), help)">
        <q-tooltip>{{$t('click_see_help')}}</q-tooltip>
      </q-btn>
      <q-btn flat round color="positive" size="sm" icon="add" class="q-ml-sm float-right" @click="addItem">
        <q-tooltip>{{$t('add')}}</q-tooltip>
      </q-btn>
    </div>
    <div class="col-12" v-for="(item, index) in value" :key="index">
      <div class="row border">
        <div class="col-5 col-sm-4 q-px-xs">
          <q-input outlined dense v-model="item.key" :label="$t(labelKey)" 
            :rules="[v=> v && v.length || $t('field_required')]"
            @blur="changeModel"
          />
        </div>
        <div class="col-5 col-sm-7 q-px-xs">
          <q-input outlined dense v-model="item.value" :label="$t(labelValue)" lazy-rules 
            :rules="[v=> v && v.length || $t('field_required')]"
            @blur="changeModel"
          />
        </div>
        <div class="col-2 col-sm-1 q-pt-sm">
          <q-btn @click="removeItem(index)" flat round color="negative" size="sm" icon="remove" class="float-left">
            <q-tooltip>{{$t('remove')}}</q-tooltip>
          </q-btn>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
export default {
  name: 'ComponentKeyValue',
  props: {
    modelValue: {
      type: Object,
    },
    labelKey: {
      type: String,
      default() {
        return 'key'
      }
    },
    labelValue: {
      type: String,
      default() {
        return 'value'
      }
    },
    title: {
      type: String,
      default() {
        return 'Title'
      }
    },
    help: {
      type: String
    }
  },
  emits: ['update:modelValue'],
  computed: {
    value: {
      get() {
        return Object.entries(this.modelValue || {}).map(x=>{
          return { key: x[0], value: x[1]}
        })
      },
      set(value) {
        this.$emit('update:modelValue', Object.fromEntries(value.map(x=>[x.key,x.value])))
      }
    }
  },
  methods:{
    addItem(){
      this.value.push({key:'', value:''})
      this.changeModel()
    },
    removeItem(index){
      this.value = this.value.filter((x,idx)=> idx != index)
    },
    changeModel(){
      this.$emit('update:modelValue', Object.fromEntries(this.value.map(x=>[x.key,x.value])))
    }
  }
}
</script>
