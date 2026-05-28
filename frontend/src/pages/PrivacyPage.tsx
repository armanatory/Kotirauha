import { Link } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { BRAND } from "@/lib/branding";
import LanguageToggle from "@/components/LanguageToggle";

const UPDATED = "2026-05-28";
const CONTACT = "armanatory@gmail.com";

export default function PrivacyPage() {
  const { i18n } = useTranslation();
  const fi = i18n.language !== "en";
  const content = fi ? FI : EN;

  return (
    <div className="brand-wash min-h-screen">
      <div className="absolute top-4 right-4">
        <LanguageToggle className="shadow-sm" />
      </div>
      <div className="max-w-2xl mx-auto px-4 py-12">
        <Link to="/" className="text-sm text-teal-700 hover:text-teal-900">← {BRAND.name}</Link>
        <h1 className="text-2xl font-semibold text-slate-800 mt-3">{content.title}</h1>
        <p className="text-sm text-slate-400 mt-1">{content.updated} {UPDATED}</p>

        <div className="mt-6 space-y-6">
          {content.sections.map((s) => (
            <section key={s.h}>
              <h2 className="text-base font-semibold text-slate-700">{s.h}</h2>
              <p className="text-sm text-slate-600 leading-relaxed mt-1 whitespace-pre-line">{s.p}</p>
            </section>
          ))}
          <p className="text-sm text-slate-600">
            {content.contact} <a href={`mailto:${CONTACT}`} className="text-teal-700 underline">{CONTACT}</a>.
          </p>
        </div>
      </div>
    </div>
  );
}

const EN = {
  title: "Privacy & cookies",
  updated: "Last updated",
  contact: "Questions or requests about your data? Contact",
  sections: [
    {
      h: "Who we are",
      p: "Kotirauha is a tool for documenting residential incidents in apartment buildings. This notice explains, in plain terms, what data we handle and why. It is intentionally general and may be refined over time.",
    },
    {
      h: "What we collect",
      p: "Account details you provide: your email address, display name, and preferred language.\nThe reports you write: their text, category, location, timestamp, and any photos you attach. Your original wording is always preserved; machine translations are clearly labelled.\nBasic usage analytics: the pages visited, the referring site, the interface language, an approximate country, and a one-way hashed identifier used only to estimate unique visitors. We do not store raw IP addresses.",
    },
    {
      h: "Why we use it",
      p: "To run the service: sign you in by email link, show your building's records, translate entries, and produce exportable documentation. Analytics help us understand usage and improve the product. The legal basis is our legitimate interest in operating and improving Kotirauha, and your consent where required.",
    },
    {
      h: "Cookies and local storage",
      p: "We do not use advertising or cross-site tracking cookies. We store a login token and your language choice in your browser's local storage so you stay signed in and see the right language. Clearing your browser storage removes them.",
    },
    {
      h: "Sharing",
      p: "Your reports are visible to members of your building and its board, as the product is designed for shared documentation. We use a few service providers to operate Kotirauha (email delivery and AI translation); they process data only to provide those features. We do not sell your data.",
    },
    {
      h: "Retention",
      p: "We keep account and report data for as long as the building uses the service, in line with the product's purpose of preserving an accurate record. Analytics events are kept in aggregate for a limited period.",
    },
    {
      h: "Your rights",
      p: "Under the GDPR you may request access to your data, correction, deletion, or a copy. Note that incident records may be retained where needed for the building's documentation. Contact us to exercise any of these rights.",
    },
  ],
};

const FI = {
  title: "Tietosuoja ja evästeet",
  updated: "Päivitetty viimeksi",
  contact: "Kysymyksiä tai pyyntöjä tiedoistasi? Ota yhteyttä",
  sections: [
    {
      h: "Keitä olemme",
      p: "Kotirauha on työkalu taloyhtiöiden häiriötilanteiden dokumentointiin. Tämä seloste kertoo selkokielisesti, mitä tietoja käsittelemme ja miksi. Se on tarkoituksella yleisluontoinen ja sitä voidaan tarkentaa myöhemmin.",
    },
    {
      h: "Mitä keräämme",
      p: "Antamasi tilitiedot: sähköpostiosoite, näyttönimi ja kielivalinta.\nKirjoittamasi ilmoitukset: teksti, luokka, sijainti, aikaleima ja liittämäsi kuvat. Alkuperäinen sanamuotosi säilytetään aina; konekäännökset on merkitty selvästi.\nPerustason käyttöanalytiikka: vieraillut sivut, lähdesivusto, käyttöliittymän kieli, likimääräinen maa sekä yksisuuntainen tiiviste, jota käytetään vain yksittäisten kävijöiden arviointiin. Emme tallenna raakoja IP-osoitteita.",
    },
    {
      h: "Miksi käytämme niitä",
      p: "Palvelun toimintaan: sähköpostilinkillä kirjautuminen, taloyhtiösi kirjausten näyttäminen, käännökset ja vietävän dokumentaation tuottaminen. Analytiikka auttaa ymmärtämään käyttöä ja parantamaan tuotetta. Oikeusperuste on oikeutettu etumme Kotirauhan ylläpitämiseksi ja kehittämiseksi sekä suostumuksesi sitä vaadittaessa.",
    },
    {
      h: "Evästeet ja paikallinen tallennus",
      p: "Emme käytä mainos- tai seurantaevästeitä. Tallennamme kirjautumistunnisteen ja kielivalintasi selaimesi paikalliseen tallennustilaan, jotta pysyt kirjautuneena ja näet oikean kielen. Selaimen tallennustilan tyhjentäminen poistaa ne.",
    },
    {
      h: "Tietojen jakaminen",
      p: "Ilmoituksesi näkyvät taloyhtiösi jäsenille ja hallitukselle, sillä tuote on tarkoitettu yhteiseen dokumentointiin. Käytämme muutamaa palveluntarjoajaa Kotirauhan toimintaan (sähköpostin lähetys ja tekoälykäännökset); ne käsittelevät tietoja vain näiden ominaisuuksien tuottamiseksi. Emme myy tietojasi.",
    },
    {
      h: "Säilytys",
      p: "Säilytämme tili- ja ilmoitustietoja niin kauan kuin taloyhtiö käyttää palvelua, tuotteen tarkoituksen mukaisesti tarkan kirjauksen säilyttämiseksi. Analytiikkatapahtumat säilytetään koosteena rajatun ajan.",
    },
    {
      h: "Oikeutesi",
      p: "GDPR:n nojalla voit pyytää pääsyä tietoihisi, niiden oikaisua, poistoa tai kopiota. Huomaa, että häiriökirjauksia voidaan säilyttää, jos taloyhtiön dokumentointi sitä edellyttää. Ota yhteyttä käyttääksesi näitä oikeuksia.",
    },
  ],
};
