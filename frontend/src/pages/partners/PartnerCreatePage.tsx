import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useNavigate } from 'react-router-dom'
import { z } from 'zod'
import { partnersApi } from '../../api/partners'
import { PartnerCountryCategoryValues, type PartnerCountryCategory } from '../../api/types'
import { getErrorMessage } from '../../api/client'
import { Button } from '../../components/Button'
import { ErrorBanner } from '../../components/Feedback'
import { SelectField, TextField } from '../../components/FormField'

const countryCategoryLabels: Record<PartnerCountryCategory, string> = {
  Domestic: 'Belföldi',
  EuMemberState: 'EU-s tagállam',
  ThirdCountry: 'EU-n kívüli',
}

const schema = z
  .object({
    name: z.string().min(1, 'Kötelező mező.'),
    isPrivatePerson: z.boolean(),
    countryCategory: z.enum(PartnerCountryCategoryValues as [PartnerCountryCategory, ...PartnerCountryCategory[]]),
    countryCode: z.string().length(2, 'Kétbetűs országkód szükséges (pl. HU).'),
    address: z.string().min(1, 'Kötelező mező.'),
    taxId: z.string().optional(),
    euVatId: z.string().optional(),
    email: z.string().email('Érvénytelen e-mail cím.').optional().or(z.literal('')),
    paymentTermDays: z.coerce.number().int().min(0).optional(),
  })
  .refine((data) => data.isPrivatePerson || data.countryCategory !== 'Domestic' || !!data.taxId, {
    message: 'Belföldi cég vevőnél az adószám kötelező.',
    path: ['taxId'],
  })
type FormValues = z.infer<typeof schema>

export function PartnerCreatePage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [serverError, setServerError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { isPrivatePerson: false, countryCategory: 'Domestic', countryCode: 'HU' },
  })

  const createMutation = useMutation({
    mutationFn: (values: FormValues) =>
      partnersApi.create({
        name: values.name,
        isPrivatePerson: values.isPrivatePerson,
        countryCategory: values.countryCategory,
        countryCode: values.countryCode,
        address: values.address,
        taxId: values.taxId || null,
        euVatId: values.euVatId || null,
        email: values.email || null,
        paymentTermDays: values.paymentTermDays ?? null,
      }),
    onSuccess: async (id) => {
      await queryClient.invalidateQueries({ queryKey: ['partners'] })
      navigate(`/partners/${id}`, { replace: true })
    },
    onError: (err) => setServerError(getErrorMessage(err)),
  })

  const isPrivatePerson = watch('isPrivatePerson')

  return (
    <div className="max-w-xl">
      <h1 className="mb-6 text-2xl font-semibold text-slate-900">Új partner</h1>

      {serverError && (
        <div className="mb-4">
          <ErrorBanner message={serverError} />
        </div>
      )}

      <form className="space-y-4" onSubmit={handleSubmit((values) => createMutation.mutate(values))}>
        <TextField label="Név" error={errors.name?.message} {...register('name')} />

        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input type="checkbox" className="rounded border-slate-300" {...register('isPrivatePerson')} />
          Magánszemély
        </label>

        <SelectField label="Ország kategória" error={errors.countryCategory?.message} {...register('countryCategory')}>
          {PartnerCountryCategoryValues.map((value) => (
            <option key={value} value={value}>
              {countryCategoryLabels[value]}
            </option>
          ))}
        </SelectField>

        <TextField label="Országkód (ISO, pl. HU)" maxLength={2} error={errors.countryCode?.message} {...register('countryCode')} />
        <TextField label="Cím" error={errors.address?.message} {...register('address')} />
        <TextField
          label={`Adószám${isPrivatePerson ? '' : ' (belföldi cégnél kötelező)'}`}
          placeholder="12345678-1-42"
          error={errors.taxId?.message}
          {...register('taxId')}
        />
        <TextField label="EU közösségi adószám" placeholder="HU12345678" error={errors.euVatId?.message} {...register('euVatId')} />
        <TextField label="E-mail" type="email" error={errors.email?.message} {...register('email')} />
        <TextField label="Fizetési határidő (nap)" type="number" min={0} error={errors.paymentTermDays?.message} {...register('paymentTermDays')} />

        <Button type="submit" disabled={isSubmitting}>
          Mentés
        </Button>
      </form>
    </div>
  )
}
