import { useState } from "react";
import { useParams, Link } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { api } from "@/lib/api";
import { useAuth } from "@/auth/AuthContext";
import { CATEGORY_LABELS, type EntryDetail } from "@/api/types";

export default function EntryDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { user } = useAuth();
  const qc = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState("");

  const entryQ = useQuery({
    queryKey: ["entry", id],
    queryFn: async () => (await api.get<EntryDetail>(`/entries/${id}`)).data,
  });

  const isBoard = user?.membership?.role === "board" || user?.membership?.role === "admin";
  const isReporter = entryQ.data?.reporterUserId === user?.id;

  const save = useMutation({
    mutationFn: async () => (await api.patch(`/entries/${id}`, { originalText: draft })).data,
    onSuccess: () => {
      toast.success("Entry updated.");
      setEditing(false);
      void qc.invalidateQueries({ queryKey: ["entry", id] });
      void qc.invalidateQueries({ queryKey: ["entries"] });
    },
    onError: () => toast.error("Could not update."),
  });

  const archive = useMutation({
    mutationFn: async () => (await api.post(`/entries/${id}/archive`)).data,
    onSuccess: () => {
      toast.success("Entry archived.");
      void qc.invalidateQueries({ queryKey: ["entry", id] });
      void qc.invalidateQueries({ queryKey: ["entries"] });
    },
  });

  const restore = useMutation({
    mutationFn: async () => (await api.post(`/entries/${id}/restore`)).data,
    onSuccess: () => {
      toast.success("Entry restored.");
      void qc.invalidateQueries({ queryKey: ["entry", id] });
      void qc.invalidateQueries({ queryKey: ["entries"] });
    },
  });

  const translate = useMutation({
    mutationFn: async (language: string) => (await api.post(`/entries/${id}/translate`, { language })).data,
    onSuccess: () => {
      toast.success("Translation added.");
      void qc.invalidateQueries({ queryKey: ["entry", id] });
    },
    onError: () => toast.error("Could not translate."),
  });

  if (entryQ.isLoading) return <p className="text-slate-500">Loading…</p>;
  if (!entryQ.data) return <p className="text-slate-500">Entry not found.</p>;
  const e = entryQ.data;

  const viewerLang = user?.preferredLanguage ?? "en";
  const canOfferViewerTranslation =
    viewerLang !== e.originalLanguage && !e.translations.some((t) => t.targetLanguage === viewerLang);

  return (
    <div className="max-w-2xl space-y-5">
      <Link to="/timeline" className="text-sm text-slate-500 hover:text-slate-800">← Timeline</Link>

      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-xl font-semibold text-slate-800">{CATEGORY_LABELS[e.category]}</h1>
          <p className="text-sm text-slate-500">
            Occurred {new Date(e.occurredAt).toLocaleString()} · Logged {new Date(e.createdAt).toLocaleDateString()}
          </p>
          <p className="text-sm text-slate-500">
            Reporter: {e.reporterName}
            {e.subjectApartment && ` · Apartment ${e.subjectApartment}`}
          </p>
          <div className="flex gap-2 mt-1">
            {e.editedAt && <span className="text-xs px-2 py-0.5 rounded bg-amber-100 text-amber-700">edited</span>}
            {e.archived && <span className="text-xs px-2 py-0.5 rounded bg-rose-100 text-rose-700">archived</span>}
          </div>
        </div>
        <div className="flex gap-2">
          {isReporter && !e.archived && !editing && (
            <button onClick={() => { setEditing(true); setDraft(e.originalText); }} className="text-sm text-slate-600 underline">Edit</button>
          )}
          {(isReporter || isBoard) && !e.archived && (
            <button onClick={() => archive.mutate()} className="text-sm text-rose-600 underline">Archive</button>
          )}
          {isBoard && e.archived && (
            <button onClick={() => restore.mutate()} className="text-sm text-slate-600 underline">Restore</button>
          )}
        </div>
      </div>

      <section className="bg-white border border-slate-200 rounded-xl p-4">
        <div className="text-xs uppercase tracking-wide text-slate-400 mb-1">
          Original · {e.originalLanguage}
        </div>
        {editing ? (
          <div className="space-y-2">
            <textarea rows={5} value={draft} onChange={(ev) => setDraft(ev.target.value)} className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm" />
            <div className="flex gap-2">
              <button onClick={() => save.mutate()} disabled={save.isPending} className="bg-slate-900 text-white rounded-lg px-4 py-1.5 text-sm">Save</button>
              <button onClick={() => setEditing(false)} className="text-sm text-slate-500">Cancel</button>
            </div>
          </div>
        ) : (
          <p className="text-slate-800 whitespace-pre-wrap">{e.originalText}</p>
        )}
      </section>

      {e.translations.map((translation) => (
        <section key={translation.targetLanguage} className="bg-slate-50 border border-slate-200 rounded-xl p-4">
          <div className="text-xs uppercase tracking-wide text-slate-400 mb-1">
            Translation · {translation.targetLanguage}
            {translation.targetLanguage === e.sharedLanguage && " (building shared)"}
          </div>
          {translation.status === "completed" ? (
            <>
              <p className="text-slate-700 whitespace-pre-wrap">{translation.translatedText}</p>
              <p className="text-xs text-slate-400 italic mt-2">AI-generated translation from {e.originalLanguage}.</p>
            </>
          ) : (
            <p className="text-sm text-slate-400 italic">Translation {translation.status}, retrying…</p>
          )}
        </section>
      ))}

      {canOfferViewerTranslation && (
        <button
          onClick={() => translate.mutate(viewerLang)}
          disabled={translate.isPending}
          className="text-sm text-slate-600 underline hover:text-slate-900 disabled:opacity-50"
        >
          Translate to my language ({viewerLang})
        </button>
      )}

      {e.attachmentIds.length > 0 && (
        <section>
          <div className="text-xs uppercase tracking-wide text-slate-400 mb-1">
            Attachment{e.attachmentIds.length > 1 ? "s" : ""}
          </div>
          <div className="flex flex-wrap gap-3">
            {e.attachmentIds.map((aid) => (
              <img
                key={aid}
                src={`/api/v1/entries/${e.id}/attachments/${aid}`}
                alt="attachment"
                className="max-h-80 rounded-lg border border-slate-200"
              />
            ))}
          </div>
        </section>
      )}

      {e.revisions.length > 0 && (
        <section className="bg-white border border-slate-200 rounded-xl p-4">
          <h2 className="text-sm font-semibold text-slate-700 mb-2">Edit history</h2>
          <ul className="space-y-2">
            {e.revisions.map((r) => (
              <li key={r.id} className="text-sm text-slate-500">
                <span className="text-xs text-slate-400">{new Date(r.editedAt).toLocaleString()}</span>
                <p className="whitespace-pre-wrap line-through decoration-slate-300">{r.previousText}</p>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
