/* Display instants in Asia/Amman. Serialize lecture DateTime wall clocks as
   ISO local text; other instant fields retain their existing converter contract. */
(() => {
  'use strict';
  const locale = document.documentElement.lang === 'en' ? 'en-GB' : 'ar-JO-u-nu-latn';
  const options = { timeZone: 'Asia/Amman', year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' };
  function parts(value) {
    return Object.fromEntries(new Intl.DateTimeFormat('en-GB', options)
      .formatToParts(new Date(value)).map(part => [part.type, part.value]));
  }
  function input(value) {
    if (!value) return '';
    const p = parts(value);
    return `${p.year}-${p.month}-${p.day} ${p.hour}:${p.minute}`;
  }
  function display(value) {
    return value ? new Intl.DateTimeFormat(locale, options)
      .format(new Date(value)) : '';
  }
  function displayDate(value) {
    return value ? new Intl.DateTimeFormat(locale, {
      timeZone: 'Asia/Amman', year: 'numeric', month: '2-digit', day: '2-digit'
    }).format(new Date(value)) : '';
  }
  // DateTime lecture DTOs require ISO local time (Kind=Unspecified), not an
  // instant. Do not pass through Date/toISOString or append Z/an offset.
  function localDateTime(value) {
    const text = value?.trim();
    return text ? text.replace(' ', 'T') + ':00' : null;
  }
  window.ZuLmsDates = { input, display, displayDate, localDateTime };
})();
