import i18n from "i18next";
import { initReactI18next } from "react-i18next";

const en = {
  translation: {
    appName: "Kotirauha",
    nav: { timeline: "Timeline", newEntry: "New entry", building: "Building", export: "Export", profile: "Profile" },
    auth: {
      login: "Log in",
      register: "Create account",
      email: "Email",
      password: "Password",
      displayName: "Display name",
      logout: "Log out",
    },
  },
};

const fi = {
  translation: {
    appName: "Kotirauha",
    nav: { timeline: "Aikajana", newEntry: "Uusi merkintä", building: "Taloyhtiö", export: "Vienti", profile: "Profiili" },
    auth: {
      login: "Kirjaudu",
      register: "Luo tili",
      email: "Sähköposti",
      password: "Salasana",
      displayName: "Näyttönimi",
      logout: "Kirjaudu ulos",
    },
  },
};

i18n.use(initReactI18next).init({
  resources: { en, fi },
  lng: localStorage.getItem("lang") ?? "en",
  fallbackLng: "en",
  interpolation: { escapeValue: false },
});

export default i18n;
