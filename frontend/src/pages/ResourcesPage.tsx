import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { api } from "@/lib/api";

interface ResourceLink {
  id: string;
  title: string;
  description: string | null;
  url: string;
  sortOrder: number;
}

export default function ResourcesPage() {
  const { t } = useTranslation();
  const q = useQuery({
    queryKey: ["resources"],
    queryFn: async () => (await api.get<ResourceLink[]>("/resources")).data,
  });

  return (
    <div className="max-w-2xl space-y-4">
      <div>
        <h1 className="text-xl font-semibold text-slate-800">{t("resources.title")}</h1>
        <p className="text-sm text-slate-500 mt-1">{t("resources.intro")}</p>
      </div>

      {q.isLoading ? (
        <p className="text-slate-500">{t("common.loading")}</p>
      ) : q.data && q.data.length > 0 ? (
        <ul className="space-y-2">
          {q.data.map((r) => (
            <li key={r.id}>
              <a
                href={r.url}
                target="_blank"
                rel="noopener noreferrer"
                className="block bg-white border border-slate-200 rounded-xl p-4 hover:border-slate-400"
              >
                <div className="flex items-center justify-between gap-3">
                  <span className="font-medium text-slate-800">{r.title}</span>
                  <span className="text-xs text-slate-400 shrink-0">{t("resources.visit")} ↗</span>
                </div>
                {r.description && <p className="text-sm text-slate-500 mt-1">{r.description}</p>}
              </a>
            </li>
          ))}
        </ul>
      ) : (
        <p className="text-slate-500">{t("resources.empty")}</p>
      )}
    </div>
  );
}
