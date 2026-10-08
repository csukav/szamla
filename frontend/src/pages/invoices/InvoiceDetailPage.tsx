import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { getErrorMessage } from '../../api/client'
import { invoiceSeriesApi, invoicesApi } from '../../api/invoices'
import { useAuth } from '../../auth/AuthContext'
import { Button } from '../../components/Button'
import { Badge, ErrorBanner, Spinner } from '../../components/Feedback'

const typeLabel = { Normal: 'Számla', Storno: 'Sztornó számla', Modification: 'Módosító számla' } as const

export function InvoiceDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { canWrite } = useAuth()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [actionError, setActionError] = useState<string | null>(null)
  const [seriesId, setSeriesId] = useState('')
  const [stornoDate, setStornoDate] = useState(() => new Date().toISOString().slice(0, 10))

  const { data: invoice, isLoading, error } = useQuery({ queryKey: ['invoice', id], queryFn: () => invoicesApi.get(id!), enabled: !!id })
  const { data: series } = useQuery({ queryKey: ['invoice-series'], queryFn: invoiceSeriesApi.list, enabled: canWrite })

  const finalizeMutation = useMutation({
    mutationFn: () => invoicesApi.finalize(id!, { invoiceSeriesId: seriesId }),
    onSuccess: async () => {
      setActionError(null)
      await queryClient.invalidateQueries({ queryKey: ['invoice', id] })
      await queryClient.invalidateQueries({ queryKey: ['invoices'] })
    },
    onError: (err) => setActionError(getErrorMessage(err)),
  })

  const stornoMutation = useMutation({
    mutationFn: () => invoicesApi.storno(id!, { issueDate: stornoDate }),
    onSuccess: async (stornoId) => {
      await queryClient.invalidateQueries({ queryKey: ['invoices'] })
      navigate(`/invoices/${stornoId}`)
    },
    onError: (err) => setActionError(getErrorMessage(err)),
  })

  const downloadPdf = async (copy: boolean) => {
    if (!id) return
    setActionError(null)
    try {
      const url = await invoicesApi.pdfObjectUrl(id, copy)
      window.open(url, '_blank')
    } catch {
      setActionError('A PDF letöltése nem sikerült.')
    }
  }

  if (isLoading) return <Spinner />
  if (error || !invoice) return <ErrorBanner message="A számla nem található." />

  return (
    <div className="max-w-3xl">
      <div className="mb-6 flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-900">{invoice.number ?? 'Piszkozat'}</h1>
          <p className="text-sm text-slate-500">{typeLabel[invoice.type]}</p>
        </div>
        <Badge tone={invoice.status === 'Finalized' ? 'success' : 'warning'}>{invoice.status === 'Finalized' ? 'Véglegesítve' : 'Piszkozat'}</Badge>
      </div>

      {actionError && (
        <div className="mb-4">
          <ErrorBanner message={actionError} />
        </div>
      )}

      <div className="mb-6 grid grid-cols-2 gap-6 rounded-lg border border-slate-200 bg-white p-4">
        <div>
          <h2 className="mb-1 text-sm font-semibold text-slate-500">Kiállító</h2>
          <p className="text-sm text-slate-900">{invoice.issuer.name}</p>
          <p className="text-sm text-slate-600">{invoice.issuer.address}</p>
          <p className="text-sm text-slate-600">Adószám: {invoice.issuer.taxId}</p>
        </div>
        <div>
          <h2 className="mb-1 text-sm font-semibold text-slate-500">Vevő</h2>
          <p className="text-sm text-slate-900">{invoice.partner.name}</p>
          <p className="text-sm text-slate-600">{invoice.partner.address}</p>
          {invoice.partner.taxId && <p className="text-sm text-slate-600">Adószám: {invoice.partner.taxId}</p>}
        </div>
        <div className="col-span-2 grid grid-cols-3 gap-4 border-t border-slate-100 pt-4 text-sm">
          <div>
            <span className="text-slate-500">Kiállítás:</span> {invoice.issueDate}
          </div>
          <div>
            <span className="text-slate-500">Teljesítés:</span> {invoice.performanceDate}
          </div>
          <div>
            <span className="text-slate-500">Fizetési határidő:</span> {invoice.paymentDueDate}
          </div>
        </div>
      </div>

      <div className="mb-6 overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="min-w-full divide-y divide-slate-200 text-sm">
          <thead className="bg-slate-50">
            <tr>
              <th className="px-4 py-2 text-left font-medium text-slate-600">Megnevezés</th>
              <th className="px-4 py-2 text-right font-medium text-slate-600">Menny.</th>
              <th className="px-4 py-2 text-right font-medium text-slate-600">Egységár</th>
              <th className="px-4 py-2 text-right font-medium text-slate-600">ÁFA</th>
              <th className="px-4 py-2 text-right font-medium text-slate-600">Bruttó</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {invoice.lines.map((line) => (
              <tr key={line.id}>
                <td className="px-4 py-2 text-slate-900">{line.description}</td>
                <td className="px-4 py-2 text-right text-slate-600">
                  {line.quantity} {line.unit}
                </td>
                <td className="px-4 py-2 text-right text-slate-600">{line.netUnitPriceAmount.toLocaleString('hu-HU')}</td>
                <td className="px-4 py-2 text-right text-slate-600">{line.vatRateDisplayCode}</td>
                <td className="px-4 py-2 text-right text-slate-900">{line.grossAmount.toLocaleString('hu-HU', { minimumFractionDigits: 2 })}</td>
              </tr>
            ))}
          </tbody>
          <tfoot className="border-t border-slate-200 bg-slate-50 text-sm">
            <tr>
              <td colSpan={4} className="px-4 py-2 text-right font-medium text-slate-600">
                Nettó végösszeg
              </td>
              <td className="px-4 py-2 text-right font-medium text-slate-900">
                {invoice.netTotal.toLocaleString('hu-HU', { minimumFractionDigits: 2 })} {invoice.currency}
              </td>
            </tr>
            <tr>
              <td colSpan={4} className="px-4 py-2 text-right font-medium text-slate-600">
                ÁFA végösszeg
              </td>
              <td className="px-4 py-2 text-right font-medium text-slate-900">
                {invoice.vatTotal.toLocaleString('hu-HU', { minimumFractionDigits: 2 })} {invoice.currency}
              </td>
            </tr>
            <tr>
              <td colSpan={4} className="px-4 py-2 text-right text-base font-semibold text-slate-900">
                Fizetendő végösszeg
              </td>
              <td className="px-4 py-2 text-right text-base font-semibold text-slate-900">
                {invoice.grossTotal.toLocaleString('hu-HU', { minimumFractionDigits: 2 })} {invoice.currency}
              </td>
            </tr>
          </tfoot>
        </table>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        {invoice.status === 'Finalized' && (
          <>
            <Button variant="secondary" onClick={() => void downloadPdf(false)}>
              PDF letöltése
            </Button>
            <Button variant="secondary" onClick={() => void downloadPdf(true)}>
              Másolat letöltése
            </Button>
          </>
        )}

        {canWrite && invoice.status === 'Draft' && (
          <div className="flex items-end gap-2">
            <select className="rounded-md border border-slate-300 px-3 py-2 text-sm" value={seriesId} onChange={(e) => setSeriesId(e.target.value)}>
              <option value="">— számlatömb —</option>
              {series?.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.prefix}
                </option>
              ))}
            </select>
            <Button disabled={!seriesId || finalizeMutation.isPending} onClick={() => finalizeMutation.mutate()}>
              Véglegesítés
            </Button>
            {series?.length === 0 && (
              <span className="text-sm text-slate-500">
                Nincs még számlatömb —{' '}
                <Link to="/settings/invoice-series" className="underline">
                  hozz létre egyet
                </Link>
                .
              </span>
            )}
          </div>
        )}

        {canWrite && invoice.status === 'Finalized' && invoice.type !== 'Storno' && (
          <div className="flex items-end gap-2">
            <input
              type="date"
              className="rounded-md border border-slate-300 px-3 py-2 text-sm"
              value={stornoDate}
              onChange={(e) => setStornoDate(e.target.value)}
            />
            <Button variant="danger" disabled={stornoMutation.isPending} onClick={() => stornoMutation.mutate()}>
              Sztornózás
            </Button>
          </div>
        )}

        {invoice.originalInvoiceId && (
          <Link to={`/invoices/${invoice.originalInvoiceId}`} className="text-sm text-slate-600 underline">
            Eredeti számla megtekintése
          </Link>
        )}
      </div>
    </div>
  )
}
