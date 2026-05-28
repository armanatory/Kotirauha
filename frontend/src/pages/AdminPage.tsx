import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
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
interface AdminUser {
  id: string;
  email: string;
  displayName: string;
  isAdmin: boolean;
  buildingId: string | null;
  buildingName: string | null;
  role: string | null;
}
interface ResourceLink {
  id: string;
  title: string;
  description: string | null;
  url: string;
  sortOrder: number;
}
interface TranslationStatus {
  provider: string;
  isStub: boolean;
  note: string;
}
interface CountRow {
  label: string;
  count: number;
}
interface Analytics {
  totalVisits: number;
  uniqueVisitors: number;
  days: number;
  geoEnabled: boolean;
  byDay: { date: string; count: number }[];
  topPages: CountRow[];
  topReferrers: CountRow[];
  byLanguage: CountRow[];
  byCountry: CountRow[];
}

const COUNTRY_NAMES =
  typeof Intl !== "undefined" && "DisplayNames" in Intl
    ? new Intl.DisplayNames(["en"], { type: "region" })
    : null;
function countryName(code: string) {
  try {
    return COUNTRY_NAMES?.of(code) ?? code;
  } catch {
    return code;
  }
}

export default function AdminPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const qc = useQueryClient();

  const [analyticsDays, setAnalyticsDays] = useState(30);
  const analyticsQ = useQuery({
    queryKey: ["admin-analytics", analyticsDays],
    queryFn: async () => (await api.get<Analytics>("/admin/analytics", { params: { days: analyticsDays } })).data,
    enabled: user?.isAdmin,
  });

  const overviewQ = useQuery({ queryKey: ["admin-overview"], queryFn: async () => (await api.get<Overview>("/admin/overview")).data, enabled: user?.isAdmin });
  const buildingsQ = useQuery({ queryKey: ["admin-buildings"], queryFn: async () => (await api.get<AdminBuilding[]>("/admin/buildings")).data, enabled: user?.isAdmin });
  const usersQ = useQuery({ queryKey: ["admin-users"], queryFn: async () => (await api.get<AdminUser[]>("/admin/users")).data, enabled: user?.isAdmin });
  const statusQ = useQuery({ queryKey: ["admin-translation-status"], queryFn: async () => (await api.get<TranslationStatus>("/admin/translation-status")).data, enabled: user?.isAdmin });
  const resourcesQ = useQuery({ queryKey: ["admin-resources"], queryFn: async () => (await api.get<ResourceLink[]>("/admin/resources")).data, enabled: user?.isAdmin });

  const [pending, setPending] = useState<Record<string, { buildingId: string; role: string }>>({});
  const assign = useMutation({
    mutationFn: async (body: { userId: string; buildingId: string | null; role: string }) => (await api.post("/admin/assign", body)).data,
    onSuccess: () => {
      toast.success(t("admin.assigned"));
      void qc.invalidateQueries({ queryKey: ["admin-users"] });
      void qc.invalidateQueries({ queryKey: ["admin-buildings"] });
    },
    onError: () => toast.error(t("admin.assignFailed")),
  });

  const removeUser = useMutation({
    mutationFn: async (id: string) => (await api.delete(`/admin/users/${id}`)).data,
    onSuccess: () => {
      toast.success(t("admin.userDeleted"));
      void qc.invalidateQueries({ queryKey: ["admin-users"] });
      void qc.invalidateQueries({ queryKey: ["admin-overview"] });
    },
    onError: (err: unknown) => {
      const status = (err as { response?: { status?: number } })?.response?.status;
      toast.error(status === 409 ? t("admin.deleteHasEntries") : t("admin.deleteFailed"));
    },
  });

  const [newBuilding, setNewBuilding] = useState({ name: "", sharedLanguage: "fi", address: "" });
  const createBuilding = useMutation({
    mutationFn: async () => (await api.post("/admin/buildings", newBuilding)).data,
    onSuccess: () => {
      toast.success(t("admin.buildingCreated"));
      setNewBuilding({ name: "", sharedLanguage: "fi", address: "" });
      void qc.invalidateQueries({ queryKey: ["admin-buildings"] });
      void qc.invalidateQueries({ queryKey: ["admin-overview"] });
    },
    onError: () => toast.error(t("admin.buildingCreateFailed")),
  });

  const [link, setLink] = useState({ title: "", description: "", url: "" });
  const addLink = useMutation({
    mutationFn: async () => (await api.post("/admin/resources", link)).data,
    onSuccess: () => {
      toast.success(t("admin.linkAdded"));
      setLink({ title: "", description: "", url: "" });
      void qc.invalidateQueries({ queryKey: ["admin-resources"] });
    },
    onError: () => toast.error(t("admin.linkFailed")),
  });
  const removeLink = useMutation({
    mutationFn: async (id: string) => (await api.delete(`/admin/resources/${id}`)).data,
    onSuccess: () => {
      toast.success(t("admin.linkRemoved"));
      void qc.invalidateQueries({ queryKey: ["admin-resources"] });
    },
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

      {/* Product analytics */}
      <section className="bg-white border border-slate-200 rounded-xl p-4">
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-sm font-semibold text-slate-700">{t("admin.analyticsTitle")}</h2>
          <div className="flex gap-1">
            {[7, 30, 90].map((d) => (
              <button
                key={d}
                onClick={() => setAnalyticsDays(d)}
                className={`px-2.5 py-1 rounded-lg text-xs ${analyticsDays === d ? "bg-teal-700 text-white" : "bg-slate-100 text-slate-600"}`}
              >
                {t("admin.lastDays", { days: d })}
              </button>
            ))}
          </div>
        </div>

        {analyticsQ.data ? (
          <>
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-3 mb-4">
              <Stat label={t("admin.totalVisits")} value={analyticsQ.data.totalVisits} />
              <Stat label={t("admin.uniqueVisitors")} value={analyticsQ.data.uniqueVisitors} />
              <Stat label={t("admin.activeDays")} value={analyticsQ.data.byDay.length} />
            </div>

            {analyticsQ.data.byDay.length > 0 && (
              <div className="mb-4">
                <p className="text-xs text-slate-400 mb-1">{t("admin.visitsPerDay")}</p>
                <DayBars data={analyticsQ.data.byDay} />
              </div>
            )}

            <div className="grid gap-4 sm:grid-cols-2">
              <RankList title={t("admin.topPages")} rows={analyticsQ.data.topPages} empty={t("admin.noData")} />
              <RankList title={t("admin.topReferrers")} rows={analyticsQ.data.topReferrers} empty={t("admin.noReferrers")} />
              <RankList title={t("admin.byLanguage")} rows={analyticsQ.data.byLanguage} empty={t("admin.noData")} />
              <RankList
                title={t("admin.byCountry")}
                rows={analyticsQ.data.byCountry.map((r) => ({ label: countryName(r.label), count: r.count }))}
                empty={analyticsQ.data.geoEnabled ? t("admin.noData") : t("admin.geoDisabled")}
              />
            </div>
          </>
        ) : (
          <p className="text-sm text-slate-400">{t("admin.noData")}</p>
        )}
      </section>

      {statusQ.data && (
        <section className={`border rounded-xl p-4 ${statusQ.data.isStub ? "border-amber-300 bg-amber-50" : "border-emerald-300 bg-emerald-50"}`}>
          <h2 className="text-sm font-semibold text-slate-700 mb-1">{t("admin.provider")}</h2>
          <p className="text-sm text-slate-700">
            {t("admin.activeProvider")}: <span className="font-mono font-medium">{statusQ.data.provider}</span>
            {statusQ.data.isStub && <span className="ml-2 text-amber-700">{t("admin.offlineStub")}</span>}
          </p>
          <p className="text-xs text-slate-500 mt-1">{statusQ.data.note}</p>
        </section>
      )}

      {/* Users + assign to building (card list, wraps on phones) */}
      <section className="bg-white border border-slate-200 rounded-xl p-4">
        <h2 className="text-sm font-semibold text-slate-700 mb-3">{t("admin.usersTitle")}</h2>
        <ul className="divide-y divide-slate-100">
          {usersQ.data?.map((u) => {
            const cur = pending[u.id] ?? { buildingId: u.buildingId ?? "", role: u.role ?? "resident" };
            const set = (patch: Partial<typeof cur>) => setPending((p) => ({ ...p, [u.id]: { ...cur, ...patch } }));
            return (
              <li key={u.id} className="py-3">
                <div className="flex items-center gap-2 mb-2 text-sm">
                  <span className="font-medium text-slate-700 break-all">{u.email}</span>
                  {u.isAdmin && <span className="text-[10px] px-1.5 py-0.5 rounded bg-teal-700 text-white shrink-0">{t("admin.adminBadge")}</span>}
                </div>
                <div className="flex flex-wrap items-center gap-2">
                  <select
                    value={cur.buildingId}
                    onChange={(e) => set({ buildingId: e.target.value })}
                    className="border border-slate-300 rounded-lg px-2 py-1.5 text-sm flex-1 min-w-40"
                  >
                    <option value="">{t("admin.noBuilding")}</option>
                    {buildingsQ.data?.map((b) => (
                      <option key={b.id} value={b.id}>{b.name}</option>
                    ))}
                  </select>
                  <select
                    value={cur.role}
                    onChange={(e) => set({ role: e.target.value })}
                    disabled={!cur.buildingId}
                    className="border border-slate-300 rounded-lg px-2 py-1.5 text-sm disabled:opacity-50"
                  >
                    <option value="resident">{t("roles.resident")}</option>
                    <option value="board">{t("roles.board")}</option>
                  </select>
                  <button
                    onClick={() => assign.mutate({ userId: u.id, buildingId: cur.buildingId || null, role: cur.role })}
                    className="bg-teal-700 text-white rounded-lg px-4 py-1.5 text-sm font-medium hover:bg-teal-800"
                  >
                    {t("admin.assign")}
                  </button>
                  {u.id !== user.id && (
                    <button
                      onClick={() => { if (confirm(t("admin.deleteConfirm", { email: u.email }))) removeUser.mutate(u.id); }}
                      className="text-sm text-rose-600 underline ml-auto"
                    >
                      {t("admin.deleteUser")}
                    </button>
                  )}
                </div>
              </li>
            );
          })}
        </ul>
      </section>

      {/* Buildings */}
      <section className="bg-white border border-slate-200 rounded-xl p-4 overflow-x-auto">
        <h2 className="text-sm font-semibold text-slate-700 mb-3">{t("admin.buildingsTitle")}</h2>

        <form
          onSubmit={(e) => { e.preventDefault(); if (newBuilding.name.trim()) createBuilding.mutate(); }}
          className="grid gap-2 sm:grid-cols-4 mb-4"
        >
          <input
            value={newBuilding.name}
            onChange={(e) => setNewBuilding({ ...newBuilding, name: e.target.value })}
            placeholder={t("admin.newBuildingName")}
            className="border border-slate-300 rounded-lg px-3 py-2 text-sm sm:col-span-2"
          />
          <select
            value={newBuilding.sharedLanguage}
            onChange={(e) => setNewBuilding({ ...newBuilding, sharedLanguage: e.target.value })}
            className="border border-slate-300 rounded-lg px-3 py-2 text-sm"
          >
            <option value="fi">{t("languages.fi")}</option>
            <option value="en">{t("languages.en")}</option>
          </select>
          <button
            type="submit"
            disabled={createBuilding.isPending || !newBuilding.name.trim()}
            className="bg-teal-700 text-white rounded-lg px-4 py-2 text-sm font-medium hover:bg-teal-800 disabled:opacity-50"
          >
            {t("admin.createBuilding")}
          </button>
        </form>

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
                <td className="text-slate-500">{t(`languages.${b.sharedLanguage}`, b.sharedLanguage)}</td>
                <td className="text-slate-700">{b.members}</td>
                <td className="text-slate-700">{b.entries}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {/* Good-to-know links */}
      <section className="bg-white border border-slate-200 rounded-xl p-4">
        <h2 className="text-sm font-semibold text-slate-700 mb-1">{t("admin.resourcesTitle")}</h2>
        <p className="text-xs text-slate-400 mb-3">{t("admin.resourcesHint")}</p>

        <ul className="divide-y divide-slate-100 mb-4">
          {resourcesQ.data?.map((r) => (
            <li key={r.id} className="py-2 flex items-start justify-between gap-3 text-sm">
              <div>
                <p className="text-slate-700">{r.title}</p>
                <a href={r.url} target="_blank" rel="noopener noreferrer" className="text-xs text-slate-400 underline break-all">{r.url}</a>
              </div>
              <button onClick={() => removeLink.mutate(r.id)} className="text-xs text-rose-600 underline shrink-0">
                {t("admin.remove")}
              </button>
            </li>
          ))}
        </ul>

        <form
          onSubmit={(e) => { e.preventDefault(); if (link.title.trim() && link.url.trim()) addLink.mutate(); }}
          className="grid gap-2 sm:grid-cols-2"
        >
          <input
            value={link.title}
            onChange={(e) => setLink({ ...link, title: e.target.value })}
            placeholder={t("admin.linkTitle")}
            className="border border-slate-300 rounded-lg px-3 py-2 text-sm"
          />
          <input
            value={link.url}
            onChange={(e) => setLink({ ...link, url: e.target.value })}
            placeholder={t("admin.linkUrl")}
            className="border border-slate-300 rounded-lg px-3 py-2 text-sm"
          />
          <input
            value={link.description}
            onChange={(e) => setLink({ ...link, description: e.target.value })}
            placeholder={t("admin.linkDescription")}
            className="border border-slate-300 rounded-lg px-3 py-2 text-sm sm:col-span-2"
          />
          <button
            type="submit"
            disabled={addLink.isPending || !link.title.trim() || !link.url.trim()}
            className="bg-teal-700 text-white rounded-lg px-4 py-2 text-sm font-medium hover:bg-teal-800 disabled:opacity-50 sm:col-span-2 sm:justify-self-start"
          >
            {t("admin.addLink")}
          </button>
        </form>
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

function DayBars({ data }: { data: { date: string; count: number }[] }) {
  const max = Math.max(1, ...data.map((d) => d.count));
  return (
    <div className="flex items-end gap-0.5 h-24">
      {data.map((d) => (
        <div key={d.date} className="flex-1 group relative flex items-end" title={`${d.date}: ${d.count}`}>
          <div
            className="w-full bg-teal-600/80 rounded-t hover:bg-teal-700"
            style={{ height: `${Math.max(3, (d.count / max) * 100)}%` }}
          />
        </div>
      ))}
    </div>
  );
}

function RankList({ title, rows, empty }: { title: string; rows: CountRow[]; empty: string }) {
  const max = Math.max(1, ...rows.map((r) => r.count));
  return (
    <div className="border border-slate-100 rounded-lg p-3">
      <p className="text-xs font-semibold text-slate-600 mb-2">{title}</p>
      {rows.length === 0 ? (
        <p className="text-xs text-slate-400">{empty}</p>
      ) : (
        <ul className="space-y-1.5">
          {rows.map((r) => (
            <li key={r.label} className="text-xs">
              <div className="flex justify-between gap-2 mb-0.5">
                <span className="text-slate-600 truncate" title={r.label}>{r.label}</span>
                <span className="text-slate-400 shrink-0">{r.count}</span>
              </div>
              <div className="h-1.5 bg-slate-100 rounded-full overflow-hidden">
                <div className="h-full bg-teal-500 rounded-full" style={{ width: `${(r.count / max) * 100}%` }} />
              </div>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
