import { Navigate, Route, Routes } from 'react-router-dom'
import { RequireAuth } from './auth/RequireAuth'
import { AppLayout } from './layouts/AppLayout'
import { DashboardPage } from './pages/DashboardPage'
import { LoginPage } from './pages/LoginPage'
import { RegisterTenantPage } from './pages/RegisterTenantPage'
import { InvoiceCreatePage } from './pages/invoices/InvoiceCreatePage'
import { InvoiceDetailPage } from './pages/invoices/InvoiceDetailPage'
import { InvoicesListPage } from './pages/invoices/InvoicesListPage'
import { PartnerCreatePage } from './pages/partners/PartnerCreatePage'
import { PartnerDetailPage } from './pages/partners/PartnerDetailPage'
import { PartnersListPage } from './pages/partners/PartnersListPage'
import { InvoiceSeriesPage } from './pages/settings/InvoiceSeriesPage'

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register-tenant" element={<RegisterTenantPage />} />

      <Route
        element={
          <RequireAuth>
            <AppLayout />
          </RequireAuth>
        }
      >
        <Route index element={<DashboardPage />} />
        <Route path="partners" element={<PartnersListPage />} />
        <Route path="partners/new" element={<PartnerCreatePage />} />
        <Route path="partners/:id" element={<PartnerDetailPage />} />
        <Route path="invoices" element={<InvoicesListPage />} />
        <Route path="invoices/new" element={<InvoiceCreatePage />} />
        <Route path="invoices/:id" element={<InvoiceDetailPage />} />
        <Route path="settings/invoice-series" element={<InvoiceSeriesPage />} />
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
