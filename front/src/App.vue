<template>
  <router-view />
</template>

<script setup>
import { setCssVar } from 'quasar'
const primaryColor = window.__APP_CONFIG__ ? window.__APP_CONFIG__.primaryColor : '#03a9f4'
setCssVar('primary', primaryColor)
defineOptions({
  name: 'App'
});
</script>
