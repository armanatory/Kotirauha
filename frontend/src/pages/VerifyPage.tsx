import { useEffect, useRef, useState } from "react";
import { useNavigate, useSearchParams, Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useAuth } from "@/auth/AuthContext";

export default function VerifyPage() {
  const { t } = useTranslation();
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
          <h2 className="text-lg font-semibold text-slate-800">{t("auth.linkExpired")}</h2>
          <p className="text-sm text-slate-500 mt-2">{t("auth.linkExpiredBody")}</p>
          <Link to="/login" className="inline-block mt-4 bg-slate-900 text-white rounded-lg px-4 py-2 text-sm font-medium">
            {t("auth.getNewLink")}
          </Link>
        </>
      ) : (
        <p className="text-slate-500">{t("auth.signingIn")}</p>
      )}
    </div>
  );
}
