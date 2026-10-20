import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

import translationEN from './locales/en/translation.json';
import translationFA from './locales/fa/translation.json';

const resources = {
  en: { translation: translationEN },
  fa: { translation: translationFA }
};

const savedLanguage = localStorage.getItem('app_language') || 'fa';

i18n
  .use(initReactI18next)
  .init({
    resources,
    lng: savedLanguage,
    fallbackLng: 'en',
    interpolation: {
      escapeValue: false
    }
  });

export default i18n;
