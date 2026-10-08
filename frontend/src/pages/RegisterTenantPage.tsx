import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate } from 'react-router-dom'
import { z } from 'zod'
import { apiRequest, getErrorMessage } from '../api/client'
import { Button } from '../components/Button'
import { ErrorBanner } from '../components/Feedback'
import { TextField } from '../components/FormField'

const schema = z.object({
  companyName: z.string().min(1, 'Kötelező mező.'),
  taxId: z.string().min(1, 'Kötelező mező.'),
  address: z.string().min(1, 'Kötelező mező.'),
  ownerFullName: z.string().min(1, 'Kötelező mező.'),
  ownerEmail: z.string().email('Érvénytelen e-mail cím.'),
  ownerPassword: z.string().min(10, 'Legalább 10 karakter hosszú jelszó szükséges.'),
})
type FormValues = z.infer<typeof schema>

export function RegisterTenantPage() {
  const navigate = useNavigate()
  const [serverError, setServerError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  const onSubmit = async (data: FormValues) => {
    setServerError(null)
    try {
      await apiRequest<{ tenantId: string; userId: string }>('/api/auth/register-tenant', { method: 'POST', body: data })
      navigate('/login', { replace: true })
    } catch (err) {
      setServerError(getErrorMessage(err))
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-50 px-4 py-8">
      <div className="w-full max-w-md rounded-lg border border-slate-200 bg-white p-6 shadow-sm">
        <h1 className="mb-6 text-xl font-semibold text-slate-900">Cég regisztrációja</h1>

        {serverError && (
          <div className="mb-4">
            <ErrorBanner message={serverError} />
          </div>
        )}

        <form className="space-y-4" onSubmit={handleSubmit(onSubmit)}>
          <TextField label="Cégnév" error={errors.companyName?.message} {...register('companyName')} />
          <TextField label="Adószám" placeholder="12345678-1-42" error={errors.taxId?.message} {...register('taxId')} />
          <TextField label="Székhely címe" error={errors.address?.message} {...register('address')} />
          <hr className="border-slate-200" />
          <TextField label="Tulajdonos neve" error={errors.ownerFullName?.message} {...register('ownerFullName')} />
          <TextField label="Tulajdonos e-mail címe" type="email" error={errors.ownerEmail?.message} {...register('ownerEmail')} />
          <TextField label="Jelszó" type="password" error={errors.ownerPassword?.message} {...register('ownerPassword')} />
          <Button type="submit" className="w-full" disabled={isSubmitting}>
            Regisztráció
          </Button>
        </form>

        <p className="mt-4 text-center text-sm text-slate-600">
          Már van fiókod?{' '}
          <Link to="/login" className="font-medium text-slate-900 underline">
            Bejelentkezés
          </Link>
        </p>
      </div>
    </div>
  )
}
