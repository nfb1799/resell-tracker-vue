import { request } from './http'
import type {
  DashboardDto,
  DonationRequestDto,
  FeeSettingsDto,
  ItemDto,
  ItemRequestDto,
  MeDto,
  SaleRequestDto,
  SettingsDto,
  TrendsDto,
} from './types'

export { ApiError } from './http'
export type * from './types'

export const authApi = {
  me: () => request<MeDto>('/auth/me'),
  login: (email: string, password: string) => request<MeDto>('/auth/login', { method: 'POST', body: { email, password } }),
  register: (email: string, password: string, displayName: string) =>
    request<MeDto>('/auth/register', { method: 'POST', body: { email, password, displayName } }),
  demo: () => request<MeDto>('/auth/demo', { method: 'POST' }),
  logout: () => request<void>('/auth/logout', { method: 'POST' }),
  forgotPassword: (email: string) => request<void>('/auth/forgot-password', { method: 'POST', body: { email } }),
  resetPassword: (email: string, token: string, newPassword: string) =>
    request<void>('/auth/reset-password', { method: 'POST', body: { email, token, newPassword } }),
}

export const itemsApi = {
  list: () => request<ItemDto[]>('/items'),
  create: (body: ItemRequestDto) => request<ItemDto>('/items', { method: 'POST', body }),
  update: (id: string, version: string, body: ItemRequestDto) =>
    request<ItemDto>(`/items/${id}`, { method: 'PUT', body, version }),
  remove: (id: string, version: string) => request<void>(`/items/${id}`, { method: 'DELETE', version }),
  sell: (id: string, version: string, body: SaleRequestDto) =>
    request<ItemDto>(`/items/${id}/sale`, { method: 'PUT', body, version }),
  undoSale: (id: string, version: string) => request<ItemDto>(`/items/${id}/sale`, { method: 'DELETE', version }),
  donate: (id: string, version: string, body: DonationRequestDto) =>
    request<ItemDto>(`/items/${id}/donation`, { method: 'PUT', body, version }),
  undoDonation: (id: string, version: string) =>
    request<ItemDto>(`/items/${id}/donation`, { method: 'DELETE', version }),
  setPhoto: (id: string, version: string, thumbnail: Blob, full: Blob) => {
    const form = new FormData()
    form.append('thumbnail', thumbnail, 'thumbnail.jpg')
    form.append('full', full, 'full.jpg')
    return request<ItemDto>(`/items/${id}/photo`, { method: 'PUT', body: form, version })
  },
  removePhoto: (id: string, version: string) => request<ItemDto>(`/items/${id}/photo`, { method: 'DELETE', version }),
  /** For an img src; the version busts the browser cache when the photo changes. */
  photoUrl: (id: string, version: string) => `/api/items/${id}/photo?v=${encodeURIComponent(version)}`,
}

export const settingsApi = {
  get: () => request<SettingsDto>('/settings'),
  save: (body: SettingsDto) => request<SettingsDto>('/settings', { method: 'PUT', body }),
  getFees: () => request<FeeSettingsDto>('/settings/fees'),
  saveFees: (body: FeeSettingsDto) => request<FeeSettingsDto>('/settings/fees', { method: 'PUT', body }),
}

export const statsApi = {
  dashboard: (today: string) => request<DashboardDto>(`/stats/dashboard?today=${today}`),
  trends: (today: string) => request<TrendsDto>(`/stats/trends?today=${today}`),
}
