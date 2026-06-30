<template>
  <div class="fullscreen bg-blue text-white text-center q-pa-md flex flex-center">
    <div>
      <div class="text-h2" style="opacity:.4">
        {{$t('network_error')}}
      </div>
      <q-btn icon="refresh" class="q-mt-xl" color="white" text-color="blue" unelevated to="/" :label="$t('refresh')" no-caps/>
    </div>
  </div>
</template>

<script setup>
defineOptions({
  name: 'ErrorNotFound'
});
</script>
