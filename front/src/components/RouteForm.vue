<template>
  <q-dialog v-model="showDialog">
    <q-card style="max-width: 80vw;">
      <q-card-section class="q-py-xs">
        <div class="text-h6">{{$t(formMode)}} {{$t('endpoint')}}</div>
      </q-card-section>
      <q-separator />
      <q-card-section style="max-height:70vh" class="scroll">
        <q-form ref="form" greedy>
          <!-- Services Name -->
          <div class="row">
            <div class="text-subtitle1 col-12 q-px-xs">{{$t('service_key')}}</div>
            <!-- <div class="col-12 col-sm-6 q-px-xs">
              <q-input outlined dense v-model="value.ServiceName" :label="$t('service_name')" class="q-mb-sm"/>
            </div> -->
            <div class="col-12 col-sm-12 q-px-xs">
              <q-input outlined dense v-model="value.Key" :label="$t('key')" class="q-mb-sm" :rules="[()=> !existKey(value) || $t('key_already_exist')]"/>
            </div>
          </div>
          <q-separator class="q-mb-sm q-mt-sm q-mx-sm"/>
          <!-- Entry route -->
          <div class="row">
            <div class="text-subtitle1 col-12 q-px-xs">
              {{$t('upstream')}}
              <q-btn flat round color="warning" size="sm" icon="help_outline" class="q-ml-sm float-right" @click="$store.showHelp($t('upstream'),'inbound')">
                <q-tooltip>{{$t('click_see_help')}}</q-tooltip>
              </q-btn>
            </div>
            <div class="col-12 col-sm-6 q-px-xs">
              <q-select outlined dense multiple v-model="value.UpstreamHttpMethod" :options="httpMethods" :label="$t('method', 2)" lazy-rules 
                :rules="[v=> v && v.length || $t('field_required')]"
              />
            </div>
            <div class="col-12 col-sm-6 q-px-xs">
              <q-input type="number" outlined dense v-model="value.Priority" :label="$t('priority')" />
            </div>
            <div class="col-12 q-px-xs">
              <q-input outlined dense v-model="value.UpstreamPathTemplate" :label="$t('path')" lazy-rules :rules="[
                  v=> v && v.length || $t('field_required'), 
                  v=> validPath.test(v) || $t('invalid_path'),
                  v=> !existEnpoint(value) || $t('endpoint_already_exist')
                ]"
              />
            </div>
            <div class="col-12 q-mb-md">
              <key-value v-model="value.UpstreamHeaderTemplates" title="inbound_header"></key-value>
            </div>
          </div>
          <q-separator class="q-mb-sm q-mx-sm"/>
          <!-- Destination route -->
          <div class="row">
            <div class="text-subtitle1 col-12 q-px-xs">
              {{$t('downstream')}}
              <q-btn flat round color="warning" size="sm" icon="help_outline" class="q-ml-sm float-right" @click="$store.showHelp($t('downstream'),'inbound')">
                <q-tooltip>{{$t('click_see_help')}}</q-tooltip>
              </q-btn>
            </div>
            <div class="col-12 col-sm-3 q-px-xs">
              <q-select outlined dense v-model="value.DownstreamHttpMethod" :options="httpMethods" :label="$t('method')" lazy-rules 
                :rules="[v=> v && v.length || $t('field_required')]"
              />
            </div>
            <div class="col-12 col-sm-3 q-px-xs">
              <q-select outlined dense v-model="value.DownstreamScheme" :options="shemes" :label="$t('scheme')" lazy-rules 
                :rules="[v=> v && v.length || $t('field_required')]"
              />
            </div>
            <div class="col-12 col-sm-6 q-px-xs">
              <q-input outlined dense v-model="value.DownstreamPathTemplate" :label="$t('path')" lazy-rules 
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
              <div class="row" v-for="(item, index) in value.DownstreamHostAndPorts" :key="index">
                <div class="col-5 col-sm-8 q-px-xs">
                  <q-input outlined dense v-model="item.Host" :label="$t('host')" 
                    :rules="[v=> v && v.length || $t('field_required'), v=> validHost.test(v) || $t('invalid_domain')]"
                  />
                </div>
                <div class="col-3 col-sm-2 q-px-xs">
                  <q-input outlined dense type="number" v-model.number="item.Port" :label="$t('port')" lazy-rules 
                    :rules="[v=> v > 0 || $t('field_required')]"
                  />
                </div>
                <div class="col-4 col-sm-2 q-pt-sm">
                  <q-btn @click="removeHostPort(index)" :disable="value.DownstreamHostAndPorts.length<=1" flat round color="negative" size="sm" icon="remove" class="float-left">
                    <q-tooltip>{{$t('remove_host_port')}}</q-tooltip>
                  </q-btn>
                </div>
              </div>
            </div>
          </div>
          <q-separator class="q-mb-sm q-mx-sm"/>
          <!-- Authentication Options -->
          <div class="row">
            <div class="text-subtitle1 col-12 q-px-xs">{{$t('authentication_options')}}</div>
            <div class="col-12 col-sm-6 q-px-xs">
              <q-checkbox v-model="value.AuthenticationOptions.AllowAnonymous" :label="$t('allow_anonymous')" />
            </div>
            <div class="col-12 col-sm-6 q-px-xs">
              <q-select :disable="value.AuthenticationOptions.AllowAnonymous" outlined dense v-model="value.AuthenticationOptions.AuthenticationProviderKeys" multiple map-options emit-value :options="settings.authenticationProviders" :label="$t('authentication_provider_key')" class="q-mb-sm" clearable/>              
            </div>
            <div class="col-12 q-px-xs">
              <q-select :disable="value.AuthenticationOptions.AllowAnonymous" outlined bottom-slots dense multiple v-model="value.AuthenticationOptions.AllowedScopes" :label="$t('allowed_scopes')" use-input use-chips
                hide-dropdown-icon
                input-debounce="0"
                new-value-mode="add"
                class="q-mb-sm"
              >
                <template v-slot:hint>{{$t('enter_add_scope')}}</template>
              </q-select>
            </div>
          </div>
          <q-separator class="q-mb-sm q-mt-sm q-mx-sm"/>
          <!-- Claim Requirement -->
          <!-- Add Add Claims To Request -->
          <key-value v-model="value.RouteClaimsRequirement" title="route_claims_requirement" help="route_claims"></key-value>
          
          <q-separator  class="q-mb-sm q-mt-sm q-mx-sm"/>
          <!-- Swagger -->
          <div class="row">
            <div class="text-subtitle1 col-12 q-px-xs">{{$t('swagger')}}</div>
            <div class="col-12 q-px-xs">
              <q-select clearable outlined dense v-model="value.SwaggerKey" :options="swagger"/>
            </div>
          </div>
          <q-separator  class="q-mb-sm q-mt-sm q-mx-sm"/>
          <!-- Claims Transformation -->
          <div class="text-subtitle1 col-12 q-px-xs q-mb-sm">
            {{$t('claims_transformation')}}
            <q-btn flat round color="warning" size="sm" icon="help_outline" class="q-ml-sm float-right" @click="$store.showHelp($t('claims_transformation'), 'claim_transformation')">
              <q-tooltip>{{$t('click_see_help')}}</q-tooltip>
            </q-btn>
          </div>
          <!-- Add Add Claims To Request -->
          <key-value v-model="value.AddClaimsToRequest" title="add_claims_to_request"></key-value>
          <!-- Add Headers To Request -->
          <key-value v-model="value.AddHeadersToRequest" title="add_headers_to_request"></key-value>
          <!-- Add Queries To Request -->
          <key-value v-model="value.AddQueriesToRequest" title="add_queries_to_request"></key-value>
          <!-- Add Queries To Request -->
          <key-value v-model="value.ChangeDownstreamPathTemplate" title="add_change_downstream_path_template"></key-value>
          <q-separator  class="q-mb-sm q-mt-sm q-mx-sm"/>

          <!-- Header Transformation -->
          <div class="text-subtitle1 col-12 q-px-xs q-mb-sm">
            {{$t('headers_transformation')}}
            <q-btn flat round color="warning" size="sm" icon="help_outline" class="q-ml-sm float-right" @click="$store.showHelp($t('headers_transformation'), 'header_transformation')">
              <q-tooltip>{{$t('click_see_help')}}</q-tooltip>
            </q-btn>
          </div>
          <!-- Upstream Header Transform -->
          <key-value v-model="value.UpstreamHeaderTransform" title="upstream_header_transform"></key-value>
          <!-- Downstream Header Transform -->
          <key-value v-model="value.DownstreamHeaderTransform" title="downstream_header_transform"></key-value>
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
              <q-select map-options emit-value outlined dense :label="$t('type')" v-model="value.LoadBalancerOptions.Type" :options="loadBalancerOptions"/>
            </div>
            <div class="col-12 col-sm-6 q-px-xs q-mb-md" v-if="value.LoadBalancerOptions.Type == 'CookieStickySessions'">
              <q-input outlined dense :label="$t('key')" v-model="value.LoadBalancerOptions.Key" :rules="[v=> (value.LoadBalancerOptions.Type == 'CookieStickySessions' && v && v.length) || $t('required_field')]"/>
            </div>
            <div class="col-12 col-sm-6 q-px-xs q-mb-md" v-if="value.LoadBalancerOptions.Type == 'CookieStickySessions'">
              <q-input type="number" outlined dense :label="$t('expiry')" v-model="value.LoadBalancerOptions.Expiry" />
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
              <q-checkbox v-model="value.RateLimitOptions.EnableHeaders" :label="$t('enable_rate_limit_headers')" />
            </div>
            <div v-if="enableRateLimiting" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <div class="row">
                <div class="col-8">
                  <q-input type="number" dense v-model="rateLimitPeriodValue" :label="$t('period')" class="q-pr-none" style="border-radius: 4px 0px 0px 4px;" @update:model-value="value.RateLimitOptions.Period = rateLimitPeriodValue+rateLimitPeriodTime"/>
                </div>
                <div class="col-4">
                  <q-select dense v-model="rateLimitPeriodTime" :options="optionTimes" style="border-radius: 0px 4px 4px 0px;" map-options emit-value @update:model-value="value.RateLimitOptions.Period = rateLimitPeriodValue+rateLimitPeriodTime"/>
                </div>
              </div>
            </div>
            <div v-if="enableRateLimiting" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <div class="row">
                <div class="col-8">
                  <q-input type="number" dense v-model="rateLimitWaitValue" :label="$t('period_timespan')" class="q-pr-none" style="border-radius: 4px 0px 0px 4px;" @update:model-value="value.RateLimitOptions.Wait = rateLimitWaitValue+rateLimitWaitTime"/>
                </div>
                <div class="col-4">
                  <q-select dense v-model="rateLimitWaitTime" :options="optionTimes" style="border-radius: 0px 4px 4px 0px;" map-options emit-value @update:model-value="value.RateLimitOptions.Wait = rateLimitWaitValue+rateLimitWaitTime"/>
                </div>
              </div>
            </div>
            <div v-if="enableRateLimiting" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <q-input type="number" outlined dense v-model.number="value.RateLimitOptions.Limit" :label="$t('limit')"/>
            </div>
            <div v-if="enableRateLimiting" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <q-input outlined dense v-model.number="value.RateLimitOptions.ClientIdHeader" label="Client Id Header"/>
            </div>
            <div v-if="enableRateLimiting" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <q-input type="number" outlined dense v-model.number="value.RateLimitOptions.StatusCode" :label="$t('rate_limit_status_code')"/>
            </div>
            <div v-if="enableRateLimiting" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <q-input outlined dense v-model.number="value.RateLimitOptions.QuotaMessage" :label="$t('rate_limit_quota_message')"/>
            </div>
            <div v-if="enableRateLimiting" class="col-12 q-px-xs q-mb-sm">
              <q-select outlined bottom-slots dense multiple v-model="value.RateLimitOptions.ClientWhitelist" :label="$t('client_whitelist')" use-input use-chips
                hide-dropdown-icon
                input-debounce="0"
                new-value-mode="add"
              >
                <template v-slot:hint>{{$t('enter_add_scope')}}</template>
              </q-select>
            </div>
          </div>
          <q-separator  class="q-mb-sm q-mt-sm q-mx-sm"/>
          <!-- Cache Options -->
          <div class="row">
            <div class="text-subtitle1 col-12 q-px-xs">
              {{$t('cache_options')}}
              <q-btn flat round color="warning" size="sm" icon="help_outline" class="q-ml-sm float-right" @click="$store.showHelp($t('cache_options'), 'cache_options')">
                <q-tooltip>{{$t('click_see_help')}}</q-tooltip>
              </q-btn>
            </div>
            <div class="col-12 col-sm-6 q-mb-sm">
              <q-checkbox v-model="enableCache" :label="$t('enable_cache_options')" @update:model-value="changeEnableCache"/>
            </div>
            <div v-if="enableCache" class="col-12 col-sm-6 q-mb-sm">
              <q-checkbox  v-model="value.CacheOptions.EnableContentHashing" :label="$t('enable_content_hashing')"/>
            </div>
            <div v-if="enableCache" class="col-12 col-sm-4 q-px-xs q-mb-sm">
              <q-input outlined dense v-model="value.CacheOptions.TtlSeconds" :label="$t('ttl_econds')"/>
            </div>
            <div v-if="enableCache" class="col-12 col-sm-4 q-px-xs q-mb-sm">
              <q-input outlined dense v-model="value.CacheOptions.Region" :label="$t('region')"/>
            </div>
            <div v-if="enableCache" class="col-12 col-sm-4 q-px-xs q-mb-sm">
              <q-input outlined dense v-model="value.CacheOptions.Header" :label="$t('header')"/>
            </div>
          </div>
          <q-separator  class="q-mb-sm q-mt-sm q-mx-sm"/>
          <!-- Security Options -->
          <div class="row">
            <div class="text-subtitle1 col-12 q-px-xs">
              {{$t('security_options')}}
              <q-btn flat round color="warning" size="sm" icon="help_outline" class="q-ml-sm float-right" @click="$store.showHelp($t('security_options'), 'security_options')">
                <q-tooltip>{{$t('click_see_help')}}</q-tooltip>
              </q-btn>
            </div>
            <div class="col-12 q-px-xs q-mb-sm">
              <q-select outlined bottom-slots dense multiple v-model="value.SecurityOptions.IPBlockedList" :label="$t('ip_blocked_list')" use-input use-chips
                hide-dropdown-icon
                input-debounce="0"
                new-value-mode="add">
                <template v-slot:hint>{{$t('enter_add_scope')}}</template>
              </q-select>
            </div>
            <div class="col-12 q-px-xs q-mb-sm">
              <q-select outlined bottom-slots dense multiple v-model="value.SecurityOptions.IPAllowedList" :label="$t('ip_allowed_list')" use-input use-chips
                hide-dropdown-icon
                input-debounce="0"
                new-value-mode="add">
                <template v-slot:hint>{{$t('enter_add_scope')}}</template>
              </q-select>
            </div>
            <div class="col-12 col-sm-6 q-mb-sm">
              <q-checkbox v-model="value.SecurityOptions.ExcludeAllowedFromBlocked" :label="$t('exclude_allowed_from_blocked')" @update:model-value="changeEnableCache"/>
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
              <q-input type="number" outlined dense v-model.number="value.QoSOptions.MinimumThroughput" :label="$t('exceptions_allowed_before_breaking')"/>
            </div>
            <div  v-if="enableQoS" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <q-input type="number" mask="" outlined dense v-model.number="value.QoSOptions.BreakDuration" :label="$t('duration_of_break')"/>
            </div>
            <div v-if="enableQoS" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <q-input type="number" outlined dense v-model.number="value.QoSOptions.Timeout" :label="$t('timeout_value')"/>
            </div>
            <div v-if="enableQoS" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <q-input type="number" outlined dense v-model.number="value.QoSOptions.FailureRatio" :label="$t('failure_ratio')"/>
            </div>
            <div v-if="enableQoS" class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <q-input type="number" outlined dense v-model.number="value.QoSOptions.SamplingDuration" :label="$t('sampling_duration')"/>
            </div>
          </div>
          <q-separator  class="q-mb-sm q-mt-sm q-mx-sm"/>
          <!-- Delegating Handlers -->
          <div class="row">
            <div class="text-subtitle1 col-12 q-px-xs">
              {{$t('delegating_handlers')}}
              <q-btn flat round color="warning" size="sm" icon="help_outline" class="q-ml-sm float-right" @click="$store.showHelp($t('delegating_handlers'), 'delegating_handlers')">
                <q-tooltip>{{$t('click_see_help')}}</q-tooltip>
              </q-btn>
            </div>
            <div class="col-12 q-px-xs q-mb-sm">
              <q-select outlined dense multiple v-model="value.DelegatingHandlers" map-options emit-value :options="settings.delegatingHandlers" clearable/>
            </div>
          </div>
          <q-separator  class="q-mb-sm q-mt-sm q-mx-sm"/>
          <!-- Tracing -->
          <div class="row">
            <div class="text-subtitle1 col-12 q-px-xs">
              {{$t('tracing')}}
              <q-btn flat round color="warning" size="sm" icon="help_outline" class="q-ml-sm float-right" @click="$store.showHelp($t('tracing'), 'tracing')">
                <q-tooltip>{{$t('click_see_help')}}</q-tooltip>
              </q-btn>
            </div>
            <div class="col-12 q-px-xs q-mb-sm">
              <q-checkbox v-model="enableTracing" :label="$t('enable_tracing')" @update:model-value="changeEnableTracing"/>
              <p v-show="enableTracing" class="text-caption text-warning">{{ $t('tracing_description') }}</p>
            </div>
          </div>
          <q-separator  class="q-mb-sm q-mt-sm q-mx-sm"/>
          <!-- Metadata -->
          <key-value v-model="value.Metadata" title="metadata" help="metadata"></key-value>
          <q-separator  class="q-mb-sm q-mt-sm q-mx-sm"/>
          <!-- Accept Invalid Certificates -->
          <div class="row">
            <div class="text-subtitle1 col-12 q-px-xs">{{$t('accept_invalid_certificates')}}</div>
            <div class="col-12 col-sm-6 q-px-xs q-mb-sm">
              <q-checkbox v-model="value.DangerousAcceptAnyServerCertificateValidator" :label="$t('enable_accept_invalid_certificates')"/>
            </div>
          </div>
          <q-separator  class="q-mb-sm q-mt-sm q-mx-sm"/>
          <!-- Timeout -->
          <div class="row">
            <div class="text-subtitle1 col-12 q-px-xs">
              {{$t('timeout')}}
              <q-btn flat round color="warning" size="sm" icon="help_outline" class="q-ml-sm float-right" @click="$store.showHelp($t('timeout'), 'timeout')">
                <q-tooltip>{{$t('click_see_help')}}</q-tooltip>
              </q-btn>
            </div>
            <div class="col-12 q-px-xs q-mb-sm">
              <q-input type="number" outlined dense v-model.number="value.Timeout"/>
            </div>
          </div>
           
        </q-form>
      </q-card-section>
      <q-separator />
      <q-card-actions align="right" class="q-py-xs">
        <q-btn flat :label="$t('cancel')" color="secondary" v-close-popup />
        <q-btn flat :label="$t(formMode)" color="primary" @click="save"/>
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script>
import { MainStore } from '../stores/main'
import  KeyValue  from '../components/KeyValue.vue'
const mainStore = MainStore()

export default {
  name: 'ComponentRouteForm',
  components: { KeyValue },
  props: ['data', 'show', 'mode', 'existEnpoint', 'existKey'],
  emits: ['update:show', , 'update:form'],
  watch: {
    data(newVal){
      this.value =  JSON.parse(JSON.stringify(newVal));
      if(this.formMode == 'add')
        this.value.id = null
      if(!this.value.LoadBalancerOptions)
        this.value.LoadBalancerOptions = {
          Type: 'NoLoadBalancer',
          Key: null,
          Expiry: 3600000
        }
      if(!this.value.DownstreamHostAndPorts){
        this.value.DownstreamHostAndPorts = [{
          Host: '',
          Port: ''
        }]
      }
      if(!this.Priority)
        this.value.Priority = 0

      if(!this.value.AuthenticationOptions){
        this.value.AuthenticationOptions = {
          AuthenticationProviderKeys: [],
          AllowedScopes: [],
          AllowAnonymous: false
        }
      }

      if(!this.value.DelegatingHandlers){
        this.value.DelegatingHandlers = []
      }

      if(!this.value.SecurityOptions){
        this.value.SecurityOptions = {
          IPBlockedList: [],
          IPAllowedList: [],
          ExcludeAllowedFromBlocked: false
        }
      }
      if(!this.value.DangerousAcceptAnyServerCertificateValidator){
        this.value.DangerousAcceptAnyServerCertificateValidator = false
      }
      this.enableCache = this.value.CacheOptions && Object.keys(this.value.CacheOptions).length > 0 ? true : false
      this.enableQoS = this.value.QoSOptions && Object.keys(this.value.QoSOptions).length > 0 ? true : false
      this.enableRateLimiting = this.value.RateLimitOptions && Object.keys(this.value.RateLimitOptions).length > 0 ? true : false
      this.enableTracing = this.value.HttpHandlerOptions && Object.keys(this.value.HttpHandlerOptions).length > 0 &&  this.value.HttpHandlerOptions.UseTracing ? true : false
      if(this.enableRateLimiting){
        this.rateLimitPeriodValue = this.value.RateLimitOptions.Period.replace(/\D/g, "");
        this.rateLimitPeriodTime = this.value.RateLimitOptions.Period.replace(/\d/g, "");
        this.rateLimitWaitValue = this.value.RateLimitOptions.Wait.replace(/\D/g, "");
        this.rateLimitWaitTime = this.value.RateLimitOptions.Wait.replace(/\d/g, "");
      } else {
        this.rateLimitPeriodValue = 1
        this.rateLimitPeriodTime = 's'
        this.rateLimitWaitValue = 1
        this.rateLimitWaitTime = 's'
      }
    }
  },
  computed: {
    showDialog: {
      get() {return this.show},
      set(value) {this.$emit('update:show', value)}
    },
    formMode(){return this.mode},
    swagger(){return mainStore.getSwagger.map(x=> x.Key)},
    settings(){return mainStore.settings},
  },
  data(){
    return {
      value: {},
      httpMethods: ['GET', 'POST', 'PUT', 'DELETE'],
      shemes: ['http', 'https', 'ws', 'wss'], 
      loadBalancerOptions: [
        { label:'No Load Balancer', value:'NoLoadBalancer' },
        { label:'Least Connection', value:'LeastConnection' },
        { label:'Round Robin', value:'RoundRobin' },
        { label:'Cookie Sessions', value:'CookieStickySessions' },
      ],
      validPath: /^(\/[a-zA-Z0-9\-._~!$&'()*+,;=:@\{\}\^\?]+)*\/?$/,
      validHost: /(([a-zA-Z|\d*|\-|\_])+(\.[a-zA-Z]{2,})*)|((\d{1,3}\.){3}\d{1,3})/,
      enableCache: false,
      enableQoS: false,
      enableTracing: false,
      enableRateLimiting: false,      
      optionTimes: [{ label: mainStore.translate('millisecond', 2), value: 'ms'},{ label: mainStore.translate('second', 2), value: 's'},{ label: mainStore.translate('minute', 2), value: 'm'}, { label: mainStore.translate('hour', 2), value: 'h'}, , { label: mainStore.translate('day', 2), value: 'd'}],
      rateLimitPeriodValue: '1',
      rateLimitPeriodTime: 's',
      rateLimitWaitValue: '1',
      rateLimitWaitTime: 's',
    }
  },
  methods: {
    save(){
      this.$refs.form.validate().then(success=>{
        if(success)
          this.$emit('update:form', this.value)
      })
    },
    addHostPort(){
      this.value.DownstreamHostAndPorts.push({
        Host: '',
        Port: 0
      })
    },
    removeHostPort(index){
      this.value.DownstreamHostAndPorts = this.value.DownstreamHostAndPorts.filter((x,idx)=> idx != index)
    },
    changeEnableCache(){
      if(this.enableCache){
        this.value.CacheOptions = { TtlSeconds: 15, Region: 'europe-central', Header: '', EnableContentHashing: false}
      } else this.value.CacheOptions = undefined
    },
    changeEnableQoS(){
      if(this.enableQoS){
        this.value.QoSOptions = { MinimumThroughput: 0, BreakDuration: 1000, Timeout: 5000}
      } else {
        this.value.QoSOptions = undefined
      }
    },
    changeEnableTracing(){
      if(this.enableTracing){
        this.value.HttpHandlerOptions = { UseTracing: true}
      } else this.value.HttpHandlerOptions = undefined
    },
    changeEnableRateLimit(){
      if(this.enableRateLimiting){
        this.value.RateLimitOptions = {
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
      } else this.value.RateLimitOptions = undefined
    }
  }
}
</script>
