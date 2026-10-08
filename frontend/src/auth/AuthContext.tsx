import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react'
import { apiRequest } from '../api/client'
import type { LoginResponse } from '../api/types'
import { clearStoredTokens, decodeJwtPayload, getRoles, getStoredTokens, setStoredTokens, type SzamlaJwtClaims } from './tokenStorage'

interface CurrentUser {
  userId: string
  email: string
  tenantId: string
  roles: string[]
}

interface AuthContextValue {
  user: CurrentUser | null
  isAuthenticated: boolean
  canWrite: boolean
  login: (email: string, password: string) => Promise<LoginResponse>
  completeTwoFactorLogin: (tokens: { accessToken: string; refreshToken: string }) => void
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

function userFromAccessToken(accessToken: string): CurrentUser {
  const claims = decodeJwtPayload<SzamlaJwtClaims>(accessToken)
  return { userId: claims.sub, email: claims.email, tenantId: claims.tenant_id, roles: getRoles(claims) }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CurrentUser | null>(() => {
    const tokens = getStoredTokens()
    if (!tokens) return null
    try {
      return userFromAccessToken(tokens.accessToken)
    } catch {
      clearStoredTokens()
      return null
    }
  })

  const login = useCallback(async (email: string, password: string) => {
    const response = await apiRequest<LoginResponse>('/api/auth/login', { method: 'POST', body: { email, password } })
    if (!response.requiresTwoFactor && response.accessToken && response.refreshToken) {
      setStoredTokens({ accessToken: response.accessToken, refreshToken: response.refreshToken })
      setUser(userFromAccessToken(response.accessToken))
    }
    return response
  }, [])

  const completeTwoFactorLogin = useCallback((tokens: { accessToken: string; refreshToken: string }) => {
    setStoredTokens(tokens)
    setUser(userFromAccessToken(tokens.accessToken))
  }, [])

  const logout = useCallback(async () => {
    const tokens = getStoredTokens()
    clearStoredTokens()
    setUser(null)
    if (tokens) {
      // Best-effort: revoke the refresh token server-side too, but the local logout above is
      // what actually matters for the UI — don't block on network failure here.
      await apiRequest('/api/auth/logout', { method: 'POST', body: { refreshToken: tokens.refreshToken } }).catch(() => {})
    }
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: user !== null,
      canWrite: user?.roles.some((r) => r === 'Owner' || r === 'Admin' || r === 'Invoicer') ?? false,
      login,
      completeTwoFactorLogin,
      logout,
    }),
    [user, login, completeTwoFactorLogin, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used within an AuthProvider.')
  return context
}
