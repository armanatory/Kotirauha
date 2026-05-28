import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import type { ReactNode } from "react";
import { useAuth } from "@/auth/AuthContext";
import AuthLayout from "@/layouts/AuthLayout";
import AppLayout from "@/layouts/AppLayout";
import LandingPage from "@/pages/LandingPage";
import InstallPage from "@/pages/InstallPage";
import InvitePage from "@/pages/InvitePage";
import LoginPage from "@/pages/LoginPage";
import RegisterPage from "@/pages/RegisterPage";
import VerifyPage from "@/pages/VerifyPage";
import CompleteProfilePage from "@/pages/CompleteProfilePage";
import TimelinePage from "@/pages/TimelinePage";
import NewEntryPage from "@/pages/NewEntryPage";
import EntryDetailPage from "@/pages/EntryDetailPage";
import BuildingPage from "@/pages/BuildingPage";
import InsightsPage from "@/pages/InsightsPage";
import AdminPage from "@/pages/AdminPage";
import ResourcesPage from "@/pages/ResourcesPage";
import ExportPage from "@/pages/ExportPage";
import ProfilePage from "@/pages/ProfilePage";
import NotFoundPage from "@/pages/NotFoundPage";

function Protected({ children }: { children: ReactNode }) {
  const { user, loading } = useAuth();
  if (loading) return <div className="p-6 text-slate-500">Loading…</div>;
  if (!user) return <Navigate to="/login" replace />;
  if (!user.displayName) return <CompleteProfilePage />;
  return <>{children}</>;
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<LandingPage />} />
        <Route path="/install" element={<InstallPage />} />
        <Route path="/invite/:token" element={<InvitePage />} />
        <Route element={<AuthLayout />}>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/auth/verify" element={<VerifyPage />} />
        </Route>
        <Route
          element={
            <Protected>
              <AppLayout />
            </Protected>
          }
        >
          <Route path="/timeline" element={<TimelinePage />} />
          <Route path="/entries/new" element={<NewEntryPage />} />
          <Route path="/entries/:id" element={<EntryDetailPage />} />
          <Route path="/building" element={<BuildingPage />} />
          <Route path="/insights" element={<InsightsPage />} />
          <Route path="/admin" element={<AdminPage />} />
          <Route path="/good-to-know" element={<ResourcesPage />} />
          <Route path="/export" element={<ExportPage />} />
          <Route path="/profile" element={<ProfilePage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
