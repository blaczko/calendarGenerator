import { Link, Navigate, Route, Routes } from "react-router-dom";
import CustomerWizard from "./pages/CustomerWizard";
import EditOrderPage from "./pages/EditOrderPage";
import ConfirmationPage from "./pages/ConfirmationPage";
import PrivacyPage from "./pages/PrivacyPage";
import AdminLogin from "./pages/admin/AdminLogin";
import AdminDashboard from "./pages/admin/AdminDashboard";
import AdminOrderDetail from "./pages/admin/AdminOrderDetail";

export default function App() {
  return (
    <div className="min-h-screen">
      <header className="border-b border-[var(--line)]/80 bg-[var(--card)]/80 backdrop-blur">
        <div className="mx-auto flex max-w-6xl items-center justify-between gap-4 px-4 py-3">
          <Link to="/" className="font-[Fraunces] text-lg text-[var(--accent-deep)]">
            Családi naptár
          </Link>
          <nav className="flex gap-4 text-sm text-[var(--muted)]">
            <Link to="/privacy" className="hover:text-[var(--ink)]">
              Adatvédelem
            </Link>
            <Link to="/admin" className="hover:text-[var(--ink)]">
              Admin
            </Link>
          </nav>
        </div>
      </header>
      <Routes>
        <Route path="/" element={<CustomerWizard />} />
        <Route path="/edit/:token" element={<EditOrderPage />} />
        <Route path="/confirmation" element={<ConfirmationPage />} />
        <Route path="/privacy" element={<PrivacyPage />} />
        <Route path="/admin" element={<AdminLogin />} />
        <Route path="/admin/dashboard" element={<AdminDashboard />} />
        <Route path="/admin/orders/:id" element={<AdminOrderDetail />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </div>
  );
}
