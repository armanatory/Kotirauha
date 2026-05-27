import { useEffect, useRef, useState } from "react";
import { NavLink, Outlet, useNavigate, Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Home, PlusCircle, Building2, FileText, BarChart3, Shield, User, LogOut } from "lucide-react";
import { useAuth } from "@/auth/AuthContext";
import { BRAND } from "@/lib/branding";

interface NavItem {
  to: string;
  key: string;
  icon: typeof Home;
  boardOnly?: boolean;
  accent?: boolean;
}

const navItems: NavItem[] = [
  { to: "/timeline", key: "nav.timeline", icon: Home },
  { to: "/entries/new", key: "nav.newEntry", icon: PlusCircle, accent: true },
  { to: "/insights", key: "nav.insights", icon: BarChart3, boardOnly: true },
  { to: "/building", key: "nav.building", icon: Building2 },
  { to: "/export", key: "nav.export", icon: FileText },
];

export default function AppLayout() {
  const { t } = useTranslation();
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const isBoard = user?.membership?.role === "board" || user?.membership?.role === "admin";
  const isAdmin = user?.isAdmin ?? false;
  const items = navItems.filter((item) => !item.boardOnly || isBoard);

  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    function onClick(e: MouseEvent) {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) setMenuOpen(false);
    }
    document.addEventListener("mousedown", onClick);
    return () => document.removeEventListener("mousedown", onClick);
  }, []);

  function handleLogout() {
    logout();
    navigate("/login");
  }

  const initial = (user?.displayName || user?.email || "?").charAt(0).toUpperCase();

  return (
    <div className="min-h-screen flex flex-col md:flex-row">
      {/* Desktop sidebar */}
      <aside className="hidden md:flex md:flex-col w-60 bg-white border-r border-slate-200 p-4">
        <div className="text-xl font-semibold text-slate-800 mb-6">{BRAND.name}</div>
        <nav className="flex flex-col gap-1">
          {items.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                `flex items-center gap-3 px-3 py-2 rounded-lg text-sm font-medium ${
                  isActive ? "bg-slate-900 text-white" : "text-slate-600 hover:bg-slate-100"
                }`
              }
            >
              <item.icon size={18} />
              {t(item.key)}
            </NavLink>
          ))}
        </nav>
      </aside>

      <div className="flex-1 flex flex-col">
        {/* Top bar */}
        <header className="h-14 bg-white border-b border-slate-200 flex items-center justify-between px-4 sticky top-0 z-30">
          <span className="text-sm text-slate-500 truncate">
            {user?.membership?.buildingName ?? BRAND.name}
          </span>
          <div className="relative" ref={menuRef}>
            <button
              onClick={() => setMenuOpen((v) => !v)}
              className="flex items-center gap-2 rounded-full pl-1 pr-2 py-1 hover:bg-slate-100"
            >
              <span className="w-8 h-8 rounded-full bg-slate-900 text-white text-sm font-semibold flex items-center justify-center">
                {initial}
              </span>
              <span className="text-sm text-slate-700 max-w-28 truncate">{user?.displayName}</span>
            </button>
            {menuOpen && (
              <div className="absolute right-0 mt-1 w-44 bg-white border border-slate-200 rounded-xl shadow-lg py-1 z-40">
                <Link
                  to="/profile"
                  onClick={() => setMenuOpen(false)}
                  className="flex items-center gap-2 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50"
                >
                  <User size={16} /> {t("account.profile")}
                </Link>
                {isAdmin && (
                  <Link
                    to="/admin"
                    onClick={() => setMenuOpen(false)}
                    className="flex items-center gap-2 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50"
                  >
                    <Shield size={16} /> {t("nav.admin")}
                  </Link>
                )}
                <button
                  onClick={handleLogout}
                  className="w-full flex items-center gap-2 px-3 py-2 text-sm text-slate-700 hover:bg-slate-50"
                >
                  <LogOut size={16} /> {t("account.logout")}
                </button>
              </div>
            )}
          </div>
        </header>

        <main className="flex-1 p-4 md:p-6 overflow-auto pb-24 md:pb-6">
          <Outlet />
        </main>

        {/* Mobile bottom nav */}
        <nav className="md:hidden fixed bottom-0 inset-x-0 bg-white border-t border-slate-200 flex justify-around items-stretch z-30 pb-[env(safe-area-inset-bottom)]">
          {items.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              className={({ isActive }) =>
                `flex flex-col items-center justify-center gap-0.5 flex-1 py-2 text-[11px] ${
                  isActive ? "text-slate-900 font-semibold" : "text-slate-500"
                }`
              }
            >
              {({ isActive }) =>
                item.accent ? (
                  <>
                    <span className="-mt-4 mb-0.5 w-12 h-12 rounded-full bg-slate-900 text-white flex items-center justify-center shadow-lg">
                      <item.icon size={24} />
                    </span>
                    <span>{t(item.key)}</span>
                  </>
                ) : (
                  <>
                    <item.icon size={22} strokeWidth={isActive ? 2.4 : 1.8} />
                    <span>{t(item.key)}</span>
                  </>
                )
              }
            </NavLink>
          ))}
        </nav>
      </div>
    </div>
  );
}
