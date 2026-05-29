import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { useQuery } from "@tanstack/react-query";
import { Loader2 } from "lucide-react";
import { toast } from "sonner";
import { api } from "@/lib/api";
import { compressImage } from "@/lib/image";
import { useAuth } from "@/auth/AuthContext";
import { CATEGORIES, CATEGORY_META, LANGUAGES, type Category } from "@/api/types";

const LOCATIONS = [
  { key: "stairwell", emoji: "🪜" },
  { key: "corridor", emoji: "🚪" },
  { key: "yard", emoji: "🅿️" },
  { key: "basement", emoji: "🧺" },
  { key: "apartment", emoji: "🏠" },
  { key: "other", emoji: "📍" },
] as const;
type LocationKey = (typeof LOCATIONS)[number]["key"];

export default function NewEntryPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { user } = useAuth();
  const textRef = useRef<HTMLTextAreaElement>(null);

  const [text, setText] = useState("");
  const [category, setCategory] = useState<Category>("Other");
  const [language, setLanguage] = useState(user?.preferredLanguage === "en" ? "en" : "fi");
  const [location, setLocation] = useState<LocationKey | null>(null);
  const [apartment, setApartment] = useState("");
  const [occurredAt, setOccurredAt] = useState(localNow());
  const [images, setImages] = useState<File[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const categoryTouched = useRef(false);
  const locationTouched = useRef(false);

  const [refreshNonce, setRefreshNonce] = useState(0);
  const suggestionsQ = useQuery({
    queryKey: ["entry-suggestions", refreshNonce],
    queryFn: async () =>
      (
        await api.get<{ suggestions: string[] }>("/entries/suggestions", {
          params: refreshNonce > 0 ? { refresh: true } : undefined,
        })
      ).data.suggestions,
    enabled: !!user?.membership,
    staleTime: 30 * 60 * 1000,
  });

  useEffect(() => {
    textRef.current?.focus();
  }, []);

  // Let the AI guess the category and location from what's written, unless the
  // user has already chosen them by hand. Debounced so we don't call per keystroke.
  useEffect(() => {
    if (text.trim().length < 8) return;
    if (categoryTouched.current && locationTouched.current) return;
    const handle = setTimeout(async () => {
      try {
        const { data } = await api.post<{ category: string | null; location: string | null }>(
          "/entries/classify",
          { text },
        );
        if (!categoryTouched.current && data.category && (CATEGORIES as readonly string[]).includes(data.category)) {
          setCategory(data.category as Category);
        }
        if (!locationTouched.current && data.location) {
          setLocation(data.location as LocationKey);
        }
      } catch {
        /* detection is best effort */
      }
    }, 800);
    return () => clearTimeout(handle);
  }, [text]);

  if (!user?.membership) {
    return (
      <div className="max-w-md mx-auto text-center py-16">
        <p className="text-slate-600">{t("capture.joinFirst")}</p>
        <button onClick={() => navigate("/building")} className="mt-3 text-slate-900 underline">
          {t("capture.goToBuilding")}
        </button>
      </div>
    );
  }

  async function addImages(files: FileList | null) {
    if (!files) return;
    const compressed = await Promise.all(Array.from(files).map(compressImage));
    setImages((prev) => [...prev, ...compressed]);
  }

  function resolveSubject(): string | undefined {
    if (!location) return undefined;
    if (location === "apartment")
      return apartment.trim() ? t("capture.apartmentPrefix", { num: apartment.trim() }) : t("locations.apartment");
    return t(`locations.${location}`);
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!text.trim()) {
      textRef.current?.focus();
      return;
    }
    setSubmitting(true);
    try {
      const fd = new FormData();
      fd.append("originalText", text.trim());
      fd.append("category", category);
      fd.append("occurredAt", new Date(occurredAt).toISOString());
      fd.append("originalLanguage", language);
      const subject = resolveSubject();
      if (subject) fd.append("subjectApartment", subject);
      images.forEach((img) => fd.append("image", img));
      await api.post<{ id: string }>("/entries", fd);
      toast.success(t("capture.reported"));
      navigate("/timeline");
    } catch {
      toast.error(t("capture.couldNotSave"));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="max-w-md mx-auto pb-44 md:pb-28">
      <h1 className="text-2xl font-semibold text-slate-800">{t("capture.title")}</h1>
      <p className="text-sm text-slate-500 mt-1 mb-4">{t("capture.intro")}</p>

      <textarea
        ref={textRef}
        required
        rows={4}
        value={text}
        onChange={(e) => setText(e.target.value)}
        placeholder={t("capture.placeholder")}
        className="w-full border border-slate-300 rounded-2xl px-4 py-3 text-base focus:outline-none focus:ring-2 focus:ring-teal-500"
      />

      <div className="mt-2 flex items-center gap-2 text-sm text-slate-500">
        <span>{t("capture.writingIn")}</span>
        <select
          value={language}
          onChange={(e) => setLanguage(e.target.value)}
          className="border border-slate-300 rounded-full px-3 py-1 text-slate-700"
        >
          {LANGUAGES.map((l) => (
            <option key={l.code} value={l.code}>{l.label}</option>
          ))}
        </select>
      </div>

      {!text.trim() && (suggestionsQ.isFetching || (suggestionsQ.data?.length ?? 0) > 0) && (
        <div className="mt-4 rounded-2xl border border-teal-100 bg-teal-50/60 p-3">
          <div className="flex items-center justify-between">
            <p className="text-sm font-medium text-teal-800">{t("capture.suggestionsTitle")}</p>
            {(suggestionsQ.data?.length ?? 0) > 0 && (
              <button
                type="button"
                onClick={() => setRefreshNonce((n) => n + 1)}
                disabled={suggestionsQ.isFetching}
                className="text-xs text-teal-700 underline disabled:opacity-50"
              >
                {t("capture.suggestionsRefresh")}
              </button>
            )}
          </div>
          {(suggestionsQ.data?.length ?? 0) > 0 ? (
            <>
              <p className="text-xs text-slate-500 mt-0.5 mb-2">{t("capture.suggestionsHint")}</p>
              <div className="flex flex-wrap gap-2">
                {suggestionsQ.data!.map((s, i) => (
                  <button
                    key={i}
                    type="button"
                    onClick={() => {
                      setText(s);
                      textRef.current?.focus();
                    }}
                    className="text-left rounded-full border border-teal-200 bg-white px-3 py-1.5 text-sm text-slate-700 hover:border-teal-400"
                  >
                    {s}
                  </button>
                ))}
              </div>
            </>
          ) : (
            <p className="mt-1 flex items-center gap-2 text-sm text-slate-500">
              <Loader2 size={15} className="animate-spin text-teal-700" />
              {t("capture.suggestionsLoading")}
            </p>
          )}
        </div>
      )}

      <p className="text-sm text-slate-600 mt-5 mb-2">{t("capture.whatAbout")}</p>
      <div className="grid grid-cols-3 gap-2">
        {CATEGORIES.map((c) => {
          const active = c === category;
          return (
            <button
              key={c}
              type="button"
              onClick={() => { categoryTouched.current = true; setCategory(c); }}
              className={`flex flex-col items-center gap-1 rounded-2xl py-3 border text-sm transition ${
                active ? "bg-teal-700 text-white border-teal-700" : "bg-white text-slate-700 border-slate-200 hover:border-slate-400"
              }`}
            >
              <span className="text-2xl leading-none">{CATEGORY_META[c].emoji}</span>
              <span className="text-xs">{t(`categories.short.${c}`)}</span>
            </button>
          );
        })}
      </div>

      <p className="text-sm text-slate-600 mt-5 mb-2">
        {t("capture.whereIs")} <span className="text-slate-400">{t("common.optional")}</span>
      </p>
      <div className="flex flex-wrap gap-2">
        {LOCATIONS.map((l) => {
          const active = l.key === location;
          return (
            <button
              key={l.key}
              type="button"
              onClick={() => { locationTouched.current = true; setLocation(active ? null : l.key); }}
              className={`inline-flex items-center gap-1.5 rounded-full border px-3 py-2 text-sm transition ${
                active ? "bg-teal-700 text-white border-teal-700" : "bg-white text-slate-700 border-slate-200 hover:border-slate-400"
              }`}
            >
              <span>{l.emoji}</span>
              <span>{t(`locations.${l.key}`)}</span>
            </button>
          );
        })}
      </div>
      {location === "apartment" && (
        <div className="mt-2 rounded-xl border border-teal-200 bg-teal-50/60 p-3">
          <label className="block text-sm font-medium text-teal-800 mb-1">{t("capture.apartmentLabel")}</label>
          <input
            value={apartment}
            onChange={(e) => setApartment(e.target.value)}
            placeholder={t("capture.apartmentPlaceholder")}
            className="w-full border border-slate-300 rounded-xl px-3 py-2 text-sm"
          />
          <p className="text-xs text-slate-500 mt-1">{t("capture.apartmentHint")}</p>
        </div>
      )}

      <p className="text-sm text-slate-600 mt-5 mb-2">
        {t("capture.addPhoto")} <span className="text-slate-400">{t("common.optional")}</span>
      </p>
      <div className="flex gap-2">
        <label className="flex-1 inline-flex items-center justify-center gap-2 cursor-pointer rounded-xl border border-slate-300 px-3 py-3 text-slate-700">
          <span>📷</span><span>{t("capture.takePhoto")}</span>
          <input type="file" accept="image/*" capture="environment" className="hidden" onChange={(e) => void addImages(e.target.files)} />
        </label>
        <label className="flex-1 inline-flex items-center justify-center gap-2 cursor-pointer rounded-xl border border-slate-300 px-3 py-3 text-slate-700">
          <span>🖼️</span><span>{t("capture.gallery")}</span>
          <input type="file" accept="image/*" multiple className="hidden" onChange={(e) => void addImages(e.target.files)} />
        </label>
      </div>
      {images.length > 0 && (
        <div className="mt-3 flex flex-wrap gap-2">
          {images.map((img, i) => (
            <div key={i} className="relative">
              <img src={URL.createObjectURL(img)} alt="" className="h-20 w-20 object-cover rounded-lg border border-slate-200" />
              <button
                type="button"
                onClick={() => setImages((prev) => prev.filter((_, j) => j !== i))}
                className="absolute -top-2 -right-2 bg-teal-700 text-white rounded-full w-6 h-6 text-xs"
                aria-label={t("capture.removePhoto")}
              >
                ✕
              </button>
            </div>
          ))}
        </div>
      )}

      <label className="flex flex-col gap-1 text-sm mt-5">
        <span className="text-slate-600">{t("capture.whenHappened")}</span>
        <input
          type="datetime-local"
          value={occurredAt}
          onChange={(e) => setOccurredAt(e.target.value)}
          className="border border-slate-300 rounded-xl px-3 py-2"
        />
      </label>

      {/* Always-visible action bar so the Save button is never hidden below the
          fold (it floats above the mobile nav / beside the desktop sidebar). */}
      <div className="fixed left-0 right-0 z-40 px-4 md:left-60 md:px-6 bottom-[calc(env(safe-area-inset-bottom)+64px)] md:bottom-4 pointer-events-none">
        <div className="max-w-md mx-auto">
          <button
            type="submit"
            disabled={submitting}
            className="pointer-events-auto w-full bg-teal-700 text-white rounded-2xl py-3.5 text-base font-semibold shadow-xl ring-1 ring-black/5 hover:bg-teal-800 disabled:opacity-50"
          >
            {submitting ? t("capture.saving") : t("capture.save")}
          </button>
        </div>
      </div>
    </form>
  );
}

function localNow() {
  const d = new Date();
  d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
  return d.toISOString().slice(0, 16);
}
