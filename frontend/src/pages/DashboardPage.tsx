import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { invoicesApi } from '../api/invoices'
import { ErrorBanner, Spinner } from '../components/Feedback'

export function DashboardPage() {
  const { data: invoices, isLoading, error } = useQuery({ queryKey: ['invoices', undefined], queryFn: () => invoicesApi.list() })

  if (isLoading) return <Spinner />
  if (error) return <ErrorBanner message="Nem sikerült betölteni az áttekintést." />
  if (!invoices) return null

  const drafts = invoices.filter((i) => i.status === 'Draft')
  const finalized = invoices.filter((i) => i.status === 'Finalized')
  const today = new Date().toISOString().slice(0, 10)
  // "kifizetett" (paid) tracking doesn't exist in the backend yet — this only approximates
  // "lejárt" (overdue) using the payment due date on finalized invoices, nothing about whether
  // it was actually paid.
  const overdue = finalized.filter((i) => i.paymentDueDate < today)
  const grossByCurrency = finalized.reduce<Record<string, number>>((acc, i) => {
    acc[i.currency] = (acc[i.currency] ?? 0) + i.grossTotal
    return acc
  }, {})

  return (
    <div>
      <h1 className="mb-6 text-2xl font-semibold text-slate-900">Áttekintés</h1>

      <div className="mb-8 grid grid-cols-4 gap-4">
        <StatCard label="Piszkozatok" value={drafts.length} />
        <StatCard label="Véglegesített számlák" value={finalized.length} />
        <StatCard label="Lejárt fizetési határidejű" value={overdue.length} />
        <StatCard
          label="Összesen (bruttó)"
          value={
            Object.entries(grossByCurrency)
              .map(([currency, amount]) => `${amount.toLocaleString('hu-HU', { minimumFractionDigits: 2 })} ${currency}`)
              .join(', ') || '—'
          }
        />
      </div>

      <h2 className="mb-2 text-lg font-medium text-slate-900">Legutóbbi számlák</h2>
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
        <table className="min-w-full divide-y divide-slate-200 text-sm">
          <tbody className="divide-y divide-slate-100">
            {invoices.slice(0, 5).map((invoice) => (
              <tr key={invoice.id} className="hover:bg-slate-50">
                <td className="px-4 py-2">
                  <Link to={`/invoices/${invoice.id}`} className="font-medium text-slate-900 hover:underline">
                    {invoice.number ?? 'piszkozat'}
                  </Link>
                </td>
                <td className="px-4 py-2 text-slate-600">{invoice.partnerName}</td>
                <td className="px-4 py-2 text-slate-600">{invoice.issueDate}</td>
                <td className={`px-4 py-2 ${invoice.status === 'Finalized' && invoice.paymentDueDate < today ? 'text-red-600' : 'text-slate-600'}`}>
                  {invoice.status === 'Finalized' ? 'véglegesítve' : 'piszkozat'}
                </td>
              </tr>
            ))}
            {invoices.length === 0 && (
              <tr>
                <td className="px-4 py-6 text-center text-slate-500">Még nincs számla.</td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}

function StatCard({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="rounded-lg border border-slate-200 bg-white p-4">
      <p className="text-sm text-slate-500">{label}</p>
      <p className="mt-1 text-2xl font-semibold text-slate-900">{value}</p>
    </div>
  )
}
