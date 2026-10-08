import { NavLink, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'

const navItems = [
  { to: '/', label: 'Áttekintés', end: true },
  { to: '/invoices', label: 'Számlák' },
  { to: '/partners', label: 'Partnerek' },
  { to: '/settings/invoice-series', label: 'Számlatömbök' },
]

export function AppLayout() {
  const { user, logout } = useAuth()

  return (
    <div className="flex min-h-screen bg-slate-50">
      <aside className="flex w-56 shrink-0 flex-col border-r border-slate-200 bg-white">
        <div className="border-b border-slate-200 px-4 py-4">
          <span className="text-lg font-semibold text-slate-900">Számla</span>
        </div>
        <nav className="flex-1 space-y-1 px-2 py-4">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.end}
              className={({ isActive }) =>
                `block rounded-md px-3 py-2 text-sm font-medium ${
                  isActive ? 'bg-slate-800 text-white' : 'text-slate-700 hover:bg-slate-100'
                }`
              }
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
        <div className="border-t border-slate-200 px-4 py-3 text-xs text-slate-500">
          <div className="truncate" title={user?.email}>
            {user?.email}
          </div>
          <button className="mt-2 text-slate-600 underline hover:text-slate-900" onClick={() => void logout()}>
            Kijelentkezés
          </button>
        </div>
      </aside>
      <main className="flex-1 p-6">
        <Outlet />
      </main>
    </div>
  )
}
