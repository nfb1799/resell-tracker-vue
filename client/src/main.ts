import './assets/base.css'
import './assets/app.css'
import './assets/desktop.css'

import { createApp } from 'vue'
import { createPinia } from 'pinia'

import App from './App.vue'
import router from './router'
import { ApiError } from './api'
import { useAuthStore } from './stores/auth'

const app = createApp(App)
const pinia = createPinia()

app.use(pinia)
app.use(router)

// A 401 from any call means the session is gone (expired, or a demo was
// deleted): drop back to sign-in rather than leaving a broken page up.
window.addEventListener('unhandledrejection', (event) => {
  if (event.reason instanceof ApiError && event.reason.status === 401 && !router.currentRoute.value.meta.public) {
    useAuthStore(pinia).forget()
    void router.replace({ name: 'sign-in' })
  }
})

app.mount('#app')
