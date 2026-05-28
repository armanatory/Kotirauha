import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { api } from "@/lib/api";
import { useAuth } from "@/auth/AuthContext";
import { CATEGORIES, type Category } from "@/api/types";

function ymd(d: Date) {
  const z = new Date(d);
  z.setMinutes(z.getMinutes() - z.getTimezoneOffset());
  return z.toISOString().slice(0, 10);
}

export default function ExportPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const isBoard = user?.membership?.role === "board" || user?.membership?.role === "admin";
  const [from, setFrom] = useState(() => {
    const d = new Date();
    d.setDate(d.getDate() - 90);
    return ymd(d);
  });
  const [to, setTo] = useState(() => ymd(new Date()));
  const [categories, setCategories] = useState<Category[]>([]);
  const [subjectApartment, setSubjectApartment] = useState("");
  const [includeArchived, setIncludeArchived] = useState(false);
  const [busy, setBusy] = useState(false);

  function toggle(c: Category) {
    setCategories((prev) => (prev.includes(c) ? prev.filter((x) => x !== c) : [...prev, c]));
  }

  async function generate(format: "pdf" | "excel" | "word") {
    setBusy(true);
    try {
      const res = await api.post(
        "/export",
        {
          from: from ? new Date(from).toISOString() : null,
          to: to ? new Date(`${to}T23:59:59`).toISOString() : null,
          categories: categories.length ? categories : null,
          subjectApartment: subjectApartment || null,
          includeArchived,
          format,
        },
        { responseType: "blob" },
      );
      const url = URL.createObjectURL(res.data as Blob);
      if (format === "pdf") {
        window.open(url, "_blank");
      } else {
        const a = document.createElement("a");
        a.href = url;
        a.download = format === "excel" ? "kotirauha-raportti.xlsx" : "kotirauha-raportti.docx";
        document.body.appendChild(a);
        a.click();
        a.remove();
        setTimeout(() => URL.revokeObjectURL(url), 5000);
      }
    } catch {
      toast.error(t("exportPage.couldNotGenerate"));
    } finally {
      setBusy(false);
    }
  }

  if (!user?.membership) return <p className="text-slate-500">{t("exportPage.joinFirst")}</p>;

  return (
    <div className="max-w-xl space-y-4">
      <h1 className="text-xl font-semibold text-slate-800">{t("exportPage.title")}</h1>
      <p className="text-sm text-slate-500">{t("exportPage.intro")}</p>

      <div className="grid grid-cols-2 gap-4">
        <label className="flex flex-col gap-1 text-sm">
          <span className="text-slate-600">{t("exportPage.from")}</span>
          <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="border border-slate-300 rounded-lg px-3 py-2" />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span className="text-slate-600">{t("exportPage.to")}</span>
          <input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="border border-slate-300 rounded-lg px-3 py-2" />
        </label>
      </div>

      <div>
        <span className="text-sm text-slate-600">{t("exportPage.categories")} {t("common.optional")}</span>
        <div className="flex flex-wrap gap-2 mt-1">
          {CATEGORIES.map((c) => (
            <button
              key={c}
              type="button"
              onClick={() => toggle(c)}
              className={`px-3 py-1 rounded-full text-xs border ${categories.includes(c) ? "bg-teal-700 text-white border-teal-700" : "bg-white text-slate-600 border-slate-300"}`}
            >
              {t(`categories.full.${c}`)}
            </button>
          ))}
        </div>
      </div>

      <label className="flex flex-col gap-1 text-sm">
        <span className="text-slate-600">{t("exportPage.location")} {t("common.optional")}</span>
        <input value={subjectApartment} onChange={(e) => setSubjectApartment(e.target.value)} className="border border-slate-300 rounded-lg px-3 py-2" />
      </label>

      {isBoard && (
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <input type="checkbox" checked={includeArchived} onChange={(e) => setIncludeArchived(e.target.checked)} />
          {t("exportPage.includeArchived")}
        </label>
      )}

      <div>
        <p className="text-sm text-slate-600 mb-1">{t("exportPage.downloadAs")}</p>
        <div className="flex flex-wrap gap-2">
          <button onClick={() => generate("pdf")} disabled={busy}
            className="bg-teal-700 text-white rounded-lg py-2 px-5 text-sm font-medium hover:bg-teal-800 disabled:opacity-50">
            {t("exportPage.pdf")}
          </button>
          <button onClick={() => generate("excel")} disabled={busy}
            className="border border-slate-300 text-slate-700 rounded-lg py-2 px-5 text-sm font-medium hover:bg-slate-100 disabled:opacity-50">
            {t("exportPage.excel")}
          </button>
          <button onClick={() => generate("word")} disabled={busy}
            className="border border-slate-300 text-slate-700 rounded-lg py-2 px-5 text-sm font-medium hover:bg-slate-100 disabled:opacity-50">
            {t("exportPage.word")}
          </button>
        </div>
      </div>
    </div>
  );
}
