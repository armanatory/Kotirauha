import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { api } from "@/lib/api";
import { useAuth } from "@/auth/AuthContext";
import type { BuildingDto, MemberDto, InviteDto } from "@/api/types";

interface BrowseBuilding { id: string; name: string; address: string | null; requested: boolean }
interface JoinRequest { id: string; requesterName: string; requesterEmail: string; apartmentNumber: string | null; createdAt: string }
interface MyJoinRequest { buildingId: string; buildingName: string }

export default function BuildingPage() {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const { refresh } = useAuth();

  const buildingQ = useQuery({
    queryKey: ["building"],
    queryFn: async () => {
      const res = await api.get<BuildingDto>("/buildings/me", { validateStatus: (s) => s < 500 });
      return res.status === 204 ? null : res.data;
    },
  });

  const requestQ = useQuery({
    queryKey: ["my-join-request"],
    queryFn: async () => {
      const res = await api.get<MyJoinRequest>("/buildings/my-join-request", { validateStatus: (s) => s < 500 });
      return res.status === 204 ? null : res.data;
    },
    enabled: buildingQ.data === null,
  });

  const onChanged = () => {
    void qc.invalidateQueries({ queryKey: ["building"] });
    void qc.invalidateQueries({ queryKey: ["my-join-request"] });
    void refresh();
  };

  if (buildingQ.isLoading) return <p className="text-slate-500">{t("common.loading")}</p>;
  if (buildingQ.data) return <BuildingHome building={buildingQ.data} onChanged={onChanged} />;
  if (requestQ.data) return <PendingRequest request={requestQ.data} onChanged={onChanged} />;
  return <FindOrJoin onChanged={onChanged} />;
}

function PendingRequest({ request, onChanged }: { request: MyJoinRequest; onChanged: () => void }) {
  const { t } = useTranslation();
  const cancel = useMutation({
    mutationFn: async () => (await api.delete("/buildings/my-join-request")).data,
    onSuccess: () => {
      toast.success(t("building.requestCancelled"));
      onChanged();
    },
  });
  return (
    <div className="max-w-md mx-auto text-center py-10">
      <div className="bg-white border border-slate-200 rounded-2xl p-6">
        <div className="w-12 h-12 rounded-full bg-teal-600/10 text-teal-700 flex items-center justify-center mx-auto mb-3 text-xl">⏳</div>
        <h1 className="text-lg font-semibold text-slate-800">{t("building.pendingTitle")}</h1>
        <p className="text-sm text-slate-500 mt-2">{t("building.pendingBody", { building: request.buildingName })}</p>
        <button onClick={() => cancel.mutate()} disabled={cancel.isPending} className="mt-4 text-sm text-slate-500 underline">
          {t("building.cancelRequest")}
        </button>
      </div>
    </div>
  );
}

function BuildingHome({ building, onChanged }: { building: BuildingDto; onChanged: () => void }) {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const isBoard = building.role === "board" || building.role === "admin";
  const [customCode, setCustomCode] = useState("");

  const membersQ = useQuery({
    queryKey: ["members"],
    queryFn: async () => (await api.get<MemberDto[]>("/buildings/members")).data,
    enabled: isBoard,
  });

  const requestsQ = useQuery({
    queryKey: ["join-requests"],
    queryFn: async () => (await api.get<JoinRequest[]>("/buildings/join-requests")).data,
    enabled: isBoard,
  });

  const decide = useMutation({
    mutationFn: async ({ id, action }: { id: string; action: "approve" | "reject" }) =>
      (await api.post(`/buildings/join-requests/${id}/${action}`)).data,
    onSuccess: (_d, vars) => {
      toast.success(vars.action === "approve" ? t("building.approved") : t("building.rejected"));
      void qc.invalidateQueries({ queryKey: ["join-requests"] });
      void qc.invalidateQueries({ queryKey: ["members"] });
    },
  });

  const regen = useMutation({
    mutationFn: async () => (await api.post<{ joinCode: string }>("/buildings/join-code", {})).data,
    onSuccess: () => { toast.success(t("building.toastCodeRegenerated")); onChanged(); },
  });
  const setCode = useMutation({
    mutationFn: async (code: string) => (await api.post<{ joinCode: string }>("/buildings/join-code", { code })).data,
    onSuccess: () => { toast.success(t("building.toastCodeSet")); setCustomCode(""); onChanged(); },
    onError: (err: unknown) => {
      const status = (err as { response?: { status?: number } })?.response?.status;
      toast.error(status === 409 ? t("building.toastCodeTaken") : t("building.toastCodeInvalid"));
    },
  });

  return (
    <div className="max-w-2xl space-y-6">
      <div>
        <h1 className="text-xl font-semibold text-slate-800">{building.name}</h1>
        {building.address && <p className="text-slate-500 text-sm">{building.address}</p>}
        <p className="text-sm text-slate-500 mt-1">
          {t("building.sharedLanguage")}:{" "}
          <span className="font-medium text-slate-700">{t(`languages.${building.sharedLanguage}`, building.sharedLanguage)}</span> ·{" "}
          {t("building.yourRole")}: <span className="font-medium text-slate-700">{t(`roles.${building.role}`)}</span>
        </p>
      </div>

      {isBoard && (
        <section className="bg-white border border-slate-200 rounded-xl p-4">
          <h2 className="text-sm font-semibold text-slate-700 mb-2">
            {t("building.joinRequests")}
            {requestsQ.data && requestsQ.data.length > 0 && (
              <span className="ml-2 text-xs bg-teal-700 text-white rounded-full px-2 py-0.5">{requestsQ.data.length}</span>
            )}
          </h2>
          {requestsQ.data && requestsQ.data.length > 0 ? (
            <ul className="divide-y divide-slate-100">
              {requestsQ.data.map((r) => (
                <li key={r.id} className="py-2.5 flex flex-wrap items-center justify-between gap-2">
                  <div className="text-sm">
                    <span className="text-slate-700 font-medium">{r.requesterName || r.requesterEmail}</span>
                    <span className="text-slate-400">
                      {" "}· {r.requesterEmail}{r.apartmentNumber ? ` · ${t("building.aptShort", { num: r.apartmentNumber })}` : ""}
                    </span>
                  </div>
                  <div className="flex gap-2">
                    <button onClick={() => decide.mutate({ id: r.id, action: "approve" })} disabled={decide.isPending}
                      className="bg-teal-700 text-white rounded-lg px-3 py-1.5 text-sm font-medium hover:bg-teal-800 disabled:opacity-50">
                      {t("building.approve")}
                    </button>
                    <button onClick={() => decide.mutate({ id: r.id, action: "reject" })} disabled={decide.isPending}
                      className="text-sm text-rose-600 underline">
                      {t("building.reject")}
                    </button>
                  </div>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-sm text-slate-400">{t("building.noRequests")}</p>
          )}
        </section>
      )}

      {isBoard && (
        <section className="bg-white border border-slate-200 rounded-xl p-4">
          <h2 className="text-sm font-semibold text-slate-700 mb-2">{t("building.joinCode")}</h2>
          <div className="flex items-center gap-3">
            <code className="px-3 py-1.5 bg-slate-100 rounded-lg text-lg tracking-widest">{building.joinCode}</code>
            <button onClick={() => regen.mutate()} disabled={regen.isPending} className="text-sm text-slate-600 underline hover:text-slate-900">
              {t("building.regenerate")}
            </button>
          </div>
          <p className="text-xs text-slate-400 mt-2">{t("building.shareCode")}</p>
          <form
            onSubmit={(e) => { e.preventDefault(); if (customCode.trim()) setCode.mutate(customCode); }}
            className="mt-3 flex flex-col gap-1.5 sm:flex-row sm:items-center"
          >
            <input value={customCode} onChange={(e) => setCustomCode(e.target.value)} placeholder={t("building.customCodePlaceholder")}
              className="border border-slate-300 rounded-lg px-3 py-2 text-sm uppercase flex-1" />
            <button type="submit" disabled={setCode.isPending || !customCode.trim()}
              className="bg-teal-700 text-white rounded-lg px-4 py-2 text-sm font-medium hover:bg-teal-800 disabled:opacity-50">
              {t("building.setCode")}
            </button>
          </form>
          <p className="text-xs text-slate-400 mt-2">{t("building.customCodeHint")}</p>
        </section>
      )}

      {isBoard && <InviteLinks />}

      {isBoard && (
        <section className="bg-white border border-slate-200 rounded-xl p-4">
          <h2 className="text-sm font-semibold text-slate-700 mb-2">{t("building.members")}</h2>
          <ul className="divide-y divide-slate-100">
            {membersQ.data?.map((m) => (
              <li key={m.userId} className="py-2 flex justify-between text-sm">
                <span className="text-slate-700">
                  {m.displayName}
                  {m.joinedVia && (
                    <span className="ml-2 text-[10px] px-1.5 py-0.5 rounded bg-slate-100 text-slate-500">
                      {t(`building.via.${m.joinedVia}`, m.joinedVia)}
                    </span>
                  )}
                </span>
                <span className="text-slate-400">
                  {m.apartmentNumber ? `${t("building.aptShort", { num: m.apartmentNumber })} · ` : ""}
                  {t(`roles.${m.role}`, m.role)}
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}

function InviteLinks() {
  const { t } = useTranslation();
  const qc = useQueryClient();
  const [title, setTitle] = useState("");
  const [maxUses, setMaxUses] = useState("");
  const [expiresInDays, setExpiresInDays] = useState("30");

  const invitesQ = useQuery({
    queryKey: ["invites"],
    queryFn: async () => (await api.get<InviteDto[]>("/buildings/invites")).data,
  });

  const create = useMutation({
    mutationFn: async () =>
      (
        await api.post<InviteDto>("/buildings/invites", {
          title: title.trim() || null,
          maxUses: maxUses ? Number(maxUses) : null,
          expiresInDays: expiresInDays ? Number(expiresInDays) : null,
        })
      ).data,
    onSuccess: () => {
      toast.success(t("building.inviteCreated"));
      setTitle("");
      setMaxUses("");
      void qc.invalidateQueries({ queryKey: ["invites"] });
    },
    onError: () => toast.error(t("building.inviteFailed")),
  });

  const revoke = useMutation({
    mutationFn: async (id: string) => (await api.post(`/buildings/invites/${id}/revoke`)).data,
    onSuccess: () => {
      toast.success(t("building.inviteRevoked"));
      void qc.invalidateQueries({ queryKey: ["invites"] });
    },
  });

  async function copy(url: string) {
    try {
      await navigator.clipboard.writeText(url);
      toast.success(t("building.inviteCopied"));
    } catch {
      toast.error(t("building.inviteCopyFailed"));
    }
  }

  return (
    <section className="bg-white border border-slate-200 rounded-xl p-4">
      <h2 className="text-sm font-semibold text-slate-700 mb-1">{t("building.inviteLinks")}</h2>
      <p className="text-xs text-slate-400 mb-3">{t("building.inviteLinksHint")}</p>

      <form
        onSubmit={(e) => { e.preventDefault(); create.mutate(); }}
        className="grid grid-cols-1 sm:grid-cols-2 gap-2 mb-4"
      >
        <input
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder={t("building.inviteTitlePlaceholder")}
          className="border border-slate-300 rounded-lg px-3 py-2 text-sm sm:col-span-2"
        />
        <label className="flex flex-col gap-1 text-xs text-slate-500">
          {t("building.inviteMaxUses")}
          <input
            type="number"
            min={1}
            value={maxUses}
            onChange={(e) => setMaxUses(e.target.value)}
            placeholder={t("building.inviteNoLimit")}
            className="border border-slate-300 rounded-lg px-3 py-2 text-sm"
          />
        </label>
        <label className="flex flex-col gap-1 text-xs text-slate-500">
          {t("building.inviteExpiry")}
          <select
            value={expiresInDays}
            onChange={(e) => setExpiresInDays(e.target.value)}
            className="border border-slate-300 rounded-lg px-3 py-2 text-sm"
          >
            <option value="7">{t("building.invite7d")}</option>
            <option value="30">{t("building.invite30d")}</option>
            <option value="90">{t("building.invite90d")}</option>
            <option value="">{t("building.inviteNeverExpires")}</option>
          </select>
        </label>
        <button
          type="submit"
          disabled={create.isPending}
          className="sm:col-span-2 bg-teal-700 text-white rounded-lg px-4 py-2 text-sm font-medium hover:bg-teal-800 disabled:opacity-50"
        >
          {t("building.inviteCreate")}
        </button>
      </form>

      {invitesQ.data && invitesQ.data.length > 0 ? (
        <ul className="divide-y divide-slate-100">
          {invitesQ.data.map((inv) => (
            <li key={inv.id} className="py-3">
              <div className="rounded-xl border border-slate-200 bg-slate-50 p-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <p className="text-sm text-slate-700 font-medium truncate">
                    {inv.title || t("building.inviteUntitled")}
                    {!inv.active && (
                      <span className="ml-2 text-xs text-rose-600">
                        {inv.revoked ? t("building.inviteRevokedTag") : t("building.inviteExpiredTag")}
                      </span>
                    )}
                  </p>
                  <p className="text-xs text-slate-400">
                    {t("building.inviteUses", { used: inv.usedCount, max: inv.maxUses ?? "∞" })}
                    {inv.expiresAt ? ` · ${t("building.inviteUntil", { date: new Date(inv.expiresAt).toLocaleDateString() })}` : ""}
                  </p>
                </div>
                <div className="mt-2 flex items-center gap-2">
                  <input
                    readOnly
                    value={inv.url}
                    onFocus={(e) => e.currentTarget.select()}
                    className={`flex-1 min-w-0 border border-slate-200 rounded-lg px-2.5 py-1.5 text-xs font-mono bg-white ${inv.active ? "text-slate-600" : "text-slate-400 line-through"}`}
                  />
                  {inv.active && (
                    <button onClick={() => copy(inv.url)}
                      className="shrink-0 bg-teal-700 text-white rounded-lg px-3 py-1.5 text-xs font-medium hover:bg-teal-800">
                      {t("building.inviteCopy")}
                    </button>
                  )}
                  {!inv.revoked && (
                    <button onClick={() => revoke.mutate(inv.id)} disabled={revoke.isPending}
                      className="shrink-0 text-xs text-rose-600 underline disabled:opacity-50">
                      {t("building.inviteRevoke")}
                    </button>
                  )}
                </div>
                {inv.usedBy.length > 0 && (
                  <p className="mt-2 text-xs text-slate-500">
                    <span className="text-slate-400">{t("building.inviteJoinedBy")}:</span> {inv.usedBy.join(", ")}
                  </p>
                )}
              </div>
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-sm text-slate-400">{t("building.inviteNone")}</p>
      )}
    </section>
  );
}

function FindOrJoin({ onChanged }: { onChanged: () => void }) {
  const { t } = useTranslation();
  const [tab, setTab] = useState<"find" | "code">("find");

  return (
    <div className="max-w-md mx-auto">
      <h1 className="text-xl font-semibold text-slate-800 mb-3">{t("building.setupTitle")}</h1>
      <div className="flex gap-2 mb-4 flex-wrap">
        {(["find", "code"] as const).map((tabKey) => (
          <button
            key={tabKey}
            onClick={() => setTab(tabKey)}
            className={`px-3 py-1.5 rounded-lg text-sm ${tab === tabKey ? "bg-teal-700 text-white" : "bg-slate-100 text-slate-600"}`}
          >
            {t(`building.tab${tabKey.charAt(0).toUpperCase()}${tabKey.slice(1)}`)}
          </button>
        ))}
      </div>

      {tab === "find" && <FindBuilding onChanged={onChanged} />}
      {tab === "code" && <JoinWithCode onChanged={onChanged} />}
    </div>
  );
}

function FindBuilding({ onChanged }: { onChanged: () => void }) {
  const { t } = useTranslation();
  const [q, setQ] = useState("");
  const [apt, setApt] = useState("");

  const browseQ = useQuery({
    queryKey: ["browse"],
    queryFn: async () => (await api.get<BrowseBuilding[]>("/buildings/browse")).data,
  });

  const request = useMutation({
    mutationFn: async (id: string) => (await api.post(`/buildings/${id}/join-request`, { apartmentNumber: apt || null })).data,
    onSuccess: () => { toast.success(t("building.requestSent")); onChanged(); },
    onError: () => toast.error(t("building.requestFailed")),
  });

  const list = (browseQ.data ?? []).filter((b) => b.name.toLowerCase().includes(q.trim().toLowerCase()));

  return (
    <div>
      <p className="text-sm text-slate-500 mb-3">{t("building.findIntro")}</p>
      <input value={q} onChange={(e) => setQ(e.target.value)} placeholder={t("building.search")}
        className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm mb-2" />
      <input value={apt} onChange={(e) => setApt(e.target.value)} placeholder={t("building.yourApartment")}
        className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm mb-3" />

      {browseQ.isLoading ? (
        <p className="text-slate-500 text-sm">{t("common.loading")}</p>
      ) : list.length === 0 ? (
        <p className="text-slate-400 text-sm">{t("building.noBuildings")}</p>
      ) : (
        <ul className="space-y-2">
          {list.map((b) => (
            <li key={b.id} className="flex items-center justify-between gap-2 bg-white border border-slate-200 rounded-xl px-3 py-2.5">
              <div className="min-w-0">
                <p className="text-sm font-medium text-slate-800 truncate">{b.name}</p>
                {b.address && <p className="text-xs text-slate-400 truncate">{b.address}</p>}
              </div>
              {b.requested ? (
                <span className="text-xs text-teal-700 font-medium shrink-0">✓ {t("building.requested")}</span>
              ) : (
                <button onClick={() => request.mutate(b.id)} disabled={request.isPending}
                  className="bg-teal-700 text-white rounded-lg px-3 py-1.5 text-sm font-medium hover:bg-teal-800 disabled:opacity-50 shrink-0">
                  {t("building.requestToJoin")}
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function JoinWithCode({ onChanged }: { onChanged: () => void }) {
  const { t } = useTranslation();
  const [code, setCode] = useState("");
  const [apt, setApt] = useState("");
  const join = useMutation({
    mutationFn: async () => (await api.post("/buildings/join", { joinCode: code, apartmentNumber: apt })).data,
    onSuccess: () => { toast.success(t("building.toastJoined")); onChanged(); },
    onError: () => toast.error(t("building.toastInvalidCode")),
  });
  return (
    <form onSubmit={(e) => { e.preventDefault(); join.mutate(); }} className="flex flex-col gap-3">
      <p className="text-sm text-slate-500">{t("building.haveCode")}</p>
      <Field label={t("building.joinCodeField")} value={code} onChange={setCode} required />
      <Field label={t("building.yourApartment")} value={apt} onChange={setApt} />
      <Submit pending={join.isPending}>{t("building.joinBuilding")}</Submit>
    </form>
  );
}

function Field({ label, value, onChange, required }: { label: string; value: string; onChange: (v: string) => void; required?: boolean }) {
  return (
    <label className="flex flex-col gap-1 text-sm">
      <span className="text-slate-600">{label}</span>
      <input value={value} required={required} onChange={(e) => onChange(e.target.value)} className="border border-slate-300 rounded-lg px-3 py-2" />
    </label>
  );
}

function Submit({ pending, children }: { pending: boolean; children: React.ReactNode }) {
  return (
    <button type="submit" disabled={pending}
      className="bg-teal-700 text-white rounded-lg py-2 text-sm font-medium hover:bg-teal-800 disabled:opacity-50">
      {children}
    </button>
  );
}
