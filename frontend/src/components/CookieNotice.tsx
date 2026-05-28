import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";

const ACK_KEY = "kotirauha_cookie_ack";

// A light, dismissible usage/cookie notice. The app only uses local storage for
// the login token and language, so this is a notice rather than a consent gate.
export default function CookieNotice() {
  const { t } = useTranslation();
  const [show, setShow] = useState(false);

  useEffect(() => {
    setShow(!localStorage.getItem(ACK_KEY));
  }, []);

  if (!show) return null;

  function dismiss() {
    localStorage.setItem(ACK_KEY, "1");
    setShow(false);
  }

  return (
    <div className="fixed inset-x-0 bottom-0 z-50 p-3 pb-[max(0.75rem,env(safe-area-inset-bottom))]">
      <div className="mx-auto max-w-2xl bg-white border border-slate-200 shadow-lg rounded-xl p-4 flex flex-col sm:flex-row sm:items-center gap-3">
        <p className="text-sm text-slate-600 flex-1">
          {t("cookie.message")}{" "}
          <Link to="/privacy" className="text-teal-700 underline">{t("cookie.learnMore")}</Link>
        </p>
        <button
          onClick={dismiss}
          className="shrink-0 bg-teal-700 text-white rounded-lg px-4 py-2 text-sm font-medium hover:bg-teal-800"
        >
          {t("cookie.ok")}
        </button>
      </div>
    </div>
  );
}
