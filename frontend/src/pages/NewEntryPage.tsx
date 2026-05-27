import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { api } from "@/lib/api";
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
  const [showDetails, setShowDetails] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    textRef.current?.focus();
  }, []);

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

  function addImages(files: FileList | null) {
    if (!files) return;
    setImages((prev) => [...prev, ...Array.from(files)]);
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
    <form onSubmit={handleSubmit} className="max-w-md mx-auto pb-28">
      <h1 className="text-2xl font-semibold text-slate-800">{t("capture.title")}</h1>
      <p className="text-sm text-slate-500 mt-1 mb-4">{t("capture.intro")}</p>

      <textarea
        ref={textRef}
        required
        rows={4}
        value={text}
        onChange={(e) => setText(e.target.value)}
        placeholder={t("capture.placeholder")}
        className="w-full border border-slate-300 rounded-2xl px-4 py-3 text-base focus:outline-none focus:ring-2 focus:ring-slate-400"
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

      <p className="text-sm text-slate-600 mt-5 mb-2">{t("capture.whatAbout")}</p>
      <div className="grid grid-cols-3 gap-2">
        {CATEGORIES.map((c) => {
          const active = c === category;
          return (
            <button
              key={c}
              type="button"
              onClick={() => setCategory(c)}
              className={`flex flex-col items-center gap-1 rounded-2xl py-3 border text-sm transition ${
                active ? "bg-slate-900 text-white border-slate-900" : "bg-white text-slate-700 border-slate-200 hover:border-slate-400"
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
              onClick={() => setLocation(active ? null : l.key)}
              className={`inline-flex items-center gap-1.5 rounded-full border px-3 py-2 text-sm transition ${
                active ? "bg-slate-900 text-white border-slate-900" : "bg-white text-slate-700 border-slate-200 hover:border-slate-400"
              }`}
            >
              <span>{l.emoji}</span>
              <span>{t(`locations.${l.key}`)}</span>
            </button>
          );
        })}
      </div>
      {location === "apartment" && (
        <input
          value={apartment}
          onChange={(e) => setApartment(e.target.value)}
          placeholder={t("capture.apartmentPlaceholder")}
          className="mt-2 w-full border border-slate-300 rounded-xl px-3 py-2 text-sm"
        />
      )}

      <p className="text-sm text-slate-600 mt-5 mb-2">
        {t("capture.addPhoto")} <span className="text-slate-400">{t("common.optional")}</span>
      </p>
      <div className="flex gap-2">
        <label className="flex-1 inline-flex items-center justify-center gap-2 cursor-pointer rounded-xl border border-slate-300 px-3 py-3 text-slate-700">
          <span>📷</span><span>{t("capture.takePhoto")}</span>
          <input type="file" accept="image/*" capture="environment" className="hidden" onChange={(e) => addImages(e.target.files)} />
        </label>
        <label className="flex-1 inline-flex items-center justify-center gap-2 cursor-pointer rounded-xl border border-slate-300 px-3 py-3 text-slate-700">
          <span>🖼️</span><span>{t("capture.gallery")}</span>
          <input type="file" accept="image/*" multiple className="hidden" onChange={(e) => addImages(e.target.files)} />
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
                className="absolute -top-2 -right-2 bg-slate-900 text-white rounded-full w-6 h-6 text-xs"
                aria-label={t("capture.removePhoto")}
              >
                ✕
              </button>
            </div>
          ))}
        </div>
      )}

      <button
        type="button"
        onClick={() => setShowDetails((v) => !v)}
        className="mt-5 text-sm text-slate-500 underline"
      >
        {showDetails ? t("capture.hide") : t("capture.changeTime")}
      </button>
      {showDetails && (
        <label className="flex flex-col gap-1 text-sm mt-3">
          <span className="text-slate-600">{t("capture.whenHappened")}</span>
          <input
            type="datetime-local"
            value={occurredAt}
            onChange={(e) => setOccurredAt(e.target.value)}
            className="border border-slate-300 rounded-xl px-3 py-2"
          />
        </label>
      )}

      <button
        type="submit"
        disabled={submitting}
        className="mt-6 w-full bg-slate-900 text-white rounded-2xl py-3.5 text-base font-semibold shadow-sm hover:bg-slate-700 disabled:opacity-50"
      >
        {submitting ? t("capture.saving") : t("capture.save")}
      </button>
    </form>
  );
}

function localNow() {
  const d = new Date();
  d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
  return d.toISOString().slice(0, 16);
}
