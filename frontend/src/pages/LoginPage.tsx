import { useState } from "react";
import { Link } from "react-router-dom";
import { toast } from "sonner";
import { useAuth } from "@/auth/AuthContext";

export default function LoginPage() {
  const { requestMagicLink } = useAuth();
  const [email, setEmail] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [sent, setSent] = useState(false);
  const [devLink, setDevLink] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    try {
      const { devLink } = await requestMagicLink({ email });
      setDevLink(devLink ?? null);
      setSent(true);
    } catch {
      toast.error("Could not send the link. Check the email and try again.");
    } finally {
      setSubmitting(false);
    }
  }

  if (sent) {
    return (
      <div className="text-center">
        <h2 className="text-lg font-semibold text-slate-800">Check your email</h2>
        <p className="text-sm text-slate-500 mt-2">
          We sent a login link to <span className="font-medium">{email}</span>. Open it on
          this device to sign in. The link works once and expires in 20 minutes.
        </p>
        {devLink && (
          <a
            href={devLink}
            className="inline-block mt-4 bg-slate-900 text-white rounded-lg px-4 py-2 text-sm font-medium"
          >
            Open login link (dev)
          </a>
        )}
        <button onClick={() => setSent(false)} className="block mx-auto mt-4 text-sm text-slate-500 underline">
          Use a different email
        </button>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-4">
      <h2 className="text-lg font-semibold text-slate-800">Log in</h2>
      <p className="text-sm text-slate-500">Enter your email and we will send you a login link.</p>
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
      <button
        type="submit"
        disabled={submitting}
        className="bg-slate-900 text-white rounded-lg py-2.5 text-sm font-medium hover:bg-slate-700 disabled:opacity-50"
      >
        {submitting ? "Sending…" : "Send login link"}
      </button>
      <p className="text-sm text-slate-500 text-center">
        New here? <Link to="/register" className="text-slate-700 underline">Create an account</Link>
      </p>
    </form>
  );
}
