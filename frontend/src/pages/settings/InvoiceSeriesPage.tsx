import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { getErrorMessage } from '../../api/client'
import { invoiceSeriesApi } from '../../api/invoices'
import { useAuth } from '../../auth/AuthContext'
import { Button } from '../../components/Button'
import { Badge, ErrorBanner, Spinner } from '../../components/Feedback'
import { TextField } from '../../components/FormField'

const schema = z.object({ prefix: z.string().min(1, 'Kötelező mező.').max(10, 'Legfeljebb 10 karakter.') })
type FormValues = z.infer<typeof schema>

export function InvoiceSeriesPage() {
  const { canWrite } = useAuth()
  const queryClient = useQueryClient()
  const [serverError, setServerError] = useState<string | null>(null)
  const { data: series, isLoading, error } = useQuery({ queryKey: ['invoice-series'], queryFn: invoiceSeriesApi.list })

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  const createMutation = useMutation({
    mutationFn: (values: FormValues) => invoiceSeriesApi.create({ prefix: values.prefix }),
    onSuccess: async () => {
      reset()
      await queryClient.invalidateQueries({ queryKey: ['invoice-series'] })
    },
    onError: (err) => setServerError(getErrorMessage(err)),
  })

  return (
    <div className="max-w-xl">
      <h1 className="mb-6 text-2xl font-semibold text-slate-900">Számlatömbök</h1>

      {isLoading && <Spinner />}
      {error && <ErrorBanner message="Nem sikerült betölteni a számlatömböket." />}

      {series && (
        <div className="mb-6 overflow-hidden rounded-lg border border-slate-200 bg-white">
          <table className="min-w-full divide-y divide-slate-200 text-sm">
            <thead className="bg-slate-50">
              <tr>
                <th className="px-4 py-2 text-left font-medium text-slate-600">Előtag</th>
                <th className="px-4 py-2 text-left font-medium text-slate-600">Állapot</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {series.length === 0 && (
                <tr>
                  <td colSpan={2} className="px-4 py-6 text-center text-slate-500">
                    Nincs még számlatömb.
                  </td>
                </tr>
              )}
              {series.map((s) => (
                <tr key={s.id}>
                  <td className="px-4 py-2 font-medium text-slate-900">{s.prefix}</td>
                  <td className="px-4 py-2">
                    <Badge tone={s.isActive ? 'success' : 'neutral'}>{s.isActive ? 'Aktív' : 'Inaktív'}</Badge>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {canWrite && (
        <>
          <h2 className="mb-2 text-lg font-medium text-slate-900">Új számlatömb</h2>
          {serverError && (
            <div className="mb-4">
              <ErrorBanner message={serverError} />
            </div>
          )}
          <form className="flex items-end gap-3" onSubmit={handleSubmit((values) => createMutation.mutate(values))}>
            <TextField label="Előtag (pl. SZ)" error={errors.prefix?.message} {...register('prefix')} />
            <Button type="submit" disabled={isSubmitting}>
              Létrehozás
            </Button>
          </form>
        </>
      )}
    </div>
  )
}
