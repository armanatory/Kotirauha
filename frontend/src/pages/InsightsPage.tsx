import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAuth } from "@/auth/AuthContext";
import { CATEGORY_LABELS, type Category } from "@/api/types";

interface Insights {
  totalEntries: number;
  byCategory: { category: Category; count: number }[];
  bySubjectApartment: { apartment: string; count: number }[];
  byMonth: { month: string; count: number }[];
  topRecurring: {
    category: Category;
    subjectApartment: string;
    count: number;
    firstAt: string;
    lastAt: string;
  }[];
}

export default function InsightsPage() {
  const { user } = useAuth();
  const isBoard = user?.membership?.role === "board" || user?.membership?.role === "admin";

  const q = useQuery({
    queryKey: ["insights"],
    queryFn: async () => (await api.get<Insights>("/insights")).data,
    enabled: isBoard,
  });

  if (!isBoard) return <p className="text-slate-500">Board access required.</p>;
  if (q.isLoading) return <p className="text-slate-500">Loading…</p>;
  if (!q.data) return <p className="text-slate-500">No data.</p>;
  const d = q.data;
  const maxCat = Math.max(1, ...d.byCategory.map((c) => c.count));
  const maxMonth = Math.max(1, ...d.byMonth.map((c) => c.count));

  return (
    <div className="max-w-3xl space-y-6">
      <div>
        <h1 className="text-xl font-semibold text-slate-800">Insights</h1>
        <p className="text-sm text-slate-500">
          Frequency and distribution of reported observations. This is a factual
          overview, not a judgment of any resident.
        </p>
      </div>

      <section className="bg-white border border-slate-200 rounded-xl p-4">
        <p className="text-3xl font-semibold text-slate-800">{d.totalEntries}</p>
        <p className="text-sm text-slate-500">total entries</p>
      </section>

      <section className="bg-white border border-slate-200 rounded-xl p-4">
        <h2 className="text-sm font-semibold text-slate-700 mb-3">By category</h2>
        <div className="space-y-2">
          {d.byCategory.map((c) => (
            <div key={c.category} className="flex items-center gap-2 text-sm">
              <span className="w-40 text-slate-600">{CATEGORY_LABELS[c.category]}</span>
              <div className="flex-1 bg-slate-100 rounded h-4">
                <div className="bg-slate-700 h-4 rounded" style={{ width: `${(c.count / maxCat) * 100}%` }} />
              </div>
              <span className="w-8 text-right text-slate-500">{c.count}</span>
            </div>
          ))}
        </div>
      </section>

      <section className="bg-white border border-slate-200 rounded-xl p-4">
        <h2 className="text-sm font-semibold text-slate-700 mb-3">By month</h2>
        <div className="flex items-end gap-2 h-28">
          {d.byMonth.map((m) => (
            <div key={m.month} className="flex flex-col items-center gap-1 flex-1">
              <div className="w-full bg-slate-700 rounded-t" style={{ height: `${(m.count / maxMonth) * 100}%` }} />
              <span className="text-[10px] text-slate-400">{m.month}</span>
            </div>
          ))}
          {d.byMonth.length === 0 && <p className="text-sm text-slate-400">No data.</p>}
        </div>
      </section>

      <section className="bg-white border border-slate-200 rounded-xl p-4">
        <h2 className="text-sm font-semibold text-slate-700 mb-3">Recurring patterns</h2>
        {d.topRecurring.length === 0 ? (
          <p className="text-sm text-slate-400">No recurring patterns (no apartment with 2+ entries of the same category).</p>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="text-left text-slate-400 text-xs">
                <th className="py-1">Category</th>
                <th>Apartment</th>
                <th>Count</th>
                <th>First</th>
                <th>Last</th>
              </tr>
            </thead>
            <tbody>
              {d.topRecurring.map((p, i) => (
                <tr key={i} className="border-t border-slate-100">
                  <td className="py-1.5 text-slate-700">{CATEGORY_LABELS[p.category]}</td>
                  <td className="text-slate-700">{p.subjectApartment}</td>
                  <td className="text-slate-700 font-medium">{p.count}</td>
                  <td className="text-slate-500">{new Date(p.firstAt).toLocaleDateString()}</td>
                  <td className="text-slate-500">{new Date(p.lastAt).toLocaleDateString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </div>
  );
}
