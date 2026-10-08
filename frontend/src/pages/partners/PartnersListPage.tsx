import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { partnersApi } from '../../api/partners'
import { useAuth } from '../../auth/AuthContext'
import { Button } from '../../components/Button'
import { ErrorBanner, Spinner } from '../../components/Feedback'

export function PartnersListPage() {
  const { canWrite } = useAuth()
  const [search, setSearch] = useState('')
  const { data: partners, isLoading, error } = useQuery({
    queryKey: ['partners', search],
    queryFn: () => partnersApi.list(search || undefined),
  })

  return (
    <div>
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-semibold text-slate-900">Partnerek</h1>
        {canWrite && (
          <Link to="/partners/new">
            <Button>Új partner</Button>
          </Link>
        )}
      </div>

      <input
        className="mb-4 block w-full max-w-xs rounded-md border border-slate-300 px-3 py-2 text-sm shadow-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500"
        placeholder="Keresés név szerint…"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
      />

      {isLoading && <Spinner />}
      {error && <ErrorBanner message="Nem sikerült betölteni a partnereket." />}

      {partners && (
        <div className="overflow-hidden rounded-lg border border-slate-200 bg-white">
          <table className="min-w-full divide-y divide-slate-200 text-sm">
            <thead className="bg-slate-50">
              <tr>
                <th className="px-4 py-2 text-left font-medium text-slate-600">Név</th>
                <th className="px-4 py-2 text-left font-medium text-slate-600">Adószám</th>
                <th className="px-4 py-2 text-left font-medium text-slate-600">Cím</th>
                <th className="px-4 py-2 text-left font-medium text-slate-600">E-mail</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {partners.length === 0 && (
                <tr>
                  <td colSpan={4} className="px-4 py-6 text-center text-slate-500">
                    Nincs még partner.
                  </td>
                </tr>
              )}
              {partners.map((partner) => (
                <tr key={partner.id} className="hover:bg-slate-50">
                  <td className="px-4 py-2 font-medium text-slate-900">
                    <Link to={`/partners/${partner.id}`} className="hover:underline">
                      {partner.name}
                    </Link>
                  </td>
                  <td className="px-4 py-2 text-slate-600">{partner.taxId ?? partner.euVatId ?? '—'}</td>
                  <td className="px-4 py-2 text-slate-600">{partner.address}</td>
                  <td className="px-4 py-2 text-slate-600">{partner.email ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
