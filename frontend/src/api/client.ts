import { clearStoredTokens, getStoredTokens, setStoredTokens } from '../auth/tokenStorage'

export class ApiError extends Error {
  status: number
  details?: unknown

  constructor(status: number, message: string, details?: unknown) {
    super(message)
    this.status = status
    this.details = details
  }
}

let refreshPromise: Promise<boolean> | null = null

async function refreshAccessToken(): Promise<boolean> {
  const tokens = getStoredTokens()
  if (!tokens) return false

  const response = await fetch('/api/auth/refresh', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken: tokens.refreshToken }),
  })

  if (!response.ok) {
    clearStoredTokens()
    return false
  }

  const body = (await response.json()) as { accessToken: string; refreshToken: string }
  setStoredTokens(body)
  return true
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  /** For non-JSON responses (e.g. the PDF download) — returns the raw Response instead of parsing JSON. */
  raw?: boolean
}

async function doFetch(path: string, options: RequestOptions): Promise<Response> {
  const tokens = getStoredTokens()
  const headers: Record<string, string> = {}
  if (options.body !== undefined) headers['Content-Type'] = 'application/json'
  if (tokens) headers['Authorization'] = `Bearer ${tokens.accessToken}`

  return fetch(path, {
    method: options.method ?? 'GET',
    headers,
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
  })
}

/** Typed fetch wrapper: attaches the bearer token, retries once via refresh-token on a 401, and throws ApiError on any other non-2xx response. */
export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  let response = await doFetch(path, options)

  if (response.status === 401 && getStoredTokens()) {
    refreshPromise ??= refreshAccessToken().finally(() => {
      refreshPromise = null
    })
    const refreshed = await refreshPromise
    if (refreshed) {
      response = await doFetch(path, options)
    }
  }

  if (!response.ok) {
    let details: unknown
    try {
      details = await response.json()
    } catch {
      // Non-JSON error body (e.g. a plain 401) — nothing more to extract.
    }
    const title = (details as { title?: string } | undefined)?.title ?? response.statusText
    throw new ApiError(response.status, title, details)
  }

  if (options.raw) {
    return response as unknown as T
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export const api = {
  get: <T>(path: string) => apiRequest<T>(path),
  post: <T>(path: string, body?: unknown) => apiRequest<T>(path, { method: 'POST', body }),
  put: <T>(path: string, body?: unknown) => apiRequest<T>(path, { method: 'PUT', body }),
}

/**
 * A single place every page's onError handler should call, instead of each rolling its own
 * `err instanceof ApiError ? err.message : 'generic string'` — that pattern was silently
 * swallowing the real cause of non-ApiError failures (a network error, a bug elsewhere in the
 * render path) behind a useless "ismeretlen hiba történt". This always logs the raw error to the
 * console too, so the browser console is the first place to look when the on-screen message
 * still isn't specific enough.
 */
export function getErrorMessage(err: unknown): string {
  console.error(err)
  if (err instanceof ApiError) return err.message
  if (err instanceof TypeError) return `Hálózati hiba történt (a szerver nem érhető el?): ${err.message}`
  if (err instanceof Error) return `Váratlan hiba történt: ${err.message}`
  return 'Ismeretlen hiba történt.'
}
