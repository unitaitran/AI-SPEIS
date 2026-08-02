import i18n from 'i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import { initReactI18next } from 'react-i18next';

import enHomepage from './locales/en/landing.json';
import viHomepage from './locales/vi/landing.json';

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
import enAdminSubscription from './locales/en/admin-subscription.json';
import viAdminSubscription from './locales/vi/admin-subscription.json';
import enQuestionBank from './locales/en/questionBank.json';
import viQuestionBank from './locales/vi/questionBank.json';
import enInterview from './locales/en/interview.json';
import viInterview from './locales/vi/interview.json';

import enPackages from './locales/en/packages.json';
import viPackages from './locales/vi/packages.json';

import enCvjd from './locales/en/cvjd.json';
import viCvjd from './locales/vi/cvjd.json';

const resources = {
  en: {
    homepage: enHomepage,
    login: enLogin,
    register: enRegister,
    dashboard: enDashboard,
    'admin-users': enAdminUsers,
    'admin-dashboard': enAdminDashboard,
    'admin-subscription': enAdminSubscription,
    questionBank: enQuestionBank,
    interview: enInterview,
    packages: enPackages,
    cvjd: enCvjd,
  },
  vi: {
    homepage: viHomepage,
    login: viLogin,
    register: viRegister,
    dashboard: viDashboard,
    'admin-users': viAdminUsers,
    'admin-dashboard': viAdminDashboard,
    'admin-subscription': viAdminSubscription,
    questionBank: viQuestionBank,
    interview: viInterview,
    packages: viPackages,
    cvjd: viCvjd,
  },
};

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources,
    fallbackLng: 'vi',
    supportedLngs: ['vi', 'en'],
    defaultNS: 'homepage',
    ns: ['homepage', 'login', 'register', 'dashboard', 'admin-users', 'admin-dashboard', 'admin-subscription', 'questionBank', 'interview', 'packages', 'cvjd'],
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
