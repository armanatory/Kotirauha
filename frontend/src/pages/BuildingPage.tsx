import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { api } from "@/lib/api";
import { useAuth } from "@/auth/AuthContext";
import type { BuildingDto, MemberDto } from "@/api/types";

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

  if (buildingQ.isLoading) return <p className="text-slate-500">{t("common.loading")}</p>;

  const onChanged = () => {
    void qc.invalidateQueries({ queryKey: ["building"] });
    void refresh();
  };

  return buildingQ.data ? (
    <BuildingHome building={buildingQ.data} onChanged={onChanged} />
  ) : (
    <CreateOrJoin onDone={onChanged} />
  );
}

function BuildingHome({ building, onChanged }: { building: BuildingDto; onChanged: () => void }) {
  const { t } = useTranslation();
  const isBoard = building.role === "board" || building.role === "admin";
  const [customCode, setCustomCode] = useState("");

  const membersQ = useQuery({
    queryKey: ["members"],
    queryFn: async () => (await api.get<MemberDto[]>("/buildings/members")).data,
    enabled: isBoard,
  });

  const regen = useMutation({
    mutationFn: async () => (await api.post<{ joinCode: string }>("/buildings/join-code", {})).data,
    onSuccess: () => {
      toast.success(t("building.toastCodeRegenerated"));
      onChanged();
    },
  });

  const setCode = useMutation({
    mutationFn: async (code: string) => (await api.post<{ joinCode: string }>("/buildings/join-code", { code })).data,
    onSuccess: () => {
      toast.success(t("building.toastCodeSet"));
      setCustomCode("");
      onChanged();
    },
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
          <h2 className="text-sm font-semibold text-slate-700 mb-2">{t("building.joinCode")}</h2>
          <div className="flex items-center gap-3">
            <code className="px-3 py-1.5 bg-slate-100 rounded-lg text-lg tracking-widest">{building.joinCode}</code>
            <button
              onClick={() => regen.mutate()}
              disabled={regen.isPending}
              className="text-sm text-slate-600 underline hover:text-slate-900"
            >
              {t("building.regenerate")}
            </button>
          </div>
          <p className="text-xs text-slate-400 mt-2">{t("building.shareCode")}</p>

          <form
            onSubmit={(e) => {
              e.preventDefault();
              if (customCode.trim()) setCode.mutate(customCode);
            }}
            className="mt-3 flex flex-col gap-1.5 sm:flex-row sm:items-center"
          >
            <input
              value={customCode}
              onChange={(e) => setCustomCode(e.target.value)}
              placeholder={t("building.customCodePlaceholder")}
              className="border border-slate-300 rounded-lg px-3 py-2 text-sm uppercase flex-1"
            />
            <button
              type="submit"
              disabled={setCode.isPending || !customCode.trim()}
              className="bg-slate-900 text-white rounded-lg px-4 py-2 text-sm font-medium hover:bg-slate-700 disabled:opacity-50"
            >
              {t("building.setCode")}
            </button>
          </form>
          <p className="text-xs text-slate-400 mt-2">{t("building.customCodeHint")}</p>
        </section>
      )}

      {isBoard && (
        <section className="bg-white border border-slate-200 rounded-xl p-4">
          <h2 className="text-sm font-semibold text-slate-700 mb-2">{t("building.members")}</h2>
          <ul className="divide-y divide-slate-100">
            {membersQ.data?.map((m) => (
              <li key={m.userId} className="py-2 flex justify-between text-sm">
                <span className="text-slate-700">{m.displayName}</span>
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

function CreateOrJoin({ onDone }: { onDone: () => void }) {
  const { t } = useTranslation();
  const [tab, setTab] = useState<"create" | "join">("create");
  const [name, setName] = useState("");
  const [lang, setLang] = useState("fi");
  const [apt, setApt] = useState("");
  const [code, setCode] = useState("");

  const create = useMutation({
    mutationFn: async (input: { name: string; sharedLanguage: string; apartmentNumber: string }) =>
      (await api.post("/buildings", input)).data,
    onSuccess: () => {
      toast.success(t("building.toastCreated"));
      onDone();
    },
    onError: () => toast.error(t("building.toastCouldNotCreate")),
  });

  const join = useMutation({
    mutationFn: async (input: { joinCode: string; apartmentNumber: string }) =>
      (await api.post("/buildings/join", input)).data,
    onSuccess: () => {
      toast.success(t("building.toastJoined"));
      onDone();
    },
    onError: () => toast.error(t("building.toastInvalidCode")),
  });

  return (
    <div className="max-w-md mx-auto">
      <h1 className="text-xl font-semibold text-slate-800 mb-1">{t("building.setupTitle")}</h1>
      <p className="text-sm text-slate-500 mb-4">{t("building.setupIntro")}</p>

      <div className="flex gap-2 mb-4">
        <button
          onClick={() => setTab("create")}
          className={`px-3 py-1.5 rounded-lg text-sm ${tab === "create" ? "bg-slate-900 text-white" : "bg-slate-100 text-slate-600"}`}
        >
          {t("building.create")}
        </button>
        <button
          onClick={() => setTab("join")}
          className={`px-3 py-1.5 rounded-lg text-sm ${tab === "join" ? "bg-slate-900 text-white" : "bg-slate-100 text-slate-600"}`}
        >
          {t("building.join")}
        </button>
      </div>

      {tab === "create" ? (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            create.mutate({ name, sharedLanguage: lang, apartmentNumber: apt });
          }}
          className="flex flex-col gap-3"
        >
          <Field label={t("building.buildingName")} value={name} onChange={setName} required />
          <label className="flex flex-col gap-1 text-sm">
            <span className="text-slate-600">{t("building.sharedLanguage")}</span>
            <select value={lang} onChange={(e) => setLang(e.target.value)} className="border border-slate-300 rounded-lg px-3 py-2">
              <option value="fi">{t("languages.fi")}</option>
              <option value="en">{t("languages.en")}</option>
            </select>
          </label>
          <Field label={t("building.yourApartment")} value={apt} onChange={setApt} />
          <Submit pending={create.isPending}>{t("building.createBuilding")}</Submit>
        </form>
      ) : (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            join.mutate({ joinCode: code, apartmentNumber: apt });
          }}
          className="flex flex-col gap-3"
        >
          <Field label={t("building.joinCodeField")} value={code} onChange={setCode} required />
          <Field label={t("building.yourApartment")} value={apt} onChange={setApt} />
          <Submit pending={join.isPending}>{t("building.joinBuilding")}</Submit>
        </form>
      )}
    </div>
  );
}

function Field({ label, value, onChange, required }: { label: string; value: string; onChange: (v: string) => void; required?: boolean }) {
  return (
    <label className="flex flex-col gap-1 text-sm">
      <span className="text-slate-600">{label}</span>
      <input
        value={value}
        required={required}
        onChange={(e) => onChange(e.target.value)}
        className="border border-slate-300 rounded-lg px-3 py-2"
      />
    </label>
  );
}

function Submit({ pending, children }: { pending: boolean; children: React.ReactNode }) {
  return (
    <button
      type="submit"
      disabled={pending}
      className="bg-slate-900 text-white rounded-lg py-2 text-sm font-medium hover:bg-slate-700 disabled:opacity-50"
    >
      {children}
    </button>
  );
}
