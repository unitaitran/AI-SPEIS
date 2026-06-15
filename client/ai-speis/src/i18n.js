import i18n from 'i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import { initReactI18next } from 'react-i18next';

import enLanding from './locales/en/landing.json';
import viLanding from './locales/vi/landing.json';

import enLogin from './locales/en/login.json';
import viLogin from './locales/vi/login.json';

import enRegister from './locales/en/register.json';
import viRegister from './locales/vi/register.json';

const resources = {
  en: {
    landing: enLanding,
    login: enLogin,
    register: enRegister
  },
  vi: {
    landing: viLanding,
    login: viLogin,
    register: viRegister,
  },
};

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources,
    fallbackLng: 'vi',
    supportedLngs: ['vi', 'en'],
    defaultNS: 'landing',
    ns: ['landing', 'login', 'register'],
    interpolation: {
      escapeValue: false,
    },
    detection: {
      order: ['localStorage', 'navigator', 'htmlTag'],
      caches: ['localStorage'],
      lookupLocalStorage: 'ai-speis-language',
    },
  });

export default i18n;
