import { createApp, h } from 'vue'
import './index.css'
import App from './App.vue'
import ErrorBoundary from './components/ErrorBoundary.vue'
import ToastProvider from './providers/ToastProvider.vue'
import AuthProvider from './providers/AuthProvider.vue'

createApp({
  render: () => h(ErrorBoundary, () => h(ToastProvider, () => h(AuthProvider, () => h(App)))),
}).mount('#root')
