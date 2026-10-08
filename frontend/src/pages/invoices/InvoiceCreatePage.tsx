import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useFieldArray, useForm } from 'react-hook-form'
import { useNavigate } from 'react-router-dom'
import { z } from 'zod'
import { getErrorMessage } from '../../api/client'
import { invoicesApi } from '../../api/invoices'
import { partnersApi } from '../../api/partners'
import { PaymentMethodValues, VatExemptionReasonLabels, type PaymentMethod, type VatExemptionReason } from '../../api/types'
import { Button } from '../../components/Button'
import { ErrorBanner, Spinner } from '../../components/Feedback'
import { SelectField, TextField } from '../../components/FormField'

const paymentMethodLabels: Record<PaymentMethod, string> = { BankTransfer: 'Átutalás', Cash: 'Készpénz', Card: 'Bankkártya' }
const vatPercentageOptions = [0.27, 0.18, 0.05, 0]
const exemptionReasons = Object.keys(VatExemptionReasonLabels) as VatExemptionReason[]

const lineSchema = z.object({
  description: z.string().min(1, 'Kötelező mező.'),
  quantity: z.coerce.number().positive('A mennyiségnek pozitívnak kell lennie.'),
  unit: z.string().min(1, 'Kötelező mező.'),
  netUnitPriceAmount: z.coerce.number(),
  vatMode: z.enum(['percentage', 'exempt']),
  vatPercentage: z.coerce.number().optional(),
  vatExemptionReason: z.enum(exemptionReasons as [VatExemptionReason, ...VatExemptionReason[]]).optional(),
})

const schema = z.object({
  partnerId: z.string().min(1, 'Válassz partnert.'),
  issueDate: z.string().min(1, 'Kötelező mező.'),
  performanceDate: z.string().min(1, 'Kötelező mező.'),
  paymentDueDate: z.string().min(1, 'Kötelező mező.'),
  paymentMethod: z.enum(PaymentMethodValues as [PaymentMethod, ...PaymentMethod[]]),
  currency: z.string().length(3, '3 betűs ISO pénznemkód szükséges.'),
  lines: z.array(lineSchema).min(1, 'Legalább egy tétel szükséges.'),
})
type FormValues = z.infer<typeof schema>

const emptyLine = { description: '', quantity: 1, unit: 'db', netUnitPriceAmount: 0, vatMode: 'percentage' as const, vatPercentage: 0.27 }

export function InvoiceCreatePage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [serverError, setServerError] = useState<string | null>(null)

  const { data: partners, isLoading: partnersLoading } = useQuery({ queryKey: ['partners', ''], queryFn: () => partnersApi.list() })

  const today = new Date().toISOString().slice(0, 10)
  const {
    register,
    control,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      issueDate: today,
      performanceDate: today,
      paymentDueDate: today,
      paymentMethod: 'BankTransfer',
      currency: 'HUF',
      lines: [emptyLine],
    },
  })
  const { fields, append, remove } = useFieldArray({ control, name: 'lines' })

  const createMutation = useMutation({
    mutationFn: (values: FormValues) =>
      invoicesApi.createDraft({
        partnerId: values.partnerId,
        issueDate: values.issueDate,
        performanceDate: values.performanceDate,
        paymentDueDate: values.paymentDueDate,
        paymentMethod: values.paymentMethod,
        currency: values.currency.toUpperCase(),
        lines: values.lines.map((line) => ({
          description: line.description,
          quantity: line.quantity,
          unit: line.unit,
          netUnitPriceAmount: line.netUnitPriceAmount,
          vatRateKind: line.vatMode === 'percentage' ? 'Percentage' : 'Exempt',
          vatPercentage: line.vatMode === 'percentage' ? line.vatPercentage : null,
          vatExemptionReason: line.vatMode === 'exempt' ? line.vatExemptionReason : null,
        })),
      }),
    onSuccess: async (id) => {
      await queryClient.invalidateQueries({ queryKey: ['invoices'] })
      navigate(`/invoices/${id}`, { replace: true })
    },
    onError: (err) => setServerError(getErrorMessage(err)),
  })

  if (partnersLoading) return <Spinner />

  return (
    <div className="max-w-3xl">
      <h1 className="mb-6 text-2xl font-semibold text-slate-900">Új számla (piszkozat)</h1>

      {serverError && (
        <div className="mb-4">
          <ErrorBanner message={serverError} />
        </div>
      )}

      <form className="space-y-6" onSubmit={handleSubmit((values) => createMutation.mutate(values))}>
        <div className="grid grid-cols-2 gap-4">
          <SelectField label="Vevő" error={errors.partnerId?.message} {...register('partnerId')}>
            <option value="">— válassz —</option>
            {partners?.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </SelectField>
          <SelectField label="Fizetési mód" {...register('paymentMethod')}>
            {PaymentMethodValues.map((v) => (
              <option key={v} value={v}>
                {paymentMethodLabels[v]}
              </option>
            ))}
          </SelectField>
          <TextField label="Kiállítás dátuma" type="date" error={errors.issueDate?.message} {...register('issueDate')} />
          <TextField label="Teljesítés dátuma" type="date" error={errors.performanceDate?.message} {...register('performanceDate')} />
          <TextField label="Fizetési határidő" type="date" error={errors.paymentDueDate?.message} {...register('paymentDueDate')} />
          <TextField label="Pénznem" maxLength={3} error={errors.currency?.message} {...register('currency')} />
        </div>

        <div>
          <div className="mb-2 flex items-center justify-between">
            <h2 className="text-lg font-medium text-slate-900">Tételek</h2>
            <Button type="button" variant="secondary" onClick={() => append(emptyLine)}>
              + Tétel hozzáadása
            </Button>
          </div>

          <div className="space-y-4">
            {fields.map((field, index) => (
              <LineFields key={field.id} index={index} register={register} errors={errors} onRemove={() => fields.length > 1 && remove(index)} />
            ))}
          </div>
          {errors.lines?.root?.message && <ErrorBanner message={errors.lines.root.message} />}
        </div>

        <Button type="submit" disabled={isSubmitting}>
          Piszkozat mentése
        </Button>
      </form>
    </div>
  )
}

function LineFields({
  index,
  register,
  errors,
  onRemove,
}: {
  index: number
  register: ReturnType<typeof useForm<FormValues>>['register']
  errors: ReturnType<typeof useForm<FormValues>>['formState']['errors']
  onRemove: () => void
}) {
  return (
    <div className="rounded-md border border-slate-200 p-4">
      <div className="grid grid-cols-6 gap-3">
        <div className="col-span-2">
          <TextField label="Megnevezés" error={errors.lines?.[index]?.description?.message} {...register(`lines.${index}.description`)} />
        </div>
        <TextField label="Mennyiség" type="number" step="any" error={errors.lines?.[index]?.quantity?.message} {...register(`lines.${index}.quantity`)} />
        <TextField label="Egység" error={errors.lines?.[index]?.unit?.message} {...register(`lines.${index}.unit`)} />
        <TextField
          label="Nettó egységár"
          type="number"
          step="any"
          error={errors.lines?.[index]?.netUnitPriceAmount?.message}
          {...register(`lines.${index}.netUnitPriceAmount`)}
        />
        <SelectField label="ÁFA típus" {...register(`lines.${index}.vatMode`)}>
          <option value="percentage">Százalékos</option>
          <option value="exempt">Mentesség</option>
        </SelectField>
      </div>
      <div className="mt-3 grid grid-cols-6 gap-3">
        <div className="col-span-2">
          <SelectField label="ÁFA kulcs / mentesség oka" {...register(`lines.${index}.vatPercentage`)}>
            {vatPercentageOptions.map((v) => (
              <option key={v} value={v}>
                {(v * 100).toFixed(0)}%
              </option>
            ))}
          </SelectField>
        </div>
        <div className="col-span-2">
          <SelectField label="(mentesség esetén)" {...register(`lines.${index}.vatExemptionReason`)}>
            {exemptionReasons.map((reason) => (
              <option key={reason} value={reason}>
                {VatExemptionReasonLabels[reason]}
              </option>
            ))}
          </SelectField>
        </div>
        <div className="col-span-2 flex items-end justify-end">
          <Button type="button" variant="danger" onClick={onRemove}>
            Tétel törlése
          </Button>
        </div>
      </div>
    </div>
  )
}
