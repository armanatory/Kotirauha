import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";

export default function NotFoundPage() {
  const { t } = useTranslation();
  return (
    <div className="text-center py-20">
      <h1 className="text-xl font-semibold text-slate-800">{t("notFound.title")}</h1>
      <Link to="/timeline" className="inline-block mt-3 text-slate-700 underline">
        {t("notFound.back")}
      </Link>
    </div>
  );
}
