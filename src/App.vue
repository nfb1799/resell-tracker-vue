<script setup>
import { isConfigured } from './firebase/config'
import { useAuth } from './composables/useAuth'
import ItemsProvider from './providers/ItemsProvider.vue'
import AuthScreen from './components/AuthScreen.vue'
import SetupScreen from './components/SetupScreen.vue'
import AppShell from './components/AppShell.vue'

document.documentElement.setAttribute('data-theme', localStorage.getItem('theme') || 'dark')

const { currentUser } = useAuth()
</script>

<template>
  <SetupScreen v-if="!isConfigured" />
  <AuthScreen v-else-if="!currentUser" />
  <ItemsProvider v-else>
    <AppShell />
  </ItemsProvider>
</template>
