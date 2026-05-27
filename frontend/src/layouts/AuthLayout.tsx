import { Outlet, Link } from "react-router-dom";
import { BRAND } from "@/lib/branding";

export default function AuthLayout() {
  return (
    <div className="min-h-screen flex flex-col items-center justify-center px-4">
      <div className="w-full max-w-md">
        <Link to="/" className="block text-center mb-6">
          <span className="text-2xl font-semibold text-slate-800">{BRAND.name}</span>
          <p className="text-sm text-slate-500 mt-1">{BRAND.tagline}</p>
        </Link>
        <div className="bg-white rounded-xl shadow-sm border border-slate-200 p-6">
          <Outlet />
        </div>
      </div>
    </div>
  );
}
