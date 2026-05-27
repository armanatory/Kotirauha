import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { api } from "@/lib/api";
import { useAuth } from "@/auth/AuthContext";
import { CATEGORIES, CATEGORY_LABELS, type Category } from "@/api/types";

const LANGS = [
  { code: "en", label: "English" },
  { code: "fi", label: "Finnish" },
  { code: "sv", label: "Swedish" },
  { code: "ar", label: "Arabic" },
  { code: "ru", label: "Russian" },
];

function localNow() {
  const d = new Date();
  d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
  return d.toISOString().slice(0, 16);
}

export default function NewEntryPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const [category, setCategory] = useState<Category>("Noise");
  const [occurredAt, setOccurredAt] = useState(localNow());
  const [language, setLanguage] = useState(user?.preferredLanguage ?? "en");
  const [text, setText] = useState("");
  const [subjectApartment, setSubjectApartment] = useState("");
  const [images, setImages] = useState<File[]>([]);
  const [submitting, setSubmitting] = useState(false);

  if (!user?.membership) {
    return (
      <p className="text-slate-500">
        Join a building first on the <span className="font-medium">Building</span> page.
      </p>
    );
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setSubmitting(true);
    try {
      const fd = new FormData();
      fd.append("originalText", text);
      fd.append("category", category);
      fd.append("occurredAt", new Date(occurredAt).toISOString());
      fd.append("originalLanguage", language);
      if (subjectApartment) fd.append("subjectApartment", subjectApartment);
      images.forEach((img) => fd.append("image", img));
      const { data } = await api.post<{ id: string }>("/entries", fd);
      toast.success("Entry recorded.");
      navigate(`/entries/${data.id}`);
    } catch {
      toast.error("Could not save the entry.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="max-w-xl space-y-4">
      <h1 className="text-xl font-semibold text-slate-800">New entry</h1>
      <p className="text-sm text-slate-500">
        Write in your own language. The original is always kept; a translation into your
        building's shared language is added automatically.
      </p>

      <div className="grid grid-cols-2 gap-4">
        <label className="flex flex-col gap-1 text-sm">
          <span className="text-slate-600">Category</span>
          <select value={category} onChange={(e) => setCategory(e.target.value as Category)} className="border border-slate-300 rounded-lg px-3 py-2">
            {CATEGORIES.map((c) => (
              <option key={c} value={c}>{CATEGORY_LABELS[c]}</option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span className="text-slate-600">When it happened</span>
          <input type="datetime-local" value={occurredAt} onChange={(e) => setOccurredAt(e.target.value)} className="border border-slate-300 rounded-lg px-3 py-2" />
        </label>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <label className="flex flex-col gap-1 text-sm">
          <span className="text-slate-600">Your language</span>
          <select value={language} onChange={(e) => setLanguage(e.target.value)} className="border border-slate-300 rounded-lg px-3 py-2">
            {LANGS.map((l) => (
              <option key={l.code} value={l.code}>{l.label}</option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span className="text-slate-600">Apartment concerned (optional)</span>
          <input value={subjectApartment} onChange={(e) => setSubjectApartment(e.target.value)} className="border border-slate-300 rounded-lg px-3 py-2" />
        </label>
      </div>

      <label className="flex flex-col gap-1 text-sm">
        <span className="text-slate-600">Description</span>
        <textarea
          required
          rows={5}
          value={text}
          onChange={(e) => setText(e.target.value)}
          placeholder="Describe what you observed, factually."
          className="border border-slate-300 rounded-lg px-3 py-2"
        />
      </label>

      <label className="flex flex-col gap-1 text-sm">
        <span className="text-slate-600">Images (optional)</span>
        <input
          type="file"
          accept="image/*"
          multiple
          onChange={(e) => setImages(Array.from(e.target.files ?? []))}
          className="text-sm"
        />
      </label>

      <button
        type="submit"
        disabled={submitting}
        className="bg-slate-900 text-white rounded-lg py-2 px-5 text-sm font-medium hover:bg-slate-700 disabled:opacity-50"
      >
        Record entry
      </button>
    </form>
  );
}
