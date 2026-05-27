import { useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { useAuth } from "@/auth/AuthContext";
import { LANGUAGES } from "@/api/types";

export default function RegisterPage() {
  const { i18n } = useTranslation();
  const { requestMagicLink } = useAuth();
  const [email, setEmail] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [language, setLanguage] = useState(i18n.language === "en" ? "en" : "fi");
  const [submitting, setSubmitting] = useState(false);
  const [sent, setSent] = useState(false);
  const [devLink, setDevLink] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    try {
      const { devLink } = await requestMagicLink({ email, displayName, preferredLanguage: language });
      setDevLink(devLink ?? null);
      setSent(true);
    } catch {
      toast.error("Could not send the link. Please try again.");
    } finally {
      setSubmitting(false);
    }
  }

  if (sent) {
    return (
      <div className="text-center">
        <h2 className="text-lg font-semibold text-slate-800">Check your email</h2>
        <p className="text-sm text-slate-500 mt-2">
          We sent a login link to <span className="font-medium">{email}</span>. Open it on this
          device to finish signing up.
        </p>
        {devLink && (
          <a href={devLink} className="inline-block mt-4 bg-slate-900 text-white rounded-lg px-4 py-2 text-sm font-medium">
            Open login link (dev)
          </a>
        )}
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-4">
      <h2 className="text-lg font-semibold text-slate-800">Create account</h2>
      <p className="text-sm text-slate-500">No password needed. We will email you a login link.</p>
      <label className="flex flex-col gap-1 text-sm">
        <span className="text-slate-600">Your name</span>
        <input
          required
          value={displayName}
          onChange={(e) => setDisplayName(e.target.value)}
          className="border border-slate-300 rounded-lg px-3 py-2"
        />
      </label>
      <label className="flex flex-col gap-1 text-sm">
        <span className="text-slate-600">Email</span>
        <input
          type="email"
          required
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          className="border border-slate-300 rounded-lg px-3 py-2"
        />
      </label>
      <label className="flex flex-col gap-1 text-sm">
        <span className="text-slate-600">Your language</span>
        <select
          value={language}
          onChange={(e) => setLanguage(e.target.value)}
          className="border border-slate-300 rounded-lg px-3 py-2"
        >
          {LANGUAGES.map((l) => (
            <option key={l.code} value={l.code}>{l.label}</option>
          ))}
        </select>
      </label>
      <button
        type="submit"
        disabled={submitting}
        className="bg-slate-900 text-white rounded-lg py-2.5 text-sm font-medium hover:bg-slate-700 disabled:opacity-50"
      >
        {submitting ? "Sending…" : "Send login link"}
      </button>
      <p className="text-sm text-slate-500 text-center">
        Already have an account? <Link to="/login" className="text-slate-700 underline">Log in</Link>
      </p>
    </form>
  );
}
