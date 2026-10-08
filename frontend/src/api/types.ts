// Mirrors the DTOs in Szamla.Api.Contracts.*/Szamla.Application.*.Queries — kept in one file since
// there's no shared-types codegen yet. If the backend contracts drift from this, the type
// checker won't catch it (nothing generates this from the OpenAPI spec yet); re-sync by hand
// when a backend contract changes shape.

export type PartnerCountryCategory = 'Domestic' | 'EuMemberState' | 'ThirdCountry'
export const PartnerCountryCategoryValues: PartnerCountryCategory[] = ['Domestic', 'EuMemberState', 'ThirdCountry']

export type PaymentMethod = 'BankTransfer' | 'Cash' | 'Card'
export const PaymentMethodValues: PaymentMethod[] = ['BankTransfer', 'Cash', 'Card']

export type InvoiceType = 'Normal' | 'Storno' | 'Modification'
export type InvoiceStatus = 'Draft' | 'Finalized'

export type VatRateKind = 'Percentage' | 'Exempt'
export type VatExemptionReason =
  | 'SubjectExempt'
  | 'ObjectExempt'
  | 'IntraCommunitySupply'
  | 'IntraCommunityReverseCharge'
  | 'DomesticReverseCharge'
  | 'OutsideVatScope'

export const VatExemptionReasonLabels: Record<VatExemptionReason, string> = {
  SubjectExempt: 'AAM – alanyi adómentes',
  ObjectExempt: 'TAM – tárgyi adómentes',
  IntraCommunitySupply: 'EUE – közösségen belüli értékesítés',
  IntraCommunityReverseCharge: 'EUFAD37 – közösségi fordított adózás',
  DomesticReverseCharge: 'belföldi fordított adózás',
  OutsideVatScope: 'áfa törvény hatályán kívüli',
}

// --- Auth ---

export interface RegisterTenantRequest {
  companyName: string
  taxId: string
  address: string
  ownerFullName: string
  ownerEmail: string
  ownerPassword: string
  defaultCurrency?: string
}

export interface LoginRequest {
  email: string
  password: string
}

export interface LoginResponse {
  requiresTwoFactor: boolean
  twoFactorToken: string | null
  accessToken: string | null
  refreshToken: string | null
  accessTokenExpiresAtUtc: string | null
}

export interface VerifyTwoFactorRequest {
  twoFactorToken: string
  code: string
}

// --- Partners ---

export interface PartnerDto {
  id: string
  name: string
  isPrivatePerson: boolean
  countryCategory: PartnerCountryCategory
  countryCode: string
  address: string
  taxId: string | null
  euVatId: string | null
  email: string | null
  paymentTermDays: number | null
}

export interface CreatePartnerRequest {
  name: string
  isPrivatePerson: boolean
  countryCategory: PartnerCountryCategory
  countryCode: string
  address: string
  taxId?: string | null
  euVatId?: string | null
  email?: string | null
  paymentTermDays?: number | null
}

export interface UpdatePartnerRequest {
  address: string
  email?: string | null
  paymentTermDays?: number | null
}

// --- Invoices ---

export interface InvoiceLineRequest {
  description: string
  quantity: number
  unit: string
  netUnitPriceAmount: number
  vatRateKind: VatRateKind
  vatPercentage?: number | null
  vatExemptionReason?: VatExemptionReason | null
}

export interface CreateDraftInvoiceRequest {
  partnerId: string
  issueDate: string
  performanceDate: string
  paymentDueDate: string
  paymentMethod: PaymentMethod
  currency: string
  lines: InvoiceLineRequest[]
  exchangeRate?: number | null
  exchangeRateSource?: string | null
  exchangeRateDate?: string | null
}

export interface FinalizeInvoiceRequest {
  invoiceSeriesId: string
}

export interface CreateStornoInvoiceRequest {
  issueDate: string
}

export interface CreateInvoiceSeriesRequest {
  prefix: string
}

export interface InvoiceSeriesDto {
  id: string
  prefix: string
  isActive: boolean
}

export interface InvoiceLineDto {
  id: string
  description: string
  quantity: number
  unit: string
  netUnitPriceAmount: number
  vatRateDisplayCode: string
  netAmount: number
  vatAmount: number
  grossAmount: number
}

export interface VatSummaryRowDto {
  vatRateDisplayCode: string
  netAmount: number
  vatAmount: number
  grossAmount: number
}

export interface PartySnapshotDto {
  name: string
  taxId?: string | null
  address: string
  bankAccount?: string | null
  isVatExempt?: boolean
  isPrivatePerson?: boolean
  countryCategory?: PartnerCountryCategory
  countryCode?: string
  euVatId?: string | null
  email?: string | null
}

export interface InvoiceDto {
  id: string
  type: InvoiceType
  status: InvoiceStatus
  number: string | null
  originalInvoiceId: string | null
  issueDate: string
  performanceDate: string
  paymentDueDate: string
  paymentMethod: PaymentMethod
  currency: string
  exchangeRate: number | null
  exchangeRateSource: string | null
  exchangeRateDate: string | null
  issuer: PartySnapshotDto
  partner: PartySnapshotDto
  lines: InvoiceLineDto[]
  netTotal: number
  vatTotal: number
  grossTotal: number
  vatTotalHufAmount: number | null
  vatSummary: VatSummaryRowDto[]
}

export interface InvoiceSummaryDto {
  id: string
  type: InvoiceType
  status: InvoiceStatus
  number: string | null
  issueDate: string
  paymentDueDate: string
  partnerName: string
  grossTotal: number
  currency: string
}
