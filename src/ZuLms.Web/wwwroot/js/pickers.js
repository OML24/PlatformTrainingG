(function () {
  'use strict';

  const arabic = (document.documentElement.lang || 'ar').toLowerCase().startsWith('ar');

  function initOne(input) {
    const $ = window.jQuery;
    if (!$ || !$.fn.daterangepicker) return;
    const element = $(input);
    if (element.data('daterangepicker')) return;
    const withTime = input.classList.contains('zu-datetime');
    element.daterangepicker({
      singleDatePicker: true,
      timePicker: withTime,
      timePicker24Hour: true,
      timePickerIncrement: 1,
      drops: 'auto',
      autoApply: !withTime,
      autoUpdateInput: false,
      parentEl: input.closest('dialog') || 'body',
      minYear: 2000,
      maxYear: 2100,
      locale: {
        format: withTime ? 'YYYY-MM-DD HH:mm' : 'YYYY-MM-DD',
        applyLabel: arabic ? 'تطبيق' : 'Apply',
        cancelLabel: arabic ? 'إلغاء' : 'Cancel',
        daysOfWeek: arabic
          ? ['ح', 'ن', 'ث', 'ر', 'خ', 'ج', 'س']
          : ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa'],
        monthNames: arabic
          ? ['يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو', 'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر']
          : ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'],
        firstDay: arabic ? 6 : 0
      }
    });
    element.on('apply.daterangepicker', (_event, picker) => {
      input.value = picker.startDate.format(withTime ? 'YYYY-MM-DD HH:mm' : 'YYYY-MM-DD');
      input.dispatchEvent(new Event('change', { bubbles: true }));
    });
    element.on('show.daterangepicker', (_event, picker) => {
      validate();
      const format = withTime ? 'YYYY-MM-DD HH:mm' : 'YYYY-MM-DD';
      const existing = window.moment(input.value, format, true);
      if (existing.isValid()) {
        picker.setStartDate(existing);
        picker.setEndDate(existing);
        picker.updateView();
        picker.updateCalendars();
      }
    });
    const validate = () => {
      const format = withTime ? 'YYYY-MM-DD HH:mm' : 'YYYY-MM-DD';
      const valid = !input.value || window.moment(input.value, format, true).isValid();
      input.setCustomValidity(valid ? '' : document.body.dataset.pickerInvalid);
    };
    input.addEventListener('input', validate);
    input.addEventListener('change', validate);
    validate();
    if (!input.required && !input.disabled && !input.readOnly) {
      const wrap = document.createElement('span');
      wrap.className = 'zu-picker-wrap';
      input.before(wrap);
      wrap.append(input);
      const button = document.createElement('button');
      button.type = 'button'; button.className = 'zu-picker-clear';
      button.textContent = '×';
      button.setAttribute('aria-label', `${document.body.dataset.pickerClear}: ${input.labels?.[0]?.textContent || input.id}`);
      button.addEventListener('click', () => {
        element.data('daterangepicker')?.hide();
        clear(input.id);
        button.focus();
      });
      wrap.append(button);
    }
    input.setAttribute('autocomplete', 'off');
    input.setAttribute('placeholder', withTime ? 'YYYY-MM-DD HH:mm' : 'YYYY-MM-DD');
    input.setAttribute('pattern', withTime
      ? '[0-9]{4}-(0[1-9]|1[0-2])-([0-2][0-9]|3[01]) ([01][0-9]|2[0-3]):[0-5][0-9]'
      : '[0-9]{4}-(0[1-9]|1[0-2])-([0-2][0-9]|3[01])');
  }

  function init(root) {
    (root || document).querySelectorAll('input.zu-date, input.zu-datetime').forEach(initOne);
  }

  function value(id) {
    const input = document.getElementById(id);
    return input && input.value.trim() ? input.value.trim() : null;
  }

  function clear(id) {
    const input = document.getElementById(id);
    if (!input) return;
    input.value = '';
    input.dispatchEvent(new Event('input', { bubbles: true }));
    input.dispatchEvent(new Event('change', { bubbles: true }));
  }

  document.addEventListener('DOMContentLoaded', () => init(document));
  window.ZuLmsPickers = { init: init, value: value, clear: clear };
})();
