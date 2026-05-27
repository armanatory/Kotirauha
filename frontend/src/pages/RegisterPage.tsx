import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useAuth } from "@/auth/AuthContext";

export default function RegisterPage() {
  const { t } = useTranslation();
  const { register } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    try {
      await register({ email, password, displayName, preferredLanguage: "en" });
      navigate("/building");
    } catch (err: unknown) {
      const message =
        (err as { response?: { data?: { detail?: string } } })?.response?.data?.detail ??
        "Could not create account.";
      toast.error(message);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-4">
      <h2 className="text-lg font-semibold text-slate-800">{t("auth.register")}</h2>
      <label className="flex flex-col gap-1 text-sm">
        <span className="text-slate-600">{t("auth.displayName")}</span>
        <input
          required
          value={displayName}
          onChange={(e) => setDisplayName(e.target.value)}
          className="border border-slate-300 rounded-lg px-3 py-2"
        />
      </label>
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
      <label className="flex flex-col gap-1 text-sm">
        <span className="text-slate-600">{t("auth.password")}</span>
        <div className="flex">
          <input
            type={showPassword ? "text" : "password"}
            required
            minLength={8}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            className="border border-slate-300 rounded-l-lg px-3 py-2 flex-1"
          />
          <button
            type="button"
            onClick={() => setShowPassword((v) => !v)}
            className="border border-l-0 border-slate-300 rounded-r-lg px-3 text-xs text-slate-500"
          >
            {showPassword ? "Hide" : "Show"}
          </button>
        </div>
      </label>
      <button
        type="submit"
        disabled={submitting}
        className="bg-slate-900 text-white rounded-lg py-2 text-sm font-medium hover:bg-slate-700 disabled:opacity-50"
      >
        {t("auth.register")}
      </button>
      <p className="text-sm text-slate-500 text-center">
        <Link to="/login" className="text-slate-700 underline">
          {t("auth.login")}
        </Link>
      </p>
    </form>
  );
}
