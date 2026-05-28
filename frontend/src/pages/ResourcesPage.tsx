import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { api } from "@/lib/api";
import { useAuth } from "@/auth/AuthContext";

interface ResourceLink {
  id: string;
  title: string;
  description: string | null;
  url: string;
  sortOrder: number;
  buildingId: string | null;
}
interface LinkForm { title: string; url: string; description: string }
const empty: LinkForm = { title: "", url: "", description: "" };

export default function ResourcesPage() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const qc = useQueryClient();
  const isBoard = user?.membership?.role === "board" || user?.membership?.role === "admin";
  const myBuildingId = user?.membership?.buildingId ?? null;

  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<LinkForm>(empty);
  const [adding, setAdding] = useState<LinkForm>(empty);

  const q = useQuery({
    queryKey: ["resources"],
    queryFn: async () => (await api.get<ResourceLink[]>("/resources")).data,
  });

  const invalidate = () => qc.invalidateQueries({ queryKey: ["resources"] });

  const create = useMutation({
    mutationFn: async (body: LinkForm) => (await api.post("/buildings/resources", body)).data,
    onSuccess: () => { toast.success(t("resources.added")); setAdding(empty); void invalidate(); },
    onError: () => toast.error(t("resources.failed")),
  });
  const update = useMutation({
    mutationFn: async ({ id, body }: { id: string; body: LinkForm }) => (await api.put(`/buildings/resources/${id}`, body)).data,
    onSuccess: () => { toast.success(t("resources.updated")); setEditingId(null); void invalidate(); },
    onError: () => toast.error(t("resources.failed")),
  });
  const remove = useMutation({
    mutationFn: async (id: string) => (await api.delete(`/buildings/resources/${id}`)).data,
    onSuccess: () => { toast.success(t("resources.removed")); void invalidate(); },
    onError: () => toast.error(t("resources.failed")),
  });

  const links = q.data ?? [];

  return (
    <div className="max-w-2xl space-y-4">
      <div>
        <h1 className="text-xl font-semibold text-slate-800">{t("resources.title")}</h1>
        <p className="text-sm text-slate-500 mt-1">{t("resources.intro")}</p>
      </div>

      {q.isLoading ? (
        <p className="text-slate-500">{t("common.loading")}</p>
      ) : links.length === 0 ? (
        <p className="text-slate-500">{t("resources.empty")}</p>
      ) : (
        <ul className="space-y-2">
          {links.map((r) =>
            editingId === r.id ? (
              <li key={r.id}>
                <LinkFields
                  value={form}
                  onChange={setForm}
                  onSubmit={() => update.mutate({ id: r.id, body: form })}
                  onCancel={() => setEditingId(null)}
                  submitLabel={t("resources.save")}
                  cancelLabel={t("resources.cancel")}
                  t={t}
                />
              </li>
            ) : (
              <li key={r.id} className="bg-white border border-slate-200 rounded-xl p-4">
                <div className="flex items-start justify-between gap-3">
                  <a href={r.url} target="_blank" rel="noopener noreferrer" className="min-w-0 group">
                    <span className="font-medium text-slate-800 group-hover:underline">{r.title}</span>
                    {r.description && <p className="text-sm text-slate-500 mt-0.5">{r.description}</p>}
                    <p className="text-xs text-slate-400 mt-1 break-all">{r.url}</p>
                  </a>
                  <div className="shrink-0 text-right">
                    {r.buildingId === null && isBoard && (
                      <span className="text-[10px] px-1.5 py-0.5 rounded bg-slate-100 text-slate-500">{t("resources.platformBadge")}</span>
                    )}
                    {isBoard && r.buildingId === myBuildingId && r.buildingId !== null && (
                      <div className="flex gap-2 justify-end">
                        <button
                          onClick={() => { setEditingId(r.id); setForm({ title: r.title, url: r.url, description: r.description ?? "" }); }}
                          className="text-xs text-slate-600 underline"
                        >
                          {t("resources.edit")}
                        </button>
                        <button onClick={() => remove.mutate(r.id)} className="text-xs text-rose-600 underline">
                          {t("resources.remove")}
                        </button>
                      </div>
                    )}
                  </div>
                </div>
              </li>
            ),
          )}
        </ul>
      )}

      {isBoard && (
        <section className="bg-white border border-slate-200 rounded-xl p-4">
          <h2 className="text-sm font-semibold text-slate-700">{t("resources.manage")}</h2>
          <p className="text-xs text-slate-400 mt-0.5 mb-3">{t("resources.manageHint")}</p>
          <LinkFields
            value={adding}
            onChange={setAdding}
            onSubmit={() => { if (adding.title.trim() && adding.url.trim()) create.mutate(adding); }}
            submitLabel={t("resources.add")}
            t={t}
          />
        </section>
      )}
    </div>
  );
}

function LinkFields({
  value, onChange, onSubmit, onCancel, submitLabel, cancelLabel, t,
}: {
  value: LinkForm;
  onChange: (v: LinkForm) => void;
  onSubmit: () => void;
  onCancel?: () => void;
  submitLabel: string;
  cancelLabel?: string;
  t: (k: string) => string;
}) {
  return (
    <form onSubmit={(e) => { e.preventDefault(); onSubmit(); }} className="grid gap-2 bg-white border border-slate-200 rounded-xl p-3 sm:grid-cols-2">
      <input value={value.title} onChange={(e) => onChange({ ...value, title: e.target.value })} placeholder={t("resources.fieldTitle")}
        className="border border-slate-300 rounded-lg px-3 py-2 text-sm" />
      <input value={value.url} onChange={(e) => onChange({ ...value, url: e.target.value })} placeholder={t("resources.fieldUrl")}
        className="border border-slate-300 rounded-lg px-3 py-2 text-sm" />
      <input value={value.description} onChange={(e) => onChange({ ...value, description: e.target.value })} placeholder={t("resources.fieldDesc")}
        className="border border-slate-300 rounded-lg px-3 py-2 text-sm sm:col-span-2" />
      <div className="flex gap-2 sm:col-span-2">
        <button type="submit" disabled={!value.title.trim() || !value.url.trim()}
          className="bg-teal-700 text-white rounded-lg px-4 py-2 text-sm font-medium hover:bg-teal-800 disabled:opacity-50">
          {submitLabel}
        </button>
        {onCancel && <button type="button" onClick={onCancel} className="text-sm text-slate-500">{cancelLabel}</button>}
      </div>
    </form>
  );
}
