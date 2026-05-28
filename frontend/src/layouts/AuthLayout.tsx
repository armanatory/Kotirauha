import { Outlet, Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { BRAND } from "@/lib/branding";
import LanguageToggle from "@/components/LanguageToggle";

export default function AuthLayout() {
  const { t } = useTranslation();
  return (
    <div className="brand-wash min-h-screen flex flex-col items-center justify-center px-4">
      <LanguageToggle className="absolute top-4 right-4 shadow-sm" />
      <div className="w-full max-w-md">
        <Link to="/" className="flex flex-col items-center mb-6">
          <img src="/icon.svg" alt="" className="w-14 h-14 rounded-2xl shadow-sm mb-3" />
          <span className="text-2xl font-semibold text-slate-800">{BRAND.name}</span>
          <p className="text-sm text-slate-500 mt-1 text-center">{t("brand.tagline")}</p>
        </Link>
        <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-6">
          <Outlet />
        </div>
      </div>
    </div>
  );
}
