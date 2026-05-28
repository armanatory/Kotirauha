import { useTranslation } from "react-i18next";

const LANGS = [
  { code: "fi", label: "Suomi" },
  { code: "en", label: "English" },
] as const;

// Public-facing FI / EN switch. Persists the choice so it sticks across visits
// and wins over device-language auto-detection.
export default function LanguageToggle({ className = "" }: { className?: string }) {
  const { i18n } = useTranslation();
  const current = i18n.language === "en" ? "en" : "fi";

  function choose(code: string) {
    void i18n.changeLanguage(code);
    localStorage.setItem("lang", code);
  }

  return (
    <div className={`inline-flex rounded-full border border-slate-200 bg-white/80 p-0.5 text-xs ${className}`}>
      {LANGS.map((l) => (
        <button
          key={l.code}
          type="button"
          onClick={() => choose(l.code)}
          className={`px-3 py-1 rounded-full transition ${
            current === l.code ? "bg-teal-700 text-white" : "text-slate-600 hover:text-slate-900"
          }`}
        >
          {l.label}
        </button>
      ))}
    </div>
  );
}
