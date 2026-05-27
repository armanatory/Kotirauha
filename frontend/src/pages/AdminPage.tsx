import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
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
  const { t } = useTranslation();
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

  if (!user?.isAdmin) return <p className="text-slate-500">{t("admin.adminRequired")}</p>;

  const o = overviewQ.data;

  return (
    <div className="max-w-4xl space-y-6">
      <h1 className="text-xl font-semibold text-slate-800">{t("admin.title")}</h1>

      <section className="grid grid-cols-2 md:grid-cols-5 gap-3">
        <Stat label={t("admin.users")} value={o?.users} />
        <Stat label={t("admin.buildings")} value={o?.buildings} />
        <Stat label={t("admin.entries")} value={o?.entries} />
        <Stat label={t("admin.archived")} value={o?.archivedEntries} />
        <Stat label={t("admin.translations")} value={o?.translations} />
      </section>

      {statusQ.data && (
        <section
          className={`border rounded-xl p-4 ${statusQ.data.isStub ? "border-amber-300 bg-amber-50" : "border-emerald-300 bg-emerald-50"}`}
        >
          <h2 className="text-sm font-semibold text-slate-700 mb-1">{t("admin.provider")}</h2>
          <p className="text-sm text-slate-700">
            {t("admin.activeProvider")}: <span className="font-mono font-medium">{statusQ.data.provider}</span>
            {statusQ.data.isStub && <span className="ml-2 text-amber-700">{t("admin.offlineStub")}</span>}
          </p>
          <p className="text-xs text-slate-500 mt-1">{statusQ.data.note}</p>
        </section>
      )}

      <section className="bg-white border border-slate-200 rounded-xl p-4">
        <h2 className="text-sm font-semibold text-slate-700 mb-3">{t("admin.buildingsTitle")}</h2>
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-slate-400 text-xs">
              <th className="py-1">{t("admin.colName")}</th>
              <th>{t("admin.colSharedLang")}</th>
              <th>{t("admin.colMembers")}</th>
              <th>{t("admin.colEntries")}</th>
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
      <p className="text-2xl font-semibold text-slate-800">{value ?? "-"}</p>
      <p className="text-xs text-slate-500">{label}</p>
    </div>
  );
}
