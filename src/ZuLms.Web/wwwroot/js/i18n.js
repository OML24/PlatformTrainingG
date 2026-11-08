(function () {
  'use strict';

  const language = (document.documentElement.lang || 'ar').toLowerCase().startsWith('en') ? 'en' : 'ar';

  const dictionary = {
    ar: {
      confirm: 'تأكيد',
      cancel: 'إلغاء',
      close: 'إغلاق',
      loading: 'جارٍ التحميل…',
      empty: 'لا توجد بيانات لعرضها.',
      error: 'تعذّر إكمال الطلب.',
      forbidden: 'لا تملك صلاحية لهذه الصفحة',
      'needStatus.Approved': 'معتمدة',
      'needStatus.Returned': 'معادة',
      'needStatus.Submitted': 'بانتظار المراجعة',
      'needStatus.Pending': 'بانتظار المراجعة',
      'needStatus.Rejected': 'مرفوضة',
      'needStatus.Deactivated': 'معطّلة'
    },
    en: {
      confirm: 'Confirm',
      cancel: 'Cancel',
      close: 'Close',
      loading: 'Loading…',
      empty: 'There is nothing to show.',
      error: 'The request could not be completed.',
      forbidden: 'You do not have access to this page.',
      'needStatus.Approved': 'Approved',
      'needStatus.Returned': 'Returned',
      'needStatus.Submitted': 'Pending review',
      'needStatus.Pending': 'Pending review',
      'needStatus.Rejected': 'Rejected',
      'needStatus.Deactivated': 'Deactivated'
    }
  };

  function t(key) {
    const table = dictionary[language] || dictionary.ar;
    return table[key] || key;
  }

  function localize(ar, en) {
    return language === 'en' && en ? en : ar;
  }

  function field(item, name, fallback = '') {
    if (!item) return fallback;
    const preferred = language === 'en' ? item[`${name}En`] : item[`${name}Ar`];
    const alternate = language === 'en' ? item[`${name}Ar`] : item[`${name}En`];
    return preferred || alternate || item[name] || fallback;
  }

  async function toggle() {
    const next = language === 'en' ? 'ar' : 'en';
    try {
      await ZuLms.api.patch('/api/v1/auth/language', { language: next }, { silent401: true });
    } catch {
      document.cookie = ZuLmsSession.cookieName('zu_lang') + '=' + next + '; path=/; max-age=' + (60 * 60 * 24 * 365) + '; samesite=lax; secure';
    }
    window.location.reload();
  }

  document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('[data-language-toggle]').forEach(button => {
      button.addEventListener('click', toggle);
    });
  });

  window.ZuLmsI18n = { language: language, t: t, localize: localize, field: field, toggle: toggle };
})();
