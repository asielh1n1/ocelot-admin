<template>
  <q-page padding>
    <q-card>
      <q-card-section>
        <div class="text-h6">{{$t('global_config')}}</div>
      </q-card-section>
      <q-card-section>
        <q-form ref="form">
          <q-input v-model="data.BaseUrl" :label="$t('base_url')" lazy-rules :rules="[val => val && val.length > 0 || $t('field_required'),val => isUrl(val) || $t('invalid_url')]" @update:modelValue="change"/>
          <q-input v-model="data.RequestIdKey" :label="$t('request_id')" lazy-rules :rules="[ val => val && val.length > 0 || $t('field_required')]" @update:modelValue="change"/>
          <q-separator class="q-my-md"/>
          <!-- Metadata -->
          <key-value v-model="data.Metadata" title="metadata" @update:modelValue="change" help="metadata"></key-value>
          <q-separator class="q-my-md"/>
          <!-- Authentication Options -->
          <div class="row">
            <div class="text-subtitle1 col-12 q-px-xs">{{$t('authentication_options')}}</div>
            <div class="col-12 col-sm-12 q-px-xs">
              <q-select outlined dense v-model="data.AuthenticationOptions.AuthenticationProviderKeys" multiple map-options emit-value :options="settings.authenticationProviders" :label="$t('authentication_provider_key')" class="q-mb-sm" clearable @update:modelValue="change"/>              
            </div>
            <div class="col-12 col-sm-6 q-px-xs" v-if="data.AuthenticationOptions && data.AuthenticationOptions.AuthenticationProviderKeys && data.AuthenticationOptions.AuthenticationProviderKeys.length">
              <q-select outlined dense v-model="data.AuthenticationOptions.RouteKeys" multiple map-options emit-value :options="routeKeys" :label="$t('router_key')" class="q-mb-sm" clearable @update:modelValue="change"/>              
            </div>
            <div class="col-6 q-px-xs" v-if="data.AuthenticationOptions && data.AuthenticationOptions.AuthenticationProviderKeys && data.AuthenticationOptions.AuthenticationProviderKeys.length">
              <q-select outlined bottom-slots dense multiple v-model="data.AuthenticationOptions.AllowedScopes" :label="$t('allowed_scopes')" use-input use-chips
                hide-dropdown-icon
                input-debounce="0"
                new-value-mode="add"
                class="q-mb-sm"
                @update:modelValue="change">
                <template v-slot:hint>{{$t('enter_add_scope')}}</template>
              </q-select>
            </div>
          </div>
          <q-separator class="q-my-md"/>
          <!-- Cache Options -->
          <div class="row">
            <div class="text-subtitle1 col-12 q-px-xs">
              {{$t('cache_options')}}
              <q-btn flat round color="warning" size="sm" icon="help_outline" class="q-ml-sm float-right" @click="$store.showHelp($t('cache_options'), 'cache_options')">
                <q-tooltip>{{$t('click_see_help')}}</q-tooltip>
              </q-btn>
            </div>
            <div class="col-6 q-mb-sm">
              <q-checkbox v-model="enableCache" :label="$t('enable_cache_options')" @update:model-value="changeEnableCache"/>
            </div>
            <div v-if="enableCache" class="col-6 q-px-xs q-mb-sm">
              <q-input outlined dense v-model="data.CacheOptions.TtlSeconds" :label="$t('ttl_econds')" />
            </div>
            <div v-if="enableCache" class="col-12 q-px-xs">
              <q-select  outlined dense v-model="data.CacheOptions.RouteKeys" multiple map-options emit-value :options="routeKeys" :label="$t('router_key')" class="q-mb-sm" clearable @update:modelValue="change"/>              
            </div>
          </div>
          <q-separator class="q-my-md"/>
          <!-- Timeout -->
          <div class="row">
            <div class="text-subtitle1 col-12 q-px-xs">
              {{$t('timeout')}}
              <q-btn flat round color="warning" size="sm" icon="help_outline" class="q-ml-sm float-right" @click="$store.showHelp($t('timeout'), 'timeout')">
                <q-tooltip>{{$t('click_see_help')}}</q-tooltip>
              </q-btn>
            </div>
            <div class="col-12 q-px-xs q-mb-sm">
              <q-input type="number" outlined dense v-model.number="data.Timeout" @update:modelValue="change"/>
            </div>
          </div>
          <q-separator  class="q-mb-sm q-mt-sm q-mx-sm"/>
          <!-- QoSOptions Options -->
          <div class="row">
            <div class="text-subtitle1 col-12 q-px-xs">
              {{$t('qos_options')}}
              <q-btn flat round color="warning" size="sm" icon="help_outline" class="q-ml-sm float-right" @click="$store.showHelp($t('qos_options'), 'qos_options')">
                <q-tooltip>{{$t('click_see_help')}}</q-tooltip>
              </q-btn>
            </div>
            <div class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <q-checkbox v-model="enableQoS" :label="$t('enable_qos_options')" @update:model-value="changeEnableQoS"/>
            </div>
            <div v-if="enableQoS" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <q-input type="number" outlined dense v-model.number="data.QoSOptions.MinimumThroughput" :label="$t('exceptions_allowed_before_breaking')" @update:modelValue="change"/>
            </div>
            <div  v-if="enableQoS" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <q-input type="number" mask="" outlined dense v-model.number="data.QoSOptions.BreakDuration" :label="$t('duration_of_break')" @update:modelValue="change"/>
            </div>
            <div v-if="enableQoS" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <q-input type="number" outlined dense v-model.number="data.QoSOptions.Timeout" :label="$t('timeout_value')" @update:modelValue="change"/>
            </div>
            <div v-if="enableQoS" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <q-input type="number" outlined dense v-model.number="data.QoSOptions.FailureRatio" :label="$t('failure_ratio')" @update:modelValue="change"/>
            </div>
            <div v-if="enableQoS" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <q-input type="number" outlined dense v-model.number="data.QoSOptions.SamplingDuration" :label="$t('sampling_duration')" @update:modelValue="change"/>
            </div>
            <div v-if="enableQoS" class="col-12 q-px-xs">
              <q-select  outlined dense v-model="data.QoSOptions.RouteKeys" multiple map-options emit-value :options="routeKeys" :label="$t('router_key')" class="q-mb-sm" clearable @update:modelValue="change"/>              
            </div>
          </div>
          <q-separator  class="q-mb-sm q-mt-sm q-mx-sm"/>
          <!-- Header Transformation -->
          <div class="text-subtitle1 col-12 q-px-xs q-mb-sm">
            {{$t('headers_transformation')}}
            <q-btn flat round color="warning" size="sm" icon="help_outline" class="q-ml-sm float-right" @click="$store.showHelp($t('headers_transformation'), 'header_transformation')">
              <q-tooltip>{{$t('click_see_help')}}</q-tooltip>
            </q-btn>
          </div>
          <!-- Upstream Header Transform -->
          <key-value v-model="data.UpstreamHeaderTransform" title="upstream_header_transform" @update:modelValue="change"></key-value>
          <!-- Downstream Header Transform -->
          <key-value v-model="data.DownstreamHeaderTransform" title="downstream_header_transform" @update:modelValue="change"></key-value>
          <q-separator  class="q-mb-sm q-mt-sm q-mx-sm"/>
          <!-- Load Balancer -->
          <div class="row">
            <div class="text-subtitle1 col-12 q-px-xs">
              {{$t('load_balancer')}}
              <q-btn flat round color="warning" size="sm" icon="help_outline" class="q-ml-sm float-right" @click="$store.showHelp($t('load_balancer'), 'load_balancer')">
              <q-tooltip>{{$t('click_see_help')}}</q-tooltip>
            </q-btn>
            </div>
            <div class="col-12 q-px-xs q-mb-md">
              <q-select map-options emit-value outlined dense v-model="data.LoadBalancerOptions.Type" :options="loadBalancerOptions" @update:modelValue="change"/>
            </div>
            <div class="col-12 col-sm-6 q-px-xs q-mb-md" v-if="data.LoadBalancerOptions.Type == 'CookieStickySessions'">
              <q-input outlined dense :label="$t('key')" v-model="data.LoadBalancerOptions.Key" @update:modelValue="change"/>
            </div>
            <div class="col-12 col-sm-6 q-px-xs q-mb-md" v-if="data.LoadBalancerOptions.Type == 'CookieStickySessions'">
              <q-input type="number" outlined dense :label="$t('expiry')" v-model="data.LoadBalancerOptions.Expiry" @update:modelValue="change"/>
            </div>
            <div v-if="data.LoadBalancerOptions.Type != 'NoLoadBalancer'" class="col-12 q-px-xs">
              <q-select  outlined dense v-model="data.LoadBalancerOptions.RouteKeys" multiple map-options emit-value :options="routeKeys" :label="$t('router_key')" class="q-mb-sm" clearable @update:modelValue="change"/>              
            </div>
          </div>
          <q-separator  class="q-mb-sm q-mt-sm q-mx-sm"/>
          <!-- Rate Limit Options -->
          <div class="row">
            <div class="text-subtitle1 col-12 q-px-xs">
              {{$t('rate_limiting')}}
              <q-btn flat round color="warning" size="sm" icon="help_outline" class="q-ml-sm float-right" @click="$store.showHelp($t('rate_limiting'), 'rate_limit')">
                <q-tooltip>{{$t('click_see_help')}}</q-tooltip>
              </q-btn>
            </div>
            <div class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <q-checkbox v-model="enableRateLimiting" :label="$t('enable_rate_limit')" @update:model-value="changeEnableRateLimit"/>
            </div>
            <div v-if="enableRateLimiting" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <q-checkbox v-model="data.RateLimitOptions.EnableHeaders" :label="$t('enable_rate_limit_headers')" @update:modelValue="change"/>
            </div>
            <div v-if="enableRateLimiting" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <div class="row">
                <div class="col-8">
                  <q-input type="number" dense v-model="rateLimitPeriodValue" :label="$t('period')" class="q-pr-none" style="border-radius: 4px 0px 0px 4px;" @update:model-value="data.RateLimitOptions.Period = rateLimitPeriodValue+rateLimitPeriodTime" @update:modelValue="change"/>
                </div>
                <div class="col-4">
                  <q-select dense v-model="rateLimitPeriodTime" :options="optionTimes" style="border-radius: 0px 4px 4px 0px;" map-options emit-value @update:model-value="data.RateLimitOptions.Period = rateLimitPeriodValue+rateLimitPeriodTime" @update:modelValue="change"/>
                </div>
              </div>
            </div>
            <div v-if="enableRateLimiting" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <div class="row">
                <div class="col-8">
                  <q-input type="number" dense v-model="rateLimitWaitValue" :label="$t('period_timespan')" class="q-pr-none" style="border-radius: 4px 0px 0px 4px;" @update:model-value="data.RateLimitOptions.Wait = rateLimitWaitValue+rateLimitWaitTime" @update:modelValue="change"/>
                </div>
                <div class="col-4">
                  <q-select dense v-model="rateLimitWaitTime" :options="optionTimes" style="border-radius: 0px 4px 4px 0px;" map-options emit-value @update:model-value="data.RateLimitOptions.Wait = rateLimitWaitValue+rateLimitWaitTime" @update:modelValue="change"/>
                </div>
              </div>
            </div>
            <div v-if="enableRateLimiting" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <q-input type="number" outlined dense v-model.number="data.RateLimitOptions.Limit" :label="$t('limit')" @update:modelValue="change"/>
            </div>
            <div v-if="enableRateLimiting" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <q-input outlined dense v-model.number="data.RateLimitOptions.ClientIdHeader" label="Client Id Header" @update:modelValue="change"/>
            </div>
            <div v-if="enableRateLimiting" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <q-input type="number" outlined dense v-model.number="data.RateLimitOptions.StatusCode" :label="$t('rate_limit_status_code')" @update:modelValue="change"/>
            </div>
            <div v-if="enableRateLimiting" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <q-input outlined dense v-model.number="data.RateLimitOptions.QuotaMessage" :label="$t('rate_limit_quota_message')" @update:modelValue="change"/>
            </div>
            <div v-if="enableRateLimiting" class="col-12 q-px-xs">
              <q-select  outlined dense v-model="data.RateLimitOptions.RouteKeys" multiple map-options emit-value :options="routeKeys" :label="$t('router_key')" class="q-mb-sm" clearable @update:modelValue="change"/>              
            </div>
            <div v-if="enableRateLimiting" class="col-12 q-px-xs q-mb-sm">
              <q-select outlined bottom-slots dense multiple v-model="data.RateLimitOptions.ClientWhitelist" :label="$t('client_whitelist')" use-input use-chips
                hide-dropdown-icon
                input-debounce="0"
                new-value-mode="add"
                @update:modelValue="change"
              >
                <template v-slot:hint>{{$t('enter_add_scope')}}</template>
              </q-select>
            </div>
          </div>
        </q-form>
      </q-card-section>
    </q-card>
  </q-page>
</template>

<script>
import { defineComponent } from 'vue'
import { MainStore } from '../stores/main'
import { Validate } from '../util/validate'
import  KeyValue  from '../components/KeyValue.vue'
const mainStore = MainStore()

export default defineComponent({
  name: 'GlobalConfigPage',
  components: { KeyValue },
  data(){
    return {
      data: {
        BaseUrl: '',
        RequestIdKey: '',
        Metadata: {},
        AuthenticationOptions: {
          AuthenticationProviderKeys: [],
          AllowedScopes: [],
          AllowAnonymous: false
        },
        LoadBalancerOptions: {
          Type: 'NoLoadBalancer',
          RouteKeys: []
        },
        RateLimitOptions: {
          RouteKeys: [],
          ClientWhitelist: [],
          EnableRateLimiting: false,
          Period: '1s',
          Wait: '1s',
          Limit:1,
          EnableHeaders: true,
          ClientIdHeader: 'Oc-Client',
          StatusCode: 429,
          QuotaMessage: 'Quota Exceeded'
        }
      },
      enableCache: false,
      enableQoS: false,
      enableRateLimiting: false,
      loadBalancerOptions: [
        { label:'No Load Balancer', value:'NoLoadBalancer' },
        { label:'Least Connection', value:'LeastConnection' },
        { label:'Round Robin', value:'RoundRobin' },
        { label:'Cookie Sessions', value:'CookieStickySessions' },
      ],
      optionTimes: [{ label: mainStore.translate('millisecond', 2), value: 'ms'},{ label: mainStore.translate('second', 2), value: 's'},{ label: mainStore.translate('minute', 2), value: 'm'}, { label: mainStore.translate('hour', 2), value: 'h'}, , { label: mainStore.translate('day', 2), value: 'h'}],
      rateLimitPeriodValue: '1',
      rateLimitPeriodTime: 's',
      rateLimitWaitValue: '1',
      rateLimitWaitTime: 's',
    }
  },
  computed: {
    isUrl(){ return Validate.isUrl },
    settings(){return mainStore.settings},
    routeKeys(){return mainStore.getEnpoints.filter(x=> x.Key).map(x=> x.Key)}
  },
  methods: {
    loadConfig(){
      let data = JSON.parse(JSON.stringify(mainStore.getGlobalConfig))
      if(!data.AuthenticationOptions){
        data.AuthenticationOptions = {
          AuthenticationProviderKeys: [],
          AllowedScopes: [],
          AllowAnonymous: false
        }
      }
      this.enableCache = data.CacheOptions && Object.keys(data.CacheOptions).length > 0 ? true : false
      this.enableQoS = data.QoSOptions && Object.keys(data.QoSOptions).length > 0 ? true : false
      if(data.CacheOptions){
        this.enableCache = true
      }
      if(data.QoSOptions){
        this.enableQoS = true
      }
      if(!data.LoadBalancerOptions){
        data.LoadBalancerOptions = {
          Type: 'NoLoadBalancer',
          RouteKeys: [],
          Key: null,
          Expiry: 3600000
        }
      }

      this.enableRateLimiting = data.RateLimitOptions && Object.keys(data.RateLimitOptions).length > 0 ? true : false
      if(this.enableRateLimiting){
        this.rateLimitPeriodValue = data.RateLimitOptions.Period.replace(/\D/g, "");
        this.rateLimitPeriodTime = data.RateLimitOptions.Period.replace(/\d/g, "");
        this.rateLimitWaitValue = data.RateLimitOptions.Wait.replace(/\D/g, "");
        this.rateLimitWaitTime = data.RateLimitOptions.Wait.replace(/\d/g, "");
      } else {
        this.rateLimitPeriodValue = 1
        this.rateLimitPeriodTime = 's'
        this.rateLimitWaitValue = 1
        this.rateLimitWaitTime = 's'
      }
      this.data = data
    },
    change(){
      this.$refs.form.validate().then(success=>{
        if(success)
          mainStore.setGlobalConfig(this.data)
      })
    },
    changeEnableCache(){
      if(this.enableCache){
        this.data.CacheOptions = { TtlSeconds: 300, RouteKeys: [] }
      } else this.data.CacheOptions = undefined
      this.change()
    },
    changeEnableQoS(){
      if(this.enableQoS)
        this.data.QoSOptions = { RouteKeys: [], MinimumThroughput: 0, BreakDuration: 1000, Timeout: 5000}
      else
        this.data.QoSOptions = undefined
      this.change()
    },
    changeEnableRateLimit(){
      if(this.enableRateLimiting){
        this.data.RateLimitOptions = {
          ClientWhitelist: [],
          EnableRateLimiting: true,
          Period: '1s',
          Wait: '1s',
          Limit:1,
          EnableHeaders: true,
          ClientIdHeader: 'Oc-Client',
          StatusCode: 429,
          QuotaMessage: 'Quota Exceeded'
        }
      } else this.data.RateLimitOptions = undefined
      this.change()
    }
  },
  mounted(){
    this.loadConfig()
  }
})
</script>
