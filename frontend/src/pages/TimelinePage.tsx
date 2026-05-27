import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAuth } from "@/auth/AuthContext";
import { CATEGORIES, CATEGORY_LABELS, type Category, type EntryListItem } from "@/api/types";

export default function TimelinePage() {
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
    return (
      <p className="text-slate-500">
        Join a building first on the <Link to="/building" className="underline">Building</Link> page.
      </p>
    );
  }

  return (
    <div className="max-w-3xl">
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-xl font-semibold text-slate-800">Timeline</h1>
        <Link to="/entries/new" className="bg-slate-900 text-white rounded-lg px-4 py-2 text-sm font-medium hover:bg-slate-700">
          New entry
        </Link>
      </div>

      <div className="bg-white border border-slate-200 rounded-xl p-3 mb-4 flex flex-wrap gap-2 items-end">
        <label className="flex flex-col text-xs text-slate-500">
          Category
          <select value={category} onChange={(e) => setCategory(e.target.value as Category | "")} className="border border-slate-300 rounded-lg px-2 py-1.5 text-sm">
            <option value="">All</option>
            {CATEGORIES.map((c) => (
              <option key={c} value={c}>{CATEGORY_LABELS[c]}</option>
            ))}
          </select>
        </label>
        <label className="flex flex-col text-xs text-slate-500">
          From
          <input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="border border-slate-300 rounded-lg px-2 py-1.5 text-sm" />
        </label>
        <label className="flex flex-col text-xs text-slate-500">
          To
          <input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="border border-slate-300 rounded-lg px-2 py-1.5 text-sm" />
        </label>
        <label className="flex flex-col text-xs text-slate-500 flex-1 min-w-40">
          Search
          <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="keyword" className="border border-slate-300 rounded-lg px-2 py-1.5 text-sm" />
        </label>
        {isBoard && (
          <label className="flex items-center gap-1.5 text-xs text-slate-500 pb-1.5">
            <input type="checkbox" checked={includeArchived} onChange={(e) => setIncludeArchived(e.target.checked)} />
            Include archived
          </label>
        )}
      </div>

      {entriesQ.isLoading ? (
        <p className="text-slate-500">Loading…</p>
      ) : entriesQ.data && entriesQ.data.length > 0 ? (
        <ul className="space-y-2">
          {entriesQ.data.map((e) => (
            <li key={e.id}>
              <Link
                to={`/entries/${e.id}`}
                className={`block bg-white border rounded-xl p-3 hover:border-slate-400 ${e.archived ? "border-dashed border-slate-300 opacity-70" : "border-slate-200"}`}
              >
                <div className="flex items-center gap-2 text-xs text-slate-500 mb-1">
                  <span className="font-semibold text-slate-700">{CATEGORY_LABELS[e.category]}</span>
                  <span>· {new Date(e.occurredAt).toLocaleString()}</span>
                  {e.subjectApartment && <span>· Apt {e.subjectApartment}</span>}
                  {e.hasAttachment && <span>· 📎</span>}
                  {e.edited && <span className="text-amber-600">· edited</span>}
                  {e.archived && <span className="text-rose-600">· archived</span>}
                </div>
                <p className="text-sm text-slate-700">{e.snippet}</p>
                <p className="text-xs text-slate-400 mt-1">{e.reporterName}</p>
              </Link>
            </li>
          ))}
        </ul>
      ) : (
        <div className="text-center text-slate-500 py-16">
          <p>Nothing reported yet.</p>
          <Link to="/entries/new" className="text-slate-700 underline text-sm">Report something</Link>
        </div>
      )}

      {/* Always-visible report action on phones */}
      <Link
        to="/entries/new"
        className="md:hidden fixed bottom-20 right-4 z-20 inline-flex items-center gap-2 bg-slate-900 text-white rounded-full pl-4 pr-5 py-3 shadow-lg active:scale-95 transition"
      >
        <span className="text-xl leading-none">＋</span>
        <span className="font-semibold">Report</span>
      </Link>
    </div>
  );
}
