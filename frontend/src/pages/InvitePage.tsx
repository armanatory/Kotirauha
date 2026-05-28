import { useEffect, useRef, useState } from "react";
import { useParams, useNavigate, Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Smartphone, Apple } from "lucide-react";
import { api } from "@/lib/api";
import { useAuth, setPendingInvite, clearPendingInvite } from "@/auth/AuthContext";
import type { InvitePreview } from "@/api/types";
import LanguageToggle from "@/components/LanguageToggle";
import GoogleSignInButton from "@/components/GoogleSignInButton";
import { GoogleDivider } from "@/pages/LoginPage";

type State =
  | { kind: "loading" }
  | { kind: "notFound" }
  | { kind: "expired"; buildingName: string }
  | { kind: "ready"; buildingName: string; title: string | null }
  | { kind: "joined"; buildingName: string }
  | { kind: "alreadyMember" };

export default function InvitePage() {
  const { t } = useTranslation();
  const { token } = useParams<{ token: string }>();
  const navigate = useNavigate();
  const { user, loading, refresh } = useAuth();
  const [state, setState] = useState<State>({ kind: "loading" });
  const redeemed = useRef(false);

  useEffect(() => {
    if (!token || loading) return;
    let cancelled = false;

    (async () => {
      try {
        const { data } = await api.get<InvitePreview>(`/invites/${token}`);
        if (cancelled) return;

        if (!data.valid) {
          setState({ kind: "expired", buildingName: data.buildingName });
          clearPendingInvite();
          return;
        }

        // Keep the token so it is redeemed automatically after sign-in.
        setPendingInvite(token);

        if (!user) {
          setState({ kind: "ready", buildingName: data.buildingName, title: data.title });
          return;
        }

        if (user.membership) {
          clearPendingInvite();
          setState({ kind: "alreadyMember" });
          return;
        }

        // Signed in, no building yet: join straight away.
        if (redeemed.current) return;
        redeemed.current = true;
        await api.post(`/invites/${token}/redeem`, {});
        clearPendingInvite();
        await refresh();
        if (!cancelled) setState({ kind: "joined", buildingName: data.buildingName });
      } catch {
        if (!cancelled) setState({ kind: "notFound" });
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [token, user, loading, refresh]);

  return (
    <div className="min-h-screen brand-wash flex flex-col items-center justify-center px-4">
      <div className="absolute top-4 right-4">
        <LanguageToggle />
      </div>
      <div className="w-full max-w-sm bg-white border border-slate-200 rounded-2xl p-6 text-center shadow-sm">
        <div className="w-12 h-12 rounded-full bg-teal-600/10 text-teal-700 flex items-center justify-center mx-auto mb-3 text-xl">
          ✉️
        </div>

        {state.kind === "loading" && <p className="text-slate-500">{t("common.loading")}</p>}

        {state.kind === "notFound" && (
          <>
            <h1 className="text-lg font-semibold text-slate-800">{t("invite.notFoundTitle")}</h1>
            <p className="text-sm text-slate-500 mt-2">{t("invite.notFoundBody")}</p>
            <Link to="/" className="inline-block mt-4 text-sm text-teal-700 underline">{t("invite.goHome")}</Link>
          </>
        )}

        {state.kind === "expired" && (
          <>
            <h1 className="text-lg font-semibold text-slate-800">{t("invite.expiredTitle")}</h1>
            <p className="text-sm text-slate-500 mt-2">{t("invite.expiredBody", { building: state.buildingName })}</p>
            <Link to="/building" className="inline-block mt-4 text-sm text-teal-700 underline">{t("invite.otherWays")}</Link>
          </>
        )}

        {state.kind === "ready" && (
          <>
            <h1 className="text-lg font-semibold text-slate-800">
              {t("invite.invitedTitle", { building: state.buildingName })}
            </h1>
            {state.title && <p className="text-sm text-teal-700 mt-1">{state.title}</p>}
            <p className="text-sm text-slate-500 mt-2">{t("invite.invitedBody")}</p>
            <button
              onClick={() => navigate("/login")}
              className="mt-5 w-full bg-teal-700 text-white rounded-lg py-2.5 text-sm font-medium hover:bg-teal-800"
            >
              {t("invite.continue")}
            </button>
            <div className="mt-4 space-y-3">
              <GoogleDivider />
              <GoogleSignInButton />
            </div>
            <InstallLinks />
          </>
        )}

        {state.kind === "joined" && (
          <>
            <h1 className="text-lg font-semibold text-slate-800">
              {t("invite.joinedTitle", { building: state.buildingName })}
            </h1>
            <p className="text-sm text-slate-500 mt-2">{t("invite.joinedBody")}</p>
            <button
              onClick={() => navigate("/timeline", { replace: true })}
              className="mt-5 w-full bg-teal-700 text-white rounded-lg py-2.5 text-sm font-medium hover:bg-teal-800"
            >
              {t("invite.openApp")}
            </button>
            <InstallLinks />
          </>
        )}

        {state.kind === "alreadyMember" && (
          <>
            <h1 className="text-lg font-semibold text-slate-800">{t("invite.alreadyTitle")}</h1>
            <p className="text-sm text-slate-500 mt-2">{t("invite.alreadyBody")}</p>
            <button
              onClick={() => navigate("/timeline", { replace: true })}
              className="mt-5 w-full bg-teal-700 text-white rounded-lg py-2.5 text-sm font-medium hover:bg-teal-800"
            >
              {t("invite.openApp")}
            </button>
          </>
        )}
      </div>
    </div>
  );
}

function InstallLinks() {
  const { t } = useTranslation();
  return (
    <div className="mt-6 pt-4 border-t border-slate-100">
      <p className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-2">
        {t("landing.installPrompt")}
      </p>
      <div className="flex flex-col gap-2">
        <Link to="/install?p=android" className="flex items-center justify-center gap-2 px-4 py-2 rounded-lg border border-slate-300 text-slate-700 text-sm hover:bg-slate-100">
          <Smartphone size={16} /> {t("landing.installAndroid")}
        </Link>
        <Link to="/install?p=ios" className="flex items-center justify-center gap-2 px-4 py-2 rounded-lg border border-slate-300 text-slate-700 text-sm hover:bg-slate-100">
          <Apple size={16} /> {t("landing.installIos")}
        </Link>
      </div>
    </div>
  );
}
