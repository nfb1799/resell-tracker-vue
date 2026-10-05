// Demo harness: the real UI driven by sample data held in memory, with no
// Firebase behind it. Served in dev only, at /resell-tracker-vue/demo.html —
// handy for trying the app before wiring up a project, and for working on
// screens without touching real inventory. Not part of the production build.
import { createApp, h } from 'vue'
import './index.css'
import ToastProvider from './providers/ToastProvider.vue'
import DemoHarness from './DemoHarness.vue'

document.documentElement.setAttribute('data-theme', localStorage.getItem('theme') || 'dark')

createApp({
  render: () => h(ToastProvider, () => h(DemoHarness)),
}).mount('#root')
