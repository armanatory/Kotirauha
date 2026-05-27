import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { useAuth } from "@/auth/AuthContext";

interface Overview {
  users: number;
  buildings: number;
  entries: number;
  archivedEntries: number;
  translations: number;
}
interface AdminBuilding {
  id: string;
  name: string;
  sharedLanguage: string;
  members: number;
  entries: number;
}
interface TranslationStatus {
  provider: string;
  isStub: boolean;
  note: string;
}

export default function AdminPage() {
  const { user } = useAuth();

  const overviewQ = useQuery({
    queryKey: ["admin-overview"],
    queryFn: async () => (await api.get<Overview>("/admin/overview")).data,
    enabled: user?.isAdmin,
  });
  const buildingsQ = useQuery({
    queryKey: ["admin-buildings"],
    queryFn: async () => (await api.get<AdminBuilding[]>("/admin/buildings")).data,
    enabled: user?.isAdmin,
  });
  const statusQ = useQuery({
    queryKey: ["admin-translation-status"],
    queryFn: async () => (await api.get<TranslationStatus>("/admin/translation-status")).data,
    enabled: user?.isAdmin,
  });

  if (!user?.isAdmin) return <p className="text-slate-500">Platform admin access required.</p>;

  const o = overviewQ.data;

  return (
    <div className="max-w-4xl space-y-6">
      <h1 className="text-xl font-semibold text-slate-800">Operator console</h1>

      <section className="grid grid-cols-2 md:grid-cols-5 gap-3">
        <Stat label="Users" value={o?.users} />
        <Stat label="Buildings" value={o?.buildings} />
        <Stat label="Entries" value={o?.entries} />
        <Stat label="Archived" value={o?.archivedEntries} />
        <Stat label="Translations" value={o?.translations} />
      </section>

      {statusQ.data && (
        <section
          className={`border rounded-xl p-4 ${statusQ.data.isStub ? "border-amber-300 bg-amber-50" : "border-emerald-300 bg-emerald-50"}`}
        >
          <h2 className="text-sm font-semibold text-slate-700 mb-1">Translation provider</h2>
          <p className="text-sm text-slate-700">
            Active provider: <span className="font-mono font-medium">{statusQ.data.provider}</span>
            {statusQ.data.isStub && <span className="ml-2 text-amber-700">(offline stub)</span>}
          </p>
          <p className="text-xs text-slate-500 mt-1">{statusQ.data.note}</p>
        </section>
      )}

      <section className="bg-white border border-slate-200 rounded-xl p-4">
        <h2 className="text-sm font-semibold text-slate-700 mb-3">Buildings</h2>
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-slate-400 text-xs">
              <th className="py-1">Name</th>
              <th>Shared language</th>
              <th>Members</th>
              <th>Entries</th>
            </tr>
          </thead>
          <tbody>
            {buildingsQ.data?.map((b) => (
              <tr key={b.id} className="border-t border-slate-100">
                <td className="py-1.5 text-slate-700">{b.name}</td>
                <td className="text-slate-500">{b.sharedLanguage}</td>
                <td className="text-slate-700">{b.members}</td>
                <td className="text-slate-700">{b.entries}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </div>
  );
}

function Stat({ label, value }: { label: string; value: number | undefined }) {
  return (
    <div className="bg-white border border-slate-200 rounded-xl p-4">
      <p className="text-2xl font-semibold text-slate-800">{value ?? "—"}</p>
      <p className="text-xs text-slate-500">{label}</p>
    </div>
  );
}
