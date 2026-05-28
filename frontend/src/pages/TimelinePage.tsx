import { useState } from "react";
import { Link, Navigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { api } from "@/lib/api";
import { useAuth } from "@/auth/AuthContext";
import { CATEGORIES, type Category, type EntryListItem } from "@/api/types";

export default function TimelinePage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const isBoard = user?.membership?.role === "board" || user?.membership?.role === "admin";
  const [category, setCategory] = useState<Category | "">("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [q, setQ] = useState("");
  const [includeArchived, setIncludeArchived] = useState(false);

  const entriesQ = useQuery({
    queryKey: ["entries", category, from, to, q, includeArchived],
    queryFn: async () => {
      const params: Record<string, string | boolean> = {};
      if (category) params.category = category;
      if (from) params.from = new Date(from).toISOString();
      if (to) params.to = new Date(to).toISOString();
      if (q) params.q = q;
      if (includeArchived) params.includeArchived = true;
      return (await api.get<EntryListItem[]>("/entries", { params })).data;
    },
  });

  if (!user?.membership) {
    return <Navigate to="/building" replace />;
  }

  return (
    <div className="max-w-3xl">
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-xl font-semibold text-slate-800">{t("timeline.title")}</h1>
        <div className="flex items-center gap-3">
          <Link to="/export" className="text-sm text-slate-500 underline hover:text-slate-800">
            {t("nav.export")}
          </Link>
          <Link to="/entries/new" className="bg-slate-900 text-white rounded-lg px-4 py-2 text-sm font-medium hover:bg-slate-700">
            {t("nav.newEntry")}
          </Link>
        </div>
      </div>

      <div className="bg-white border border-slate-200 rounded-xl p-3 mb-4 flex flex-wrap gap-2 items-end">
        <label className="flex flex-col text-xs text-slate-500">
          {t("timeline.category")}
          <select value={category} onChange={(e) => setCategory(e.target.value as Category | "")} className="border border-slate-300 rounded-lg px-2 py-1.5 text-sm">
            <option value="">{t("timeline.all")}</option>
            {CATEGORIES.map((c) => (
              <option key={c} value={c}>{t(`categories.full.${c}`)}</option>
            ))}
          </select>
        </label>
        <label className="flex flex-col text-xs text-slate-500">
          {t("timeline.from")}
          <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="border border-slate-300 rounded-lg px-2 py-1.5 text-sm" />
        </label>
        <label className="flex flex-col text-xs text-slate-500">
          {t("timeline.to")}
          <input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="border border-slate-300 rounded-lg px-2 py-1.5 text-sm" />
        </label>
        <label className="flex flex-col text-xs text-slate-500 flex-1 min-w-40">
          {t("timeline.search")}
          <input value={q} onChange={(e) => setQ(e.target.value)} placeholder={t("timeline.keyword")} className="border border-slate-300 rounded-lg px-2 py-1.5 text-sm" />
        </label>
        {isBoard && (
          <label className="flex items-center gap-1.5 text-xs text-slate-500 pb-1.5">
            <input type="checkbox" checked={includeArchived} onChange={(e) => setIncludeArchived(e.target.checked)} />
            {t("timeline.includeArchived")}
          </label>
        )}
      </div>

      {entriesQ.isLoading ? (
        <p className="text-slate-500">{t("common.loading")}</p>
      ) : entriesQ.data && entriesQ.data.length > 0 ? (
        <ul className="space-y-2">
          {entriesQ.data.map((e) => (
            <li key={e.id}>
              <Link
                to={`/entries/${e.id}`}
                className={`block bg-white border rounded-xl p-3 hover:border-slate-400 ${e.archived ? "border-dashed border-slate-300 opacity-70" : "border-slate-200"}`}
              >
                <div className="flex items-center gap-2 text-xs text-slate-500 mb-1">
                  <span className="font-semibold text-slate-700">{t(`categories.full.${e.category}`)}</span>
                  <span>· {new Date(e.occurredAt).toLocaleString()}</span>
                  {e.subjectApartment && <span>· 📍 {e.subjectApartment}</span>}
                  {e.hasAttachment && <span>· 📎</span>}
                  {e.edited && <span className="text-amber-600">· {t("common.edited")}</span>}
                  {e.archived && <span className="text-rose-600">· {t("common.archived")}</span>}
                </div>
                <p className="text-sm text-slate-700">{e.snippet}</p>
                <p className="text-xs text-slate-400 mt-1">{e.reporterName}</p>
              </Link>
            </li>
          ))}
        </ul>
      ) : (
        <div className="text-center text-slate-500 py-16">
          <p>{t("timeline.empty")}</p>
          <Link to="/entries/new" className="text-slate-700 underline text-sm">{t("timeline.reportFirst")}</Link>
        </div>
      )}
    </div>
  );
}
