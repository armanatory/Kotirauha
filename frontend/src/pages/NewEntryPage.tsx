import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { api } from "@/lib/api";
import { useAuth } from "@/auth/AuthContext";
import { CATEGORIES, CATEGORY_META, LANGUAGES, type Category } from "@/api/types";

export default function NewEntryPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const textRef = useRef<HTMLTextAreaElement>(null);

  const [text, setText] = useState("");
  const [category, setCategory] = useState<Category>("Other");
  const [language, setLanguage] = useState(user?.preferredLanguage ?? "en");
  const [occurredAt, setOccurredAt] = useState(localNow());
  const [subjectApartment, setSubjectApartment] = useState("");
  const [images, setImages] = useState<File[]>([]);
  const [showDetails, setShowDetails] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  // Open the keyboard immediately so the user can just start typing.
  useEffect(() => {
    textRef.current?.focus();
  }, []);

  if (!user?.membership) {
    return (
      <div className="max-w-md mx-auto text-center py-16">
        <p className="text-slate-600">Join your building first.</p>
        <button onClick={() => navigate("/building")} className="mt-3 text-slate-900 underline">
          Go to Building
        </button>
      </div>
    );
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
      if (subjectApartment) fd.append("subjectApartment", subjectApartment);
      images.forEach((img) => fd.append("image", img));
      await api.post<{ id: string }>("/entries", fd);
      toast.success("Reported — thanks.");
      navigate("/timeline");
    } catch {
      toast.error("Could not save. Please try again.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="max-w-md mx-auto pb-28">
      <h1 className="text-2xl font-semibold text-slate-800">Report something</h1>
      <p className="text-sm text-slate-500 mt-1 mb-4">
        Just describe it in your own words — we keep your original and add a
        translation for the building automatically.
      </p>

      <textarea
        ref={textRef}
        required
        rows={4}
        value={text}
        onChange={(e) => setText(e.target.value)}
        placeholder="e.g. Strong burning smell in the stairwell tonight"
        className="w-full border border-slate-300 rounded-2xl px-4 py-3 text-base focus:outline-none focus:ring-2 focus:ring-slate-400"
      />

      <div className="mt-2 flex items-center gap-2 text-sm text-slate-500">
        <span>Writing in</span>
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

      <p className="text-sm text-slate-600 mt-5 mb-2">What is it about?</p>
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
              <span className="text-xs">{CATEGORY_META[c].short}</span>
            </button>
          );
        })}
      </div>

      <button
        type="button"
        onClick={() => setShowDetails((v) => !v)}
        className="mt-5 text-sm text-slate-500 underline"
      >
        {showDetails ? "Hide details" : "Add details (optional)"}
      </button>

      {showDetails && (
        <div className="mt-3 space-y-3">
          <label className="flex flex-col gap-1 text-sm">
            <span className="text-slate-600">When it happened</span>
            <input
              type="datetime-local"
              value={occurredAt}
              onChange={(e) => setOccurredAt(e.target.value)}
              className="border border-slate-300 rounded-xl px-3 py-2"
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span className="text-slate-600">Apartment concerned</span>
            <input
              value={subjectApartment}
              onChange={(e) => setSubjectApartment(e.target.value)}
              placeholder="e.g. C12"
              className="border border-slate-300 rounded-xl px-3 py-2"
            />
          </label>
          <div className="flex flex-col gap-1 text-sm">
            <span className="text-slate-600">Photo</span>
            <label className="inline-flex items-center gap-2 cursor-pointer">
              <span className="rounded-xl border border-slate-300 px-4 py-2 text-slate-700">📷 Add photo</span>
              <input
                type="file"
                accept="image/*"
                capture="environment"
                multiple
                onChange={(e) => setImages(Array.from(e.target.files ?? []))}
                className="hidden"
              />
              {images.length > 0 && <span className="text-slate-500">{images.length} selected</span>}
            </label>
          </div>
        </div>
      )}

      {/* Thumb-reachable primary action, fixed above the bottom nav on phones */}
      <div className="fixed inset-x-0 bottom-16 md:static md:mt-6 px-4 md:px-0 z-10">
        <button
          type="submit"
          disabled={submitting}
          className="w-full bg-slate-900 text-white rounded-2xl py-3.5 text-base font-semibold shadow-lg hover:bg-slate-700 disabled:opacity-50"
        >
          {submitting ? "Saving…" : "Save report"}
        </button>
      </div>
    </form>
  );
}

function localNow() {
  const d = new Date();
  d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
  return d.toISOString().slice(0, 16);
}
