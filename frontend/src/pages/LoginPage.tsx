import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { z } from 'zod'
import { apiRequest, getErrorMessage } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { Button } from '../components/Button'
import { ErrorBanner } from '../components/Feedback'
import { TextField } from '../components/FormField'

const credentialsSchema = z.object({
  email: z.string().email('Érvénytelen e-mail cím.'),
  password: z.string().min(1, 'A jelszó megadása kötelező.'),
})
type CredentialsForm = z.infer<typeof credentialsSchema>

const codeSchema = z.object({ code: z.string().length(6, 'A kód 6 számjegyből áll.') })
type CodeForm = z.infer<typeof codeSchema>

export function LoginPage() {
  const { login, completeTwoFactorLogin } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const redirectTo = (location.state as { from?: Location })?.from?.pathname ?? '/'

  const [twoFactorToken, setTwoFactorToken] = useState<string | null>(null)
  const [serverError, setServerError] = useState<string | null>(null)

  const credentialsForm = useForm<CredentialsForm>({ resolver: zodResolver(credentialsSchema) })
  const codeForm = useForm<CodeForm>({ resolver: zodResolver(codeSchema) })

  const onSubmitCredentials = async (data: CredentialsForm) => {
    setServerError(null)
    try {
      const response = await login(data.email, data.password)
      if (response.requiresTwoFactor && response.twoFactorToken) {
        setTwoFactorToken(response.twoFactorToken)
      } else {
        navigate(redirectTo, { replace: true })
      }
    } catch (err) {
      setServerError(getErrorMessage(err))
    }
  }

  const onSubmitCode = async (data: CodeForm) => {
    if (!twoFactorToken) return
    setServerError(null)
    try {
      const tokens = await apiRequest<{ accessToken: string; refreshToken: string; requiresTwoFactor: boolean }>(
        '/api/auth/2fa/verify',
        { method: 'POST', body: { twoFactorToken, code: data.code } },
      )
      completeTwoFactorLogin(tokens)
      navigate(redirectTo, { replace: true })
    } catch (err) {
      setServerError(getErrorMessage(err))
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4">
      <div className="w-full max-w-sm rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <h1 className="mb-6 text-xl font-semibold text-slate-900">Bejelentkezés</h1>

        {serverError && (
          <div className="mb-4">
            <ErrorBanner message={serverError} />
          </div>
        )}

        {twoFactorToken === null ? (
          <form className="space-y-4" onSubmit={credentialsForm.handleSubmit(onSubmitCredentials)}>
            <TextField label="E-mail cím" type="email" autoComplete="username" error={credentialsForm.formState.errors.email?.message} {...credentialsForm.register('email')} />
            <TextField
              label="Jelszó"
              type="password"
              autoComplete="current-password"
              error={credentialsForm.formState.errors.password?.message}
              {...credentialsForm.register('password')}
            />
            <Button type="submit" className="w-full" disabled={credentialsForm.formState.isSubmitting}>
              Bejelentkezés
            </Button>
          </form>
        ) : (
          <form className="space-y-4" onSubmit={codeForm.handleSubmit(onSubmitCode)}>
            <p className="text-sm text-slate-600">Add meg a hitelesítő alkalmazásban megjelenő 6 jegyű kódot.</p>
            <TextField label="Kód" inputMode="numeric" maxLength={6} error={codeForm.formState.errors.code?.message} {...codeForm.register('code')} />
            <Button type="submit" className="w-full" disabled={codeForm.formState.isSubmitting}>
              Megerősítés
            </Button>
          </form>
        )}

        <p className="mt-4 text-center text-sm text-slate-600">
          Még nincs fiókod?{' '}
          <Link to="/register-tenant" className="font-medium text-slate-900 underline">
            Regisztráció
          </Link>
        </p>
      </div>
    </div>
  )
}
