import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { invoicesApi } from '../../api/invoices'
import type { InvoiceStatus } from '../../api/types'
import { useAuth } from '../../auth/AuthContext'
import { Badge } from '../../components/Feedback'
import { Button } from '../../components/Button'
import { ErrorBanner, Spinner } from '../../components/Feedback'

const statusLabel: Record<InvoiceStatus, { text: string; tone: 'warning' | 'success' }> = {
  Draft: { text: 'Piszkozat', tone: 'warning' },
  Finalized: { text: 'Véglegesítve', tone: 'success' },
}

const typeLabel = { Normal: 'Számla', Storno: 'Sztornó', Modification: 'Módosító' } as const

export function InvoicesListPage() {
  const { canWrite } = useAuth()
  const [status, setStatus] = useState<InvoiceStatus | ''>('')
  const { data: invoices, isLoading, error } = useQuery({
    queryKey: ['invoices', status],
    queryFn: () => invoicesApi.list(status || undefined),
  })

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-slate-900">Számlák</h1>
        {canWrite && (
          <Link to="/invoices/new">
            <Button>Új számla</Button>
          </Link>
        )}
      </div>

      <select
        className="mb-4 block rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
        value={status}
        onChange={(e) => setStatus(e.target.value as InvoiceStatus | '')}
      >
        <option value="">Összes állapot</option>
        <option value="Draft">Piszkozat</option>
        <option value="Finalized">Véglegesítve</option>
      </select>

      {isLoading && <Spinner />}
      {error && <ErrorBanner message="Nem sikerült betölteni a számlákat." />}

      {invoices && (
        <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
          <table className="min-w-full divide-y divide-slate-200 text-sm">
            <thead className="bg-slate-50">
              <tr>
                <th className="px-4 py-2 text-left font-medium text-slate-600">Szám</th>
                <th className="px-4 py-2 text-left font-medium text-slate-600">Típus</th>
                <th className="px-4 py-2 text-left font-medium text-slate-600">Vevő</th>
                <th className="px-4 py-2 text-left font-medium text-slate-600">Kiállítás</th>
                <th className="px-4 py-2 text-right font-medium text-slate-600">Bruttó</th>
                <th className="px-4 py-2 text-left font-medium text-slate-600">Állapot</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {invoices.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-6 text-center text-slate-500">
                    Nincs még számla.
                  </td>
                </tr>
              )}
              {invoices.map((invoice) => (
                <tr key={invoice.id} className="hover:bg-slate-50">
                  <td className="px-4 py-2 font-medium text-slate-900">
                    <Link to={`/invoices/${invoice.id}`} className="hover:underline">
                      {invoice.number ?? 'piszkozat'}
                    </Link>
                  </td>
                  <td className="px-4 py-2 text-slate-600">{typeLabel[invoice.type]}</td>
                  <td className="px-4 py-2 text-slate-600">{invoice.partnerName}</td>
                  <td className="px-4 py-2 text-slate-600">{invoice.issueDate}</td>
                  <td className="px-4 py-2 text-right text-slate-900">
                    {invoice.grossTotal.toLocaleString('hu-HU', { minimumFractionDigits: 2 })} {invoice.currency}
                  </td>
                  <td className="px-4 py-2">
                    <Badge tone={statusLabel[invoice.status].tone}>{statusLabel[invoice.status].text}</Badge>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
