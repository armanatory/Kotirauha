import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Smartphone, Apple } from "lucide-react";
import { BRAND } from "@/lib/branding";
import LanguageToggle from "@/components/LanguageToggle";

export default function LandingPage() {
  const { t } = useTranslation();
  return (
    <div className="brand-wash min-h-screen flex flex-col items-center justify-center px-4 text-center">
      <LanguageToggle className="absolute top-4 right-4 shadow-sm" />
      <img src="/icon.svg" alt="" className="w-20 h-20 rounded-3xl shadow-md mb-5" />
      <h1 className="text-4xl font-semibold text-slate-800 tracking-tight">{BRAND.name}</h1>
      <p className="mt-3 max-w-md text-slate-600 leading-relaxed">{t("brand.description")}</p>
      <p className="mt-2 text-sm text-slate-400">{t("brand.tagline")}</p>

      <div className="mt-8 flex gap-3">
        <Link
          to="/login"
          className="px-5 py-2.5 rounded-lg bg-teal-700 text-white text-sm font-medium hover:bg-teal-800"
        >
          {t("landing.login")}
        </Link>
        <Link
          to="/register"
          className="px-5 py-2.5 rounded-lg border border-slate-300 text-slate-700 text-sm font-medium hover:bg-slate-100"
        >
          {t("landing.createAccount")}
        </Link>
      </div>

      <div className="mt-10 w-full max-w-xs">
        <p className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-3">
          {t("landing.installPrompt")}
        </p>
        <div className="flex flex-col gap-2">
          <Link
            to="/install?p=android"
            className="flex items-center justify-center gap-2 px-4 py-2.5 rounded-lg border border-slate-300 text-slate-700 text-sm font-medium hover:bg-slate-100"
          >
            <Smartphone size={18} /> {t("landing.installAndroid")}
          </Link>
          <Link
            to="/install?p=ios"
            className="flex items-center justify-center gap-2 px-4 py-2.5 rounded-lg border border-slate-300 text-slate-700 text-sm font-medium hover:bg-slate-100"
          >
            <Apple size={18} /> {t("landing.installIos")}
          </Link>
        </div>
      </div>

      <Link to="/privacy" className="mt-10 text-xs text-slate-400 underline hover:text-slate-600">
        {t("landing.privacy")}
      </Link>
    </div>
  );
}
