import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { Share, MoreVertical, PlusSquare, Check, Globe, ArrowRight } from "lucide-react";
import { BRAND } from "@/lib/branding";
import LanguageToggle from "@/components/LanguageToggle";

type Platform = "ios" | "android" | "other";

function detectPlatform(): Platform {
  if (typeof navigator === "undefined") return "other";
  const ua = navigator.userAgent || "";
  const isIpad = /Macintosh/.test(ua) && (navigator.maxTouchPoints ?? 0) > 1;
  if (/iPhone|iPad|iPod/.test(ua) || isIpad) return "ios";
  if (/Android/i.test(ua)) return "android";
  return "other";
}

// Landing buttons pass ?p=android or ?p=ios so the right steps show even when
// the visitor is on something else (previewing on desktop, sending to a friend).
function platformFromUrl(): Platform | null {
  if (typeof window === "undefined") return null;
  const p = new URLSearchParams(window.location.search).get("p")?.toLowerCase();
  return p === "android" ? "android" : p === "ios" ? "ios" : null;
}

interface Step {
  n: number;
  mainKey: string;
  subKey?: string;
  icon: React.ReactNode;
}

const STEPS: Record<Platform, Step[]> = {
  ios: [
    { n: 1, mainKey: "install.ios1main", subKey: "install.ios1sub", icon: <Globe size={18} /> },
    { n: 2, mainKey: "install.ios2main", subKey: "install.ios2sub", icon: <Share size={18} /> },
    { n: 3, mainKey: "install.ios3main", subKey: "install.ios3sub", icon: <PlusSquare size={18} /> },
  ],
  android: [
    { n: 1, mainKey: "install.android1main", subKey: "install.android1sub", icon: <Globe size={18} /> },
    { n: 2, mainKey: "install.android2main", subKey: "install.android2sub", icon: <MoreVertical size={18} /> },
    { n: 3, mainKey: "install.android3main", subKey: "install.android3sub", icon: <PlusSquare size={18} /> },
  ],
  other: [
    { n: 1, mainKey: "install.other1main", subKey: "install.other1sub", icon: <Globe size={18} /> },
    { n: 2, mainKey: "install.other2main", subKey: "install.other2sub", icon: <MoreVertical size={18} /> },
    { n: 3, mainKey: "install.other3main", subKey: "install.other3sub", icon: <Check size={18} /> },
  ],
};

export default function InstallPage() {
  const { t } = useTranslation();
  const [platform, setPlatform] = useState<Platform>("other");

  useEffect(() => {
    setPlatform(platformFromUrl() ?? detectPlatform());
  }, []);

  const subhead = platform === "ios" ? t("install.subIos") : platform === "android" ? t("install.subAndroid") : t("install.subOther");
  const footnote = platform === "ios" ? t("install.footIos") : platform === "android" ? t("install.footAndroid") : t("install.footOther");
  const steps = STEPS[platform];

  return (
    <div className="brand-wash min-h-screen px-4 py-10">
      <LanguageToggle className="absolute top-4 right-4 shadow-sm" />
      <div className="max-w-sm mx-auto">
        <div className="flex justify-center mb-5">
          <img src="/icon.svg" alt={BRAND.name} className="w-16 h-16 rounded-2xl shadow-sm" />
        </div>
        <h1 className="text-2xl font-semibold text-center text-slate-800">{t("install.title")}</h1>
        <p className="text-center text-slate-500 mt-1 mb-5 text-sm">{subhead}</p>

        <div className="flex justify-center mb-6">
          <div className="inline-flex bg-slate-100 rounded-lg p-1 text-xs font-medium">
            {(["ios", "android", "other"] as const).map((p) => (
              <button
                key={p}
                type="button"
                onClick={() => setPlatform(p)}
                className={`px-3 py-1.5 rounded-md transition ${
                  platform === p ? "bg-white text-slate-900 shadow-sm" : "text-slate-500 hover:text-slate-700"
                }`}
              >
                {p === "ios" ? t("install.tabIos") : p === "android" ? t("install.tabAndroid") : t("install.tabOther")}
              </button>
            ))}
          </div>
        </div>

        <div className="bg-white rounded-2xl border border-slate-200 overflow-hidden mb-3">
          {steps.map((s, i) => (
            <div key={s.n} className={`flex items-start gap-4 p-4 ${i < steps.length - 1 ? "border-b border-slate-100" : ""}`}>
              <div className="shrink-0 w-7 h-7 rounded-full bg-teal-600/10 flex items-center justify-center mt-0.5">
                <span className="text-xs font-bold text-teal-700">{s.n}</span>
              </div>
              <div className="flex-1 min-w-0">
                <div className="text-sm font-semibold text-slate-800">{t(s.mainKey)}</div>
                {s.subKey && <div className="text-xs text-slate-500 mt-0.5">{t(s.subKey)}</div>}
              </div>
              <div className="shrink-0 text-slate-400 mt-0.5">{s.icon}</div>
            </div>
          ))}
        </div>

        <p className="text-xs text-center text-slate-400 mb-8">{footnote}</p>

        <p className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-2.5">{t("install.useInBrowser")}</p>
        <Link
          to="/login"
          className="flex items-center justify-between gap-3 p-3 rounded-xl border border-slate-200 bg-white hover:bg-slate-50 transition"
        >
          <span className="flex items-center gap-3">
            <span className="w-8 h-8 rounded-lg bg-teal-600/10 flex items-center justify-center">
              <Globe size={16} className="text-teal-700" />
            </span>
            <span className="text-sm font-semibold text-slate-800">{t("install.openApp")}</span>
          </span>
          <ArrowRight size={16} className="text-slate-400" />
        </Link>

        <div className="text-center mt-8">
          <Link to="/" className="text-sm text-slate-500 hover:text-slate-800">← {t("install.back")}</Link>
        </div>
      </div>
    </div>
  );
}
