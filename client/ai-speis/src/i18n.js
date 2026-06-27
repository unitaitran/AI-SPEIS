import i18n from 'i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import { initReactI18next } from 'react-i18next';

import enLanding from './locales/en/landing.json';
import viLanding from './locales/vi/landing.json';

import enLogin from './locales/en/login.json';
import viLogin from './locales/vi/login.json';

import enRegister from './locales/en/register.json';
import viRegister from './locales/vi/register.json';

import enDashboard from './locales/en/dashboard.json';
import viDashboard from './locales/vi/dashboard.json';
import enAdminUsers from './locales/en/admin-users.json';
import viAdminUsers from './locales/vi/admin-users.json';
import enAdminDashboard from './locales/en/admin-dashboard.json';
import viAdminDashboard from './locales/vi/admin-dashboard.json';
import enQuestionBank from './locales/en/questionBank.json';
import viQuestionBank from './locales/vi/questionBank.json';

const resources = {
  en: {
    landing: enLanding,
    login: enLogin,
    register: enRegister,
    dashboard: enDashboard,
    'admin-users': enAdminUsers,
    'admin-dashboard': enAdminDashboard,
    questionBank: enQuestionBank,
  },
  vi: {
    landing: viLanding,
    login: viLogin,
    register: viRegister,
    dashboard: viDashboard,
    'admin-users': viAdminUsers,
    'admin-dashboard': viAdminDashboard,
    questionBank: viQuestionBank,
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
    ns: ['landing', 'login', 'register', 'dashboard'],
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
