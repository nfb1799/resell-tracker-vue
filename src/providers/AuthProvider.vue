<script setup>
import { ref, shallowRef, provide, onUnmounted } from 'vue'
import {
  createUserWithEmailAndPassword,
  signInWithEmailAndPassword,
  signInAnonymously,
  signOut,
  onAuthStateChanged,
  updateProfile,
  sendPasswordResetEmail,
} from 'firebase/auth'
import { doc, setDoc, getDoc } from 'firebase/firestore'
import { auth, db, isConfigured } from '../firebase/config'
import { defaultFeeSettings } from '../lib/platforms'
import { AuthKey } from '../composables/keys'

function defaultSettings(displayName) {
  return {
    name: displayName,
    currency: 'USD',
    theme: 'dark',
    fees: defaultFeeSettings(),
    profitGoal: 0,
  }
}

// Both are only ever replaced wholesale, never mutated in place. Shallow refs
// keep Vue from wrapping them in deep proxies: the Firebase User is a class
// instance with internal state, and the profile's nested settings get written
// straight back to Firestore, which should be handed plain objects.
const currentUser = shallowRef(null)
const userProfile = shallowRef(null)
const loading = ref(isConfigured)

async function createProfile(user, displayName, extra = {}) {
  const profile = {
    displayName,
    email: user.email || '',
    createdAt: new Date().toISOString(),
    settings: defaultSettings(displayName),
    ...extra,
  }
  await setDoc(doc(db, 'users', user.uid), profile)
  userProfile.value = profile
  return profile
}

async function signup(email, password, displayName) {
  const credential = await createUserWithEmailAndPassword(auth, email, password)
  await updateProfile(credential.user, { displayName })
  await createProfile(credential.user, displayName)
  return credential
}

function login(email, password) {
  return signInWithEmailAndPassword(auth, email, password)
}

// Guest mode: usable immediately, upgradeable later by linking an email.
async function loginAnonymously() {
  const credential = await signInAnonymously(auth)
  await updateProfile(credential.user, { displayName: 'Guest' })

  const existing = await getDoc(doc(db, 'users', credential.user.uid))
  if (!existing.exists()) {
    await createProfile(credential.user, 'Guest', { isAnonymous: true })
  }
  return credential
}

function logout() {
  userProfile.value = null
  return signOut(auth)
}

function resetPassword(email) {
  return sendPasswordResetEmail(auth, email)
}

async function fetchUserProfile(uid) {
  const snap = await getDoc(doc(db, 'users', uid))
  if (!snap.exists()) return null
  const profile = snap.data()
  userProfile.value = profile
  return profile
}

async function updateUserProfile(updates) {
  if (!currentUser.value) return
  await setDoc(doc(db, 'users', currentUser.value.uid), updates, { merge: true })
  userProfile.value = { ...userProfile.value, ...updates }
}

if (isConfigured) {
  const unsubscribe = onAuthStateChanged(auth, async (user) => {
    currentUser.value = user
    if (user) {
      await fetchUserProfile(user.uid)
    } else {
      userProfile.value = null
    }
    loading.value = false
  })
  onUnmounted(unsubscribe)
}

provide(AuthKey, {
  currentUser,
  userProfile,
  signup,
  login,
  loginAnonymously,
  logout,
  resetPassword,
  updateUserProfile,
  fetchUserProfile,
})
</script>

<template>
  <slot v-if="!loading" />
</template>
