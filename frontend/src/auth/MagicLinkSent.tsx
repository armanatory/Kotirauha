import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useAuth } from "@/auth/AuthContext";

interface Props {
  email: string;
  devLink?: string | null;
  devCode?: string | null;
  onUseDifferent?: () => void;
}

// Shared "we emailed you" screen: the user can type the 6-digit code OR
// click the link from the email. Both log in.
export default function MagicLinkSent({ email, devLink, devCode, onUseDifferent }: Props) {
  const { t } = useTranslation();
  const { verifyCode } = useAuth();
  const navigate = useNavigate();
  const [code, setCode] = useState("");
  const [submitting, setSubmitting] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    try {
      await verifyCode(email, code);
      navigate("/timeline", { replace: true });
    } catch {
      toast.error(t("auth.invalidCode"));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="text-center">
      <h2 className="text-lg font-semibold text-slate-800">{t("auth.checkEmail")}</h2>
      <p className="text-sm text-slate-500 mt-2">{t("auth.checkEmailBoth", { email })}</p>

      <form onSubmit={submit} className="mt-5 flex flex-col gap-3">
        <input
          inputMode="numeric"
          autoComplete="one-time-code"
          pattern="[0-9]*"
          maxLength={6}
          autoFocus
          value={code}
          onChange={(e) => setCode(e.target.value.replace(/\D/g, ""))}
          placeholder={t("auth.codePlaceholder")}
          className="border border-slate-300 rounded-lg px-3 py-3 text-center text-2xl tracking-[0.4em] font-semibold"
        />
        <button
          type="submit"
          disabled={submitting || code.length < 6}
          className="bg-slate-900 text-white rounded-lg py-2.5 text-sm font-medium hover:bg-slate-700 disabled:opacity-50"
        >
          {submitting ? t("auth.verifying") : t("auth.verifyCode")}
        </button>
      </form>

      {devLink && (
        <a href={devLink} className="inline-block mt-4 text-sm text-slate-500 underline">
          {t("auth.openLinkDev")}{devCode ? ` (code ${devCode})` : ""}
        </a>
      )}
      {onUseDifferent && (
        <button onClick={onUseDifferent} className="block mx-auto mt-4 text-sm text-slate-500 underline">
          {t("auth.useDifferentEmail")}
        </button>
      )}
    </div>
  );
}
