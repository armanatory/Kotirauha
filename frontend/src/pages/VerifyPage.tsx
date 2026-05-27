import { useEffect, useRef, useState } from "react";
import { useNavigate, useSearchParams, Link } from "react-router-dom";
import { useAuth } from "@/auth/AuthContext";

export default function VerifyPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { verify } = useAuth();
  const [error, setError] = useState(false);
  const ran = useRef(false);

  useEffect(() => {
    if (ran.current) return;
    ran.current = true;
    const token = params.get("token");
    if (!token) {
      setError(true);
      return;
    }
    verify(token)
      .then(() => navigate("/timeline", { replace: true }))
      .catch(() => setError(true));
  }, [params, verify, navigate]);

  return (
    <div className="text-center">
      {error ? (
        <>
          <h2 className="text-lg font-semibold text-slate-800">Link expired</h2>
          <p className="text-sm text-slate-500 mt-2">
            This login link is invalid or has already been used.
          </p>
          <Link to="/login" className="inline-block mt-4 bg-slate-900 text-white rounded-lg px-4 py-2 text-sm font-medium">
            Get a new link
          </Link>
        </>
      ) : (
        <p className="text-slate-500">Signing you in…</p>
      )}
    </div>
  );
}
