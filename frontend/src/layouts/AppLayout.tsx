import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useAuth } from "@/auth/AuthContext";
import { BRAND } from "@/lib/branding";

const navItems = [
  { to: "/timeline", key: "nav.timeline" },
  { to: "/entries/new", key: "nav.newEntry" },
  { to: "/building", key: "nav.building" },
  { to: "/export", key: "nav.export" },
  { to: "/profile", key: "nav.profile" },
];

export default function AppLayout() {
  const { t } = useTranslation();
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  function handleLogout() {
    logout();
    navigate("/login");
  }

  return (
    <div className="min-h-screen flex flex-col md:flex-row">
      <aside className="hidden md:flex md:flex-col w-60 bg-white border-r border-slate-200 p-4">
        <div className="text-xl font-semibold text-slate-800 mb-6">{BRAND.name}</div>
        <nav className="flex flex-col gap-1">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                `px-3 py-2 rounded-lg text-sm font-medium ${
                  isActive ? "bg-slate-900 text-white" : "text-slate-600 hover:bg-slate-100"
                }`
              }
            >
              {t(item.key)}
            </NavLink>
          ))}
        </nav>
      </aside>

      <div className="flex-1 flex flex-col">
        <header className="h-14 bg-white border-b border-slate-200 flex items-center justify-between px-4">
          <span className="text-sm text-slate-500">
            {user?.membership?.buildingName ?? BRAND.name}
          </span>
          <div className="flex items-center gap-3 text-sm">
            <span className="text-slate-600">{user?.displayName}</span>
            <button onClick={handleLogout} className="text-slate-500 hover:text-slate-900">
              {t("auth.logout")}
            </button>
          </div>
        </header>

        <main className="flex-1 p-4 md:p-6 overflow-auto pb-20 md:pb-6">
          <Outlet />
        </main>

        <nav className="md:hidden fixed bottom-0 inset-x-0 bg-white border-t border-slate-200 flex justify-around py-2">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                `text-xs px-2 ${isActive ? "text-slate-900 font-semibold" : "text-slate-500"}`
              }
            >
              {t(item.key)}
            </NavLink>
          ))}
        </nav>
      </div>
    </div>
  );
}
