import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { api } from "@/lib/api";
import { useAuth } from "@/auth/AuthContext";

/* eslint-disable @typescript-eslint/no-explicit-any */
declare global {
  interface Window {
    google?: any;
  }
}

let gisPromise: Promise<void> | null = null;
function loadGis(): Promise<void> {
  if (window.google?.accounts?.id) return Promise.resolve();
  if (gisPromise) return gisPromise;
  gisPromise = new Promise((resolve, reject) => {
    const s = document.createElement("script");
    s.src = "https://accounts.google.com/gsi/client";
    s.async = true;
    s.defer = true;
    s.onload = () => resolve();
    s.onerror = () => reject(new Error("Failed to load Google sign-in"));
    document.head.appendChild(s);
  });
  return gisPromise;
}

// Renders Google's "Continue with Google" button when GOOGLE_CLIENT_ID is set
// on the server. Renders nothing otherwise, so the email flow stands alone.
export default function GoogleSignInButton() {
  const { t } = useTranslation();
  const { signInWithGoogle } = useAuth();
  const navigate = useNavigate();
  const ref = useRef<HTMLDivElement>(null);
  const [clientId, setClientId] = useState<string | null>(null);

  useEffect(() => {
    api
      .get<{ googleClientId: string | null }>("/auth/config")
      .then((r) => setClientId(r.data.googleClientId))
      .catch(() => {});
  }, []);

  useEffect(() => {
    if (!clientId || !ref.current) return;
    let cancelled = false;

    loadGis()
      .then(() => {
        if (cancelled || !window.google || !ref.current) return;
        window.google.accounts.id.initialize({
          client_id: clientId,
          callback: async (resp: { credential: string }) => {
            try {
              await signInWithGoogle(resp.credential);
              navigate("/timeline", { replace: true });
            } catch {
              toast.error(t("auth.googleFailed"));
            }
          },
        });
        window.google.accounts.id.renderButton(ref.current, {
          theme: "outline",
          size: "large",
          text: "continue_with",
          width: 280,
        });
      })
      .catch(() => {});

    return () => {
      cancelled = true;
    };
  }, [clientId, signInWithGoogle, navigate, t]);

  if (!clientId) return null;
  return <div ref={ref} className="flex justify-center" />;
}
