import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useAuth } from "@/auth/AuthContext";

export default function LoginPage() {
  const { t } = useTranslation();
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    try {
      await login(email, password);
      navigate("/timeline");
    } catch {
      toast.error("Invalid email or password.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-4">
      <h2 className="text-lg font-semibold text-slate-800">{t("auth.login")}</h2>
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
        {t("auth.login")}
      </button>
      <p className="text-sm text-slate-500 text-center">
        <Link to="/register" className="text-slate-700 underline">
          {t("auth.register")}
        </Link>
      </p>
    </form>
  );
}
