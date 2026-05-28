import { useEffect } from "react";
import { useLocation } from "react-router-dom";
import i18n from "@/i18n";
import { api } from "@/lib/api";

// Records a first-party page view on every route change. Fire-and-forget; the
// backend stores a coarse country and a salted visitor hash, never a raw IP.
export default function PageTracker() {
  const { pathname } = useLocation();

  useEffect(() => {
    void api
      .post("/track", {
        path: pathname,
        referrer: document.referrer || null,
        language: i18n.language || navigator.language || null,
      })
      .catch(() => {});
  }, [pathname]);

  return null;
}
