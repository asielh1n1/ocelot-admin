const help = {
  inbound: `<p class="text-subtitle1">Placeholders</p>
  <p>You can add placeholders for variables to your templates using the format of {something}. The placeholder variable must be included in both the Outbound Path and Inbound Path properties</p>
  <pre><code class="bg-grey-3 q-pa-xs rounded-borders" style="white-space: pre;display: block;overflow: auto;">
    Inbound Path: "/posts/{postId}"
    Outbound Path: "/api/posts/{postId}"
  </code></pre>
  <p>Additionally, it faced difficulties distinguishing placeholders from other elements within the slashes. For example, when the pattern /{url}-2/ was applied to /y-2/, it would produce {url} with y-2 value.
  We have introduced an improved method for placeholder evaluation, making it easier to identify placeholders in complex URLs.</p>
  <p>
    <strong>Example:</strong>
    <ul>
      <li>Path Pattern: /api/invoices_{url0}/{url1}-{url2}_abcd/{url3}?urlId={url4}</li>
      <li>Inbound URL Path: /api/invoices_super/123-456_abcd/789?urlId=987</li>
      <li>Resulting Placeholders:
        <ul>
          <li>{url0} = super</li>
          <li>{url1} = 123</li>
          <li>{url2} = 456</li>
          <li>{url3} = 789</li>
          <li>{url4} = 987</li>
        </ul>
      </li>
    </ul>
  </p>
  <p class="text-subtitle1">Catch All</p>
  <p>Routing supports a “Catch All” style, allowing users to specify routes that match all incoming traffic.</p>
  <pre><code class="bg-grey-3 q-pa-xs rounded-borders" style="white-space: pre;display: block;overflow: auto;">
    Inbound Path: "/{catchAll}"
    Outbound Path: "/{catchAll}"
  </code></pre>
  <p>The “Catch All” route has a lower priority than other routes. If the following route is included in your configuration, API Gateway will match it before the “Catch All” route.</p>
  <p class="text-subtitle1">Priority</p>
  <p>You can define the order in which your routes match the upstream URL by including a Priority property.
  Priority 0 is the lowest priority. API Gateway always assigns 0 to Catch All routes, and this value is still hardcoded. Beyond that, you are free to assign any priority you wish.</p>
  <p class="text-subtitle1">Path to Query String direction</p>
  <p>API Gateway allows you to include a query string as part of the Inbound Path, as demonstrated in the following example:</p>
  <pre><code class="bg-grey-3 q-pa-xs rounded-borders" style="white-space: pre;display: block;overflow: auto;">
    Inbound Path: "/api/units/{subscription}/{unit}/updates"
    Outbound Path: "/api/subscriptions/{subscription}/updates?unitId={unit}"
  </code></pre>
  <p>In this example, API Gateway uses the value of the {unit} placeholder from the inbound path and includes it in the outbound request as a query string parameter named unitId.</p>
  <p class="text-subtitle1">Query String to Path direction</p>
  <p>API Gateway also allows you to include query string parameters in the Inbound Path, enabling you to match specific queries to corresponding services:</p>
  <pre><code class="bg-grey-3 q-pa-xs rounded-borders" style="white-space: pre;display: block;overflow: auto;">
    Inbound Path: "/api/subscriptions/{subscriptionId}/updates?unitId={uid}"
    Outbound Path: "/api/units/{subscriptionId}/{uid}/updates"
  </code></pre>
  <p>In this example, API Gateway matches only requests with a corresponding URL path where the query string begins with unitId=something. Additional queries are permitted but must follow the matching parameter. Additionally, API Gateway replaces the {uid} parameter in the query string and incorporates it into the outbound request path.</p>
  <p class="text-subtitle1">Catch All Query String</p>
  <p>API Gateway routing also supports a “Catch All” style, allowing all query string parameters to be forwarded. The placeholder {query} is not significant, and any name can be used.</p>
  <pre><code class="bg-grey-3 q-pa-xs rounded-borders" style="white-space: pre;display: block;overflow: auto;">
    Inbound Path: "/contracts?{query}"
    Outbound Path: "/api/path/contracts?{query}"
  </code></pre>
  <p>This query string routing feature is particularly useful in scenarios where the query string needs to be routed without any transformations.<p>
  <p class="text-subtitle1">Header placeholders</p>
  <p>Let’s explore a more interesting scenario where placeholders can be effectively utilized within your Inbound Header Placeholders.
Consider the following approach using the special placeholder format {header:placeholdername}:</p>
  <pre><code class="bg-grey-3 q-pa-xs rounded-borders" style="white-space: pre;display: block;overflow: auto;">
    Header placeholders: {
      "version": "{header:versionnumber}" // 'header:' prefix vs placeholder
    }
    Outbound Path: "/{versionnumber}/api" // with placeholder
  </code></pre>
  <p>In this scenario, the entire value of the request header version is inserted into the Outbound Path. If needed, a more complex inbound header template can be specified using placeholders such as version-{header:version}_country-{header:country}.</p>
  `,
  security_options: `<p>API Gateway facilitates the management of multiple patterns for allowed and blocked IPs using the IPAddressRange package, which is licensed under the MPL-2.0 license.</p>
  <p>This feature is designed to enhance IP management, allowing for the inclusion or exclusion of a broad IP range using CIDR notation or specific IP ranges. The current managed patterns are as follows:</p>
  <ul class="simple">
    <li>Single IP: <code class="code docutils literal notranslate"><span class="pre">192.168.1.1</span></code></li>
    <li>IP Range: <code class="code docutils literal notranslate"><span class="pre">192.168.1.1-192.168.1.250</span></code></li>
    <li>IP Short Range: <code class="code docutils literal notranslate"><span class="pre">192.168.1.1-250</span></code></li>
    <li>IP Range with subnet: <code class="code docutils literal notranslate"><span class="pre">192.168.1.0/255.255.255.0</span></code></li>
    <li>CIDR: <code class="code docutils literal notranslate"><span class="pre">192.168.1.0/24</span></code></li>
    <li>CIDR for IPv6: <code class="code docutils literal notranslate"><span class="pre">fe80::/10</span></code></li>
    <li>The allowed/blocked lists are evaluated during configuration loading</li>
    <li>The <strong>ExcludeAllowedFromBlocked</strong> property is intended to provide the ability to specify a wide range of blocked IP addresses and allow a subrange of IP addresses.
    Default value: <code class="code docutils literal notranslate"><span class="pre">false</span></code></li>
    <li>The absence of a property in <strong>SecurityOptions</strong> is allowed, it takes the default value.</li>
  </ul>
  `,
  route_claims: `<p>Route claims requirement allows you to specify a set of claims that must be present in the incoming request for the route to be considered a match. This feature is particularly useful for implementing fine-grained access control based on user attributes or other claim values.</p>
  <p>When a request is received, API Gateway evaluates the claims present in the request against the claims specified in the route's claims requirement. If all required claims are present and match the specified values, the route is considered a match, and the request is processed accordingly. If any required claim is missing or does not match, the route is not considered a match, and API Gateway continues evaluating other routes.</p>
  <p>This feature allows you to enforce specific conditions on incoming requests based on their claims, enabling you to implement more complex routing logic and access control policies.</p>
  <pre><code class="bg-grey-3 q-pa-xs rounded-borders" style="white-space: pre;display: block;overflow: auto;">
    Route Claims Requirement: {
      "scope": "api.read",
      "UserType": "registered"
    }
  </code></pre>
  `,
  claim_transformation: `<p>Claims transformation allows you to modify the claims present in the incoming request before they are evaluated against the route's claims requirement or forwarded to the downstream service. This feature is particularly useful for scenarios where you need to adjust claim values, add new claims, or remove existing claims based on specific conditions.</p>
  <p>With claims transformation, you can define a set of transformation rules that specify how claims should be modified. These rules can include operations such as adding new claims, modifying existing claim values, or removing claims altogether. The transformations are applied to the incoming request's claims before any routing decisions are made or before the request is forwarded to the downstream service.</p>
  <p>This feature provides flexibility in handling claims, allowing you to adapt the claims to meet the requirements of your routing logic or to ensure that the downstream services receive the necessary claim information in the desired format.</p>
  <p class="text-subtitle1">Add Claims to Outbound Request</p>
  <p>Below is an example configuration that will transform claims to claims:</p>
  <pre><code class="bg-grey-3 q-pa-xs rounded-borders" style="white-space: pre;display: block;overflow: auto;">
    Add Claims to Outbound Request: {
      "UserType": "Claims[sub] > value[0] > |",
      "UserId": "Claims[sub] > value[1] > |"
    }
  </code></pre>
  <p>This shows a transforms where Ocelot looks at the users sub claim and transforms it into UserType and UserId claims. Assuming the sub looks like this usertypevalue|useridvalue.</p>
  <p class="text-subtitle1">Add Headers to Outbound Request</p>
  <p>Below is an example configuration that will transform claims to headers:</p>
  <pre><code class="bg-grey-3 q-pa-xs rounded-borders" style="white-space: pre;display: block;overflow: auto;">
    Add Headers to Outbound Request: {
      "CustomerId": "Claims[sub] > value[1] > |"
    }
  </code></pre>
  <p>This shows a transform where Ocelot looks at the users sub claim and transforms it into a CustomerId header. Assuming the sub looks like this usertypevalue|useridvalue.</p>
  <p class="text-subtitle1">Add Queries to Outbound Request</p>
  <p>Below is an example configuration that will transform claims to query string parameters:</p>
  <pre><code class="bg-grey-3 q-pa-xs rounded-borders" style="white-space: pre;display: block;overflow: auto;">
    Add Queries to Outbound Request: {
      "LocationId": "Claims[LocationId] > value"
    }
  </code></pre>
  <p>This shows a transform where API Gateway looks at the users LocationId claim and add it as a query string parameter to be forwarded onto the downstream service.</p>
  <p class="text-subtitle1">Add Changes to the Outbound Path</p>
  <p>Below is an example configuration that will transform claims to downstream path custom placeholders:</p>
  <pre><code class="bg-grey-3 q-pa-xs rounded-borders" style="white-space: pre;display: block;overflow: auto;">
    Add Changes to the Outbound Path: {
      "userId": "Claims[sub] > value[1] > |"
    }
    Inbound Path: "/api/users/me/{everything}"
    Outbound Path: "/api/users/{userId}/{everything}"
  </code></pre>
  <p>This shows a transform where API Gateway looks at the users userId claim and substitutes the value to the {userId} placeholder specified in the Outbound Path. Take into account that the key specified in the Changes to the Outbound Path must be the same than the placeholder specified in the Outbound Path.</p>
  `,
  header_transformation: `<p>Header transformation allows you to modify the headers present in the incoming request before they are forwarded to the outbound service. This feature is particularly useful for scenarios where you need to adjust header values, add new headers, or remove existing headers based on specific conditions.</p>
  <p>This feature provides flexibility in handling headers, allowing you to adapt the headers to meet the requirements of your routing logic or to ensure that the outbound services receive the necessary header information in the desired format.</p>
  <p class="text-subtitle1">Find and Replace</p>
  <p>In order to transform a header first we specify the header key and then the type of transform we want e.g.</p>
  <pre><code class="bg-grey-3 q-pa-xs rounded-borders" style="white-space: pre;display: block;overflow: auto;">
    "Test": "http://www.bbc.co.uk/, http://ocelot.net/"
  </code></pre>
  <p class="text-subtitle1">Outbound Header Transform</p>
  <pre><code class="bg-grey-3 q-pa-xs rounded-borders" style="white-space: pre;display: block;overflow: auto;">
    "Uncle": "Bob"
  </code></pre>
  <p>In the example above a header with the key Uncle and value Bob would be send to to the upstream service.</p>
  <p class="text-subtitle1">Response Header Transform</p>
  <pre><code class="bg-grey-3 q-pa-xs rounded-borders" style="white-space: pre;display: block;overflow: auto;">
    "Uncle": "Bob"
  </code></pre>
  <p>In the example above a header with the key Uncle and value Bob would be returned by API Gateway when requesting the specific route.</p>
  <p class="text-subtitle1">Placeholders</p>
  <p>API Gateway allows placeholders that can be used in header transformation.</p>
  <div class="q-markup-table q-table__container q-table--horizontal-separator q-table--no-wrap">
  <table class="q-table">
    <thead>
      <tr>
        <th><p><em>Placeholder</em></p></th>
        <th><p><em>Description</em></p></th>
      </tr>
    </thead>
    <tbody>
      <tr>
        <td><p><code><span >{BaseUrl}</span></code></p></td>
        <td><p>This will use Ocelot base URL e.g. <code><span >http://localhost:5000</span></code> as its value.</p></td>
      </tr>
      <tr>
        <td><p><code><span>{DownstreamBaseUrl}</span></code></p></td>
        <td><p>This will use the downstream services base URL e.g. <code><span>http://localhost:5000</span></code> as its value. This only works for <code class="docutils literal notranslate"><span class="pre">DownstreamHeaderTransform</span></code> route option at the moment.</p></td>
      </tr>
      <tr><td><p><code><span>{RemoteIpAddress}</span></code></p></td>
        <td><p>This will find the clients IP address using <code><span>HttpContext.Connection.RemoteIpAddress</span></code>, so you will get back some IP. See more in the <a class="reference external">GetRemoteIpAddress</a> method.</p></td>
      </tr>
      <tr>
        <td><p><code><span>{TraceId}</span></code></p></td>
        <td><p>This will use the <a><span class="std std-ref">Butterfly</span></a> Trace ID. This only works for <code><span>DownstreamHeaderTransform</span></code> route option at the moment.</p></td>
      </tr>
      <tr>
        <td><p><code><span>{UpstreamHost}</span></code></p></td>
        <td><p>This will look for the incoming <code><span>Host</span></code> header.</p></td>
      </tr>
    </tbody>
  </table>
  </div>
  <p class="q-mt-md">Placeholder Sample</p>
  <pre><code class="bg-grey-3 q-pa-xs rounded-borders" style="white-space: pre;display: block;overflow: auto;">
    "Location": "http://localhost:6773, {BaseUrl}"
    "X-Forwarded-For": "{RemoteIpAddress}"
  </code></pre>
  `,
  rate_limit: `<p>Rate limiting enables you to specify a limit on the number of requests that can be made during a defined time window. When the limit is reached, subsequent requests are rejected until the time window resets.</p>
  <p>This feature helps protect your outbound services from being overwhelmed by too many requests, whether from legitimate high traffic or potential denial-of-service attacks.</p>
  <div class="q-markup-table q-table__container q-table--horizontal-separator q-table--no-wrap">
    <table class="q-table">
      <thead>
        <tr>
          <th><p><em>Schema Option</em></p></th>
          <th><p><em>Description</em></p></th>
        </tr>
      </thead>
      <tbody>
        <tr>
          <td><p><code><span >Client Id Header</span></code></p></td>
          <td><p>Specifies the header used to identify clients, with “Oc-Client” set as the default.</p></td>
        </tr>
        <tr>
          <td><p><code><span >Client White List</span></code></p></td>
          <td><p>An array that contains the clients exempt from rate limiting.</p></td>
        </tr>
        <tr>
          <td><p><code><span >Enable Rate Limiting</span></code></p></td>
          <td><p>Enables or disables rate limiting</p></td>
        </tr>
        <tr>
          <td><p><code><span >Enable Headers</span></code></p></td>
          <td><p>Specifies whether the X-RateLimit-* and Retry-After headers are enabled</p></td>
        </tr>
        <tr>
          <td><p><code><span >Limit</span></code></p></td>
          <td><p>The maximum number of requests a client can make within a given time Period</p></td>
        </tr>
        <tr>
          <td><p><code><span >Period</span></code></p></td>
          <td><p>Rate limiting period (fixed window) can be expressed as milliseconds (1ms), as seconds (1s), minutes (1m), hours (1h), or days (1d). If the exact Limit of requests is reached (quota exceeded*), the request is immediately blocked, and if Wait is defined, a waiting period begins.</p></td>
        </tr>
        <tr>
          <td><p><code><span >Wait</span></code></p></td>
          <td><p>Rate limiting wait window (no servicing period) can be expressed as milliseconds (1ms), as seconds (1s), minutes (1m), hours (1h), or days (1d). This option can have shorter or longer durations compared to the fixed window duration specified as Period. The waiting interval either extends or shortens the Quota Exceeded period*, which typically ends after the fixed window elapses.</p></td>
        </tr>
        <tr>
          <td><p><code><span >Status Code</span></code></p></td>
          <td><p>The HTTP status code returned when the rate limit is exceeded.</p></td>
        </tr>
        <tr>
          <td><p><code><span >Quota Message</span></code></p></td>
          <td><p>The HTTP message returned when the rate limit is exceeded.</p></td>
        </tr>
      </tbody>
    </table>
  </div>
  `,
  cache_options: `<p>Cache options allow you to specify caching behavior for your routes, enabling you to improve performance and reduce load on your downstream services by caching responses.</p>
  <div class="q-markup-table q-table__container q-table--horizontal-separator q-table--no-wrap">
    <table class="q-table">
      <thead>
        <tr>
          <th><p><em>Option</em></p></th>
          <th><p><em>Description</em></p></th>
        </tr>
      </thead>
      <tbody>
        <tr>
          <td><p><code><span >Ttl Seconds</span></code></p></td>
          <td><p>Time-To-Live (TTL) in seconds for the cached downstream response, i.e., the absolute expiration timeout starting from when the item is added to the cache. This option is required. If undefined, it defaults to 0 (zero), which disables caching.</p></td>
        </tr>
        <tr>
          <td><p><code><span >Region</span></code></p></td>
          <td><p>Specifies the cache region.</p></td>
        </tr>
        <tr>
          <td><p><code><span >Header</span></code></p></td>
          <td><p>Specifies the header name used for native API Gateway caching control, defaulting to the special OC-Cache-Control header. If the header is present, its value is included in the cache key constructed by the service. Varying header values result in different cache keys, effectively invalidating the cache.</p></td>
        </tr>
        <tr>
          <td><p><code><span >Enable Content Hashing</span></code></p></td>
          <td><p>Toggles inclusion of request body hashing in the cache key. Disabled by default (false) due to potential performance impact. Recommended for POST/PUT routes where request body affects response.</p></td>
        </tr>
      </tbody>
    </table>
  </div>
  `,
  qos_options: `<p>Quality of Service (QoS) options allow you to specify policies for handling transient faults and ensuring the reliability of your routes. This feature is particularly useful for scenarios where you want to implement retry logic, circuit breaking, or other resilience strategies to improve the stability of your services.</p>
  <div class="q-markup-table q-table__container q-table--horizontal-separator q-table--no-wrap">
    <table class="q-table">
      <thead>
        <tr>
          <th><p><em>Option</em></p></th>
          <th><p><em>Description</em></p></th>
        </tr>
      </thead>
      <tbody>
        <tr>
          <td><p><code><span >Break Duration</span></code></p></td>
          <td><p>This is duration of break the circuit will stay open before resetting. The unit is milliseconds.</p></td>
        </tr>
        <tr>
          <td><p><code><span >Minimum Throughput</span></code></p></td>
          <td><p>This number of actions or more must pass through the circuit within the time slice for the statistics to be considered significant and for the circuit breaker to engage.</p></td>
        </tr>
        <tr>
          <td><p><code><span >Failure Ratio</span></code></p></td>
          <td><p>This is the failure-to-success ratio at which the circuit will break.</p></td>
        </tr>
        <tr>
          <td><p><code><span >Sampling Duration</span></code></p></td>
          <td><p>This is the duration of the sampling over which failure ratios are assessed. The unit is milliseconds.</p></td>
        </tr>
        <tr>
          <td><p><code><span >Timeout</span></code></p></td>
          <td><p>This is the default timeout. The unit is milliseconds.</p></td>
        </tr>
      </tbody>
    </table>
  </div>
  `,
  'delegating_handlers': `<p>Delegating handlers allow you to specify custom logic that can be executed during the request processing pipeline. This feature is particularly useful for scenarios where you need to implement custom authentication, logging, or other cross-cutting concerns that are not covered by the built-in features of API Gateway.</p>
  <p>With delegating handlers, you can define a set of handlers that will be invoked at specific points during the request processing. These handlers can be used to modify the request or response, perform additional checks, or execute any custom logic that you require.</p>
  <p>This feature provides flexibility in handling requests and responses, allowing you to extend the functionality of API Gateway to meet the specific needs of your application.</p>
  `,
  'tracing': `<p>Tracing allows you to enable distributed tracing for your routes, providing insights into the flow of requests through your system and helping you identify performance bottlenecks or issues in your downstream services.</p>
  <p>When tracing is enabled for a route, API Gateway will generate trace information for each request that matches the route. This trace information can include details such as the request path, headers, and timing information for each step of the request processing.</p>
  <p>Enabling tracing can be particularly useful for debugging and monitoring your services, as it allows you to see how requests are being processed and where any potential issues may be occurring.</p>
  <p class="text-warning">For the trace sending to work, you must create a metadata entry with the key "TraceEndpointUrl" and set its value to the URL where the trace will be sent.</p>
  `,
  metadata: `<p>Metadata allows you to include additional information with your routes that can be used for various purposes, such as providing context for logging, enabling custom behavior in your downstream services, or storing any other relevant information that you want to associate with the route.</p>
  <p>With metadata, you can define key-value pairs that will be included with the route configuration. This metadata can then be accessed by your downstream services or used in custom logic within API Gateway to influence routing decisions or other behavior.</p>
  <p>This feature provides flexibility in associating additional information with your routes, allowing you to enhance the functionality of your API Gateway configuration and provide more context for your services.</p>
   `,
   timeout: `<p>Timeout options allow you to specify the maximum amount of time that API Gateway will wait for a response from the downstream service before timing out the request. This feature is particularly useful for scenarios where you want to ensure that your services remain responsive and do not get stuck waiting indefinitely for a response.</p>
   <p>When a request is made to a route with a specified timeout, API Gateway will wait for the specified amount of time for a response from the downstream service. If a response is not received within that time frame, API Gateway will return a timeout error to the client.</p>
   <p>Enabling timeouts can help improve the reliability and responsiveness of your services by preventing requests from hanging indefinitely and allowing you to handle timeout scenarios gracefully.</p>
   `,
   load_balancer: `<p>Load balancing options allow you to specify how API Gateway should distribute incoming requests across multiple instances of your downstream services. This feature is particularly useful for scenarios where you have multiple instances of a service running and want to ensure that requests are distributed evenly to improve performance and reliability.</p>
   <div class="q-markup-table q-table__container q-table--horizontal-separator q-table--no-wrap">
    <table class="q-table">
      <thead>
        <tr>
          <th><p><em>Option</em></p></th>
          <th><p><em>Description</em></p></th>
        </tr>
      </thead>
      <tbody>
        <tr>
          <td><p><code><span >Type</span></code></p></td>
          <td><p>An in-built load balancer type selected from the list of available Balancers, or a user-defined type.</p></td>
        </tr>
        <tr>
          <td><p><code><span >Key</span></code></p></td>
          <td><p>The name of the cookie you wish to use for sticky sessions. This option is applicable only to the Cookie Sessions type.</p></td>
        </tr>
        <tr>
          <td><p><code><span >Expiry</span></code></p></td>
          <td><p>Expiration period specifies how long, in milliseconds, the session should remain sticky. This value refreshes with each request to mimic typical session behavior. Note: This option applies only to the Cookie Sessions type.</p></td>
        </tr>
      </tbody>
    </table>
  </div>
  <p class="q-pt-md">To enable load balancing, you must configure multiple entries in the Outbound Host and Ports section. If only one host and port is defined, all traffic will be directed to that single instance regardless of the selected load balancer type. Add at least two downstream hosts for the load balancer (Round Robin, Least Connection, or Cookie Sticky Sessions) to distribute requests across instances.</p>
   `,
  swagger: `<p>Swagger (OpenAPI) is a standard used to describe and document REST APIs, including available endpoints, request/response schemas, parameters, and authentication requirements.</p>
  <p>In this API Gateway, Swagger is integrated to expose a unified and clear view of your downstream services through the gateway routes, making it easier to discover and test your APIs from a single place.</p>
  <p>This integration helps keep your API documentation aligned with your gateway configuration, improving development, onboarding, and maintenance.</p>`,
  aggregation: `<p>Aggregation allows you to combine multiple downstream service responses into a single response that is returned to the client. This feature is particularly useful for scenarios where you want to provide a unified API that aggregates data from multiple services, reducing the number of requests that clients need to make and improving performance.</p>
  <p>With aggregation, you can define a set of downstream services that should be called for a particular route, and specify how the responses from those services should be combined into a single response. This can include operations such as merging JSON responses, combining data into a specific format, or performing any other necessary transformations to create the final response.</p>
  <p>This feature provides flexibility in how you design your APIs, allowing you to create more efficient and user-friendly endpoints that aggregate data from multiple sources.</p>
   `
};

export default help;
