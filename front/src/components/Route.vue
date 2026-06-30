<template>
  <div class="row q-mb-xs q-pa-xs bg-grey-3">
    <div>
      <q-badge class="q-py-sm q-px-md q-ml-xs" v-for="method in value.UpstreamHttpMethod" :key="method" :color="methodsColors[method]" text-color="white" :label="method" />
    </div>
    <div class="q-mt-xs q-ml-md">
      {{value.UpstreamPathTemplate}}
    </div>
    <q-space/>
    <div>
      <q-btn flat round size="sm" color="orange" icon="content_copy">
        <q-tooltip>{{$t('duplicate_route')}}</q-tooltip>
      </q-btn>
      <q-btn flat round size="sm" icon="edit" color="blue" @click="$emit('edit',this.value, 'update')">
        <q-tooltip>{{$t('edit_route')}}</q-tooltip>
      </q-btn>
    </div>
  </div>
  
</template>

<script>

export default {
  name: 'ComponentRoute',
  props: ['data'],
  emits: ['edit'],
  computed: {
    value() {return this.data}
  },
  data(){
    return {
      httpMethods: ['Get', 'Post', 'Put', 'Delete'],
      methodsColors:{
        'Get': 'info',
        'Post': 'light-green',
        'Put': 'amber',
        'Delete': 'red'
      }
    }
  }
}
</script>
