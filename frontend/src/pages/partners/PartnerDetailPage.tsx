import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useParams } from 'react-router-dom'
import { z } from 'zod'
import { getErrorMessage } from '../../api/client'
import { partnersApi } from '../../api/partners'
import { useAuth } from '../../auth/AuthContext'
import { Button } from '../../components/Button'
import { ErrorBanner, Spinner } from '../../components/Feedback'
import { TextField } from '../../components/FormField'

const schema = z.object({
  address: z.string().min(1, 'Kötelező mező.'),
  email: z.string().email('Érvénytelen e-mail cím.').optional().or(z.literal('')),
  paymentTermDays: z.coerce.number().int().min(0).optional(),
})
type FormValues = z.infer<typeof schema>

export function PartnerDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { canWrite } = useAuth()
  const queryClient = useQueryClient()
  const [serverError, setServerError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  const { data: partner, isLoading, error } = useQuery({
    queryKey: ['partner', id],
    queryFn: () => partnersApi.get(id!),
    enabled: !!id,
  })

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  useEffect(() => {
    if (partner) {
      reset({ address: partner.address, email: partner.email ?? '', paymentTermDays: partner.paymentTermDays ?? undefined })
    }
  }, [partner, reset])

  const updateMutation = useMutation({
    mutationFn: (values: FormValues) =>
      partnersApi.update(id!, { address: values.address, email: values.email || null, paymentTermDays: values.paymentTermDays ?? null }),
    onSuccess: async () => {
      setSaved(true)
      setServerError(null)
      await queryClient.invalidateQueries({ queryKey: ['partner', id] })
      await queryClient.invalidateQueries({ queryKey: ['partners'] })
    },
    onError: (err) => setServerError(getErrorMessage(err)),
  })

  if (isLoading) return <Spinner />
  if (error || !partner) return <ErrorBanner message="A partner nem található." />

  return (
    <div className="max-w-xl">
      <h1 className="mb-1 text-2xl font-semibold text-slate-900">{partner.name}</h1>
      <p className="mb-6 text-sm text-slate-500">
        {partner.isPrivatePerson ? 'Magánszemély' : 'Cég'} · {partner.taxId ?? partner.euVatId ?? 'nincs adószám'}
      </p>

      {serverError && (
        <div className="mb-4">
          <ErrorBanner message={serverError} />
        </div>
      )}

      <form
        className="space-y-4"
        onSubmit={handleSubmit((values) => {
          setSaved(false)
          updateMutation.mutate(values)
        })}
      >
        <TextField label="Cím" error={errors.address?.message} disabled={!canWrite} {...register('address')} />
        <TextField label="E-mail" type="email" error={errors.email?.message} disabled={!canWrite} {...register('email')} />
        <TextField
          label="Fizetési határidő (nap)"
          type="number"
          min={0}
          error={errors.paymentTermDays?.message}
          disabled={!canWrite}
          {...register('paymentTermDays')}
        />

        {canWrite && (
          <div className="flex items-center gap-3">
            <Button type="submit" disabled={isSubmitting}>
              Mentés
            </Button>
            {saved && <span className="text-sm text-green-700">Mentve.</span>}
          </div>
        )}
      </form>
    </div>
  )
}
