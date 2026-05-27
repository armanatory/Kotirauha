import { Link } from "react-router-dom";
import { BRAND } from "@/lib/branding";

export default function LandingPage() {
  return (
    <div className="min-h-screen flex flex-col items-center justify-center px-4 text-center">
      <h1 className="text-4xl font-semibold text-slate-800">{BRAND.name}</h1>
      <p className="mt-3 max-w-xl text-slate-600">{BRAND.description}</p>
      <p className="mt-2 text-sm text-slate-400">{BRAND.tagline}</p>
      <div className="mt-8 flex gap-3">
        <Link
          to="/login"
          className="px-5 py-2.5 rounded-lg bg-slate-900 text-white text-sm font-medium hover:bg-slate-700"
        >
          Log in
        </Link>
        <Link
          to="/register"
          className="px-5 py-2.5 rounded-lg border border-slate-300 text-slate-700 text-sm font-medium hover:bg-slate-100"
        >
          Create account
        </Link>
      </div>
    </div>
  );
}
