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

export default function AdminPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const qc = useQueryClient();

  const overviewQ = useQuery({ queryKey: ["admin-overview"], queryFn: async () => (await api.get<Overview>("/admin/overview")).data, enabled: user?.isAdmin });
  const buildingsQ = useQuery({ queryKey: ["admin-buildings"], queryFn: async () => (await api.get<AdminBuilding[]>("/admin/buildings")).data, enabled: user?.isAdmin });
  const usersQ = useQuery({ queryKey: ["admin-users"], queryFn: async () => (await api.get<AdminUser[]>("/admin/users")).data, enabled: user?.isAdmin });
  const statusQ = useQuery({ queryKey: ["admin-translation-status"], queryFn: async () => (await api.get<TranslationStatus>("/admin/translation-status")).data, enabled: user?.isAdmin });
  const resourcesQ = useQuery({ queryKey: ["resources"], queryFn: async () => (await api.get<ResourceLink[]>("/resources")).data, enabled: user?.isAdmin });

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

  const [link, setLink] = useState({ title: "", description: "", url: "" });
  const addLink = useMutation({
    mutationFn: async () => (await api.post("/admin/resources", link)).data,
    onSuccess: () => {
      toast.success(t("admin.linkAdded"));
      setLink({ title: "", description: "", url: "" });
      void qc.invalidateQueries({ queryKey: ["resources"] });
    },
    onError: () => toast.error(t("admin.linkFailed")),
  });
  const removeLink = useMutation({
    mutationFn: async (id: string) => (await api.delete(`/admin/resources/${id}`)).data,
    onSuccess: () => {
      toast.success(t("admin.linkRemoved"));
      void qc.invalidateQueries({ queryKey: ["resources"] });
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

      {/* Users + assign to building */}
      <section className="bg-white border border-slate-200 rounded-xl p-4 overflow-x-auto">
        <h2 className="text-sm font-semibold text-slate-700 mb-3">{t("admin.usersTitle")}</h2>
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-slate-400 text-xs">
              <th className="py-1">{t("admin.colEmail")}</th>
              <th>{t("admin.colBuilding")}</th>
              <th>{t("admin.colRole")}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {usersQ.data?.map((u) => {
              const cur = pending[u.id] ?? { buildingId: u.buildingId ?? "", role: u.role ?? "resident" };
              const set = (patch: Partial<typeof cur>) => setPending((p) => ({ ...p, [u.id]: { ...cur, ...patch } }));
              return (
                <tr key={u.id} className="border-t border-slate-100 align-middle">
                  <td className="py-2 text-slate-700">
                    {u.email}{u.isAdmin && <span className="ml-1 text-[10px] px-1.5 py-0.5 rounded bg-slate-900 text-white">{t("admin.adminBadge")}</span>}
                  </td>
                  <td>
                    <select
                      value={cur.buildingId}
                      onChange={(e) => set({ buildingId: e.target.value })}
                      className="border border-slate-300 rounded-lg px-2 py-1 text-sm"
                    >
                      <option value="">{t("admin.noBuilding")}</option>
                      {buildingsQ.data?.map((b) => (
                        <option key={b.id} value={b.id}>{b.name}</option>
                      ))}
                    </select>
                  </td>
                  <td>
                    <select
                      value={cur.role}
                      onChange={(e) => set({ role: e.target.value })}
                      disabled={!cur.buildingId}
                      className="border border-slate-300 rounded-lg px-2 py-1 text-sm disabled:opacity-50"
                    >
                      <option value="resident">{t("roles.resident")}</option>
                      <option value="board">{t("roles.board")}</option>
                    </select>
                  </td>
                  <td className="text-right">
                    <button
                      onClick={() => assign.mutate({ userId: u.id, buildingId: cur.buildingId || null, role: cur.role })}
                      className="text-sm text-slate-700 underline hover:text-slate-900"
                    >
                      {t("admin.assign")}
                    </button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </section>

      {/* Buildings */}
      <section className="bg-white border border-slate-200 rounded-xl p-4 overflow-x-auto">
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
            className="bg-slate-900 text-white rounded-lg px-4 py-2 text-sm font-medium hover:bg-slate-700 disabled:opacity-50 sm:col-span-2 sm:justify-self-start"
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
