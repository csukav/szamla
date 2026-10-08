import { api } from './client'
import type { CreatePartnerRequest, PartnerDto, UpdatePartnerRequest } from './types'

export const partnersApi = {
  list: (search?: string) => api.get<PartnerDto[]>(`/api/partners${search ? `?search=${encodeURIComponent(search)}` : ''}`),
  get: (id: string) => api.get<PartnerDto>(`/api/partners/${id}`),
  create: (request: CreatePartnerRequest) => api.post<string>('/api/partners', request),
  update: (id: string, request: UpdatePartnerRequest) => api.put<void>(`/api/partners/${id}`, request),
}
