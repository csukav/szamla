// Tokens live in localStorage for MVP simplicity, matching how the backend already hands both
// tokens back in the JSON login response body (not as cookies). This is a known, deliberate
// trade-off, not an oversight: localStorage is readable by any script on the page, so an XSS bug
// elsewhere in the app could exfiltrate a session. Moving to httpOnly-cookie-based refresh
// tokens (which the backend doesn't support yet either) is a fair hardening item for Phase 7.

const ACCESS_TOKEN_KEY = 'szamla.accessToken'
const REFRESH_TOKEN_KEY = 'szamla.refreshToken'

export interface StoredTokens {
  accessToken: string
  refreshToken: string
}

export function getStoredTokens(): StoredTokens | null {
  const accessToken = localStorage.getItem(ACCESS_TOKEN_KEY)
  const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY)
  if (!accessToken || !refreshToken) return null
  return { accessToken, refreshToken }
}

export function setStoredTokens(tokens: StoredTokens): void {
  localStorage.setItem(ACCESS_TOKEN_KEY, tokens.accessToken)
  localStorage.setItem(REFRESH_TOKEN_KEY, tokens.refreshToken)
}

export function clearStoredTokens(): void {
  localStorage.removeItem(ACCESS_TOKEN_KEY)
  localStorage.removeItem(REFRESH_TOKEN_KEY)
}

/** Decodes the JWT payload without verifying the signature — fine for reading our own claims client-side; the server is the only party that actually trusts this token. */
export function decodeJwtPayload<T>(token: string): T {
  const base64Url = token.split('.')[1]
  const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/')
  const json = decodeURIComponent(
    atob(base64)
      .split('')
      .map((c) => '%' + c.charCodeAt(0).toString(16).padStart(2, '0'))
      .join(''),
  )
  return JSON.parse(json) as T
}

export interface SzamlaJwtClaims {
  sub: string
  email: string
  tenant_id: string
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': string | string[]
  exp: number
}

export function getRoles(claims: SzamlaJwtClaims): string[] {
  const role = claims['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
  if (!role) return []
  return Array.isArray(role) ? role : [role]
}
