// Injection keys for the three app-wide services. The real providers fill them
// from Firebase; demo.js fills them from memory, so every screen below runs the
// same either way.
export const ToastKey = Symbol('toast')
export const AuthKey = Symbol('auth')
export const ItemsKey = Symbol('items')
