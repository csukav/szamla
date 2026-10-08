import { api, apiRequest } from './client'
import type {
  CreateDraftInvoiceRequest,
  CreateInvoiceSeriesRequest,
  CreateStornoInvoiceRequest,
  FinalizeInvoiceRequest,
  InvoiceDto,
  InvoiceLineRequest,
  InvoiceSeriesDto,
  InvoiceStatus,
  InvoiceSummaryDto,
} from './types'

export const invoicesApi = {
  list: (status?: InvoiceStatus) => api.get<InvoiceSummaryDto[]>(`/api/invoices${status ? `?status=${status}` : ''}`),
  get: (id: string) => api.get<InvoiceDto>(`/api/invoices/${id}`),
  createDraft: (request: CreateDraftInvoiceRequest) => api.post<string>('/api/invoices', request),
  replaceLines: (id: string, lines: InvoiceLineRequest[]) => api.put<void>(`/api/invoices/${id}/lines`, { lines }),
  finalize: (id: string, request: FinalizeInvoiceRequest) => api.post<string>(`/api/invoices/${id}/finalize`, request),
  storno: (id: string, request: CreateStornoInvoiceRequest) => api.post<string>(`/api/invoices/${id}/storno`, request),

  /** Downloads the PDF and returns an object URL the caller must revoke (URL.revokeObjectURL) once done with it. */
  pdfObjectUrl: async (id: string, copy: boolean): Promise<string> => {
    const response = await apiRequest<Response>(`/api/invoices/${id}/pdf${copy ? '?copy=true' : ''}`, { raw: true })
    const blob = await response.blob()
    return URL.createObjectURL(blob)
  },
}

export const invoiceSeriesApi = {
  list: () => api.get<InvoiceSeriesDto[]>('/api/invoice-series'),
  create: (request: CreateInvoiceSeriesRequest) => api.post<string>('/api/invoice-series', request),
}
