import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Download, X } from "lucide-react";

const KEY = "kotirauha_install_hint";

function isStandalone() {
  return (
    window.matchMedia?.("(display-mode: standalone)").matches ||
    // iOS Safari
    (window.navigator as unknown as { standalone?: boolean }).standalone === true
  );
}

// Shown inside the app only when it is NOT running as an installed PWA, so
// browser users are nudged to install it. Dismissible and remembered.
export default function InstallHint() {
  const { t } = useTranslation();
  const [show, setShow] = useState(false);

  useEffect(() => {
    if (!isStandalone() && !localStorage.getItem(KEY)) setShow(true);
  }, []);

  if (!show) return null;

  return (
    <div className="mb-4 flex items-center gap-2 rounded-xl border border-teal-200 bg-teal-50 px-3 py-2 text-sm">
      <Download size={16} className="text-teal-700 shrink-0" />
      <span className="text-slate-700 flex-1">{t("install.hint")}</span>
      <Link to="/install" className="text-teal-700 font-medium underline shrink-0">
        {t("install.hintCta")}
      </Link>
      <button
        onClick={() => {
          localStorage.setItem(KEY, "1");
          setShow(false);
        }}
        aria-label={t("install.hintDismiss")}
        className="text-slate-400 hover:text-slate-600 shrink-0"
      >
        <X size={16} />
      </button>
    </div>
  );
}
