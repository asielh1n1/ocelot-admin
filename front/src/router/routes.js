const routes = [
  {
    path: '/',
    component: () => import('layouts/MainLayout.vue'),
    meta: { requiresAuth: true },
    children: [
      { path: '', component: () => import('pages/IndexPage.vue') },
      { path: 'global-config', component: () => import('pages/GlobalConfigPage.vue') },
      { path: 'endpoint', component: () => import('pages/EndpointsPage.vue') },
      { path: 'swagger', component: () => import('pages/SwaggerPage.vue') },
      { path: 'aggregation', component: () => import('pages/AggregationPage.vue') }
    ]
  },
  {
    path: '/network-error',
    component: () => import('pages/NetworkError.vue'),
    meta: { requiresAuth: false }
  },
  {
    path: '/login',
    component: () => import('pages/LoginPage.vue'),
    meta: { requiresAuth: false }
  },
  {
    path: '/register',
    component: () => import('pages/RegisterPage.vue'),
    meta: { requiresAuth: false }
  },
  {
    path: '/:catchAll(.*)*',
    component: () => import('pages/ErrorNotFound.vue'),
    meta: { requiresAuth: false }
  }
]

export default routes
