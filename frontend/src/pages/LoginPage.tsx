import { useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useAuth } from "@/auth/AuthContext";
import MagicLinkSent from "@/auth/MagicLinkSent";

export default function LoginPage() {
  const { t } = useTranslation();
  const { requestMagicLink } = useAuth();
  const [email, setEmail] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [sent, setSent] = useState(false);
  const [devLink, setDevLink] = useState<string | null>(null);
  const [devCode, setDevCode] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    try {
      const { devLink, devCode } = await requestMagicLink({ email });
      setDevLink(devLink ?? null);
      setDevCode(devCode ?? null);
      setSent(true);
    } catch {
      toast.error(t("auth.couldNotSend"));
    } finally {
      setSubmitting(false);
    }
  }

  if (sent) {
    return <MagicLinkSent email={email} devLink={devLink} devCode={devCode} onUseDifferent={() => setSent(false)} />;
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-4">
      <h2 className="text-lg font-semibold text-slate-800">{t("auth.login")}</h2>
      <p className="text-sm text-slate-500">{t("auth.loginIntro")}</p>
      <label className="flex flex-col gap-1 text-sm">
        <span className="text-slate-600">{t("auth.email")}</span>
        <input
          type="email"
          required
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          className="border border-slate-300 rounded-lg px-3 py-2"
        />
      </label>
      <button
        type="submit"
        disabled={submitting}
        className="bg-teal-700 text-white rounded-lg py-2.5 text-sm font-medium hover:bg-teal-800 disabled:opacity-50"
      >
        {submitting ? t("auth.sending") : t("auth.sendLink")}
      </button>
      <p className="text-sm text-slate-500 text-center">
        {t("auth.newHere")} <Link to="/register" className="text-slate-700 underline">{t("auth.register")}</Link>
      </p>
    </form>
  );
}
