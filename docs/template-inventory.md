# جرد مكوّنات التيمبليت

المصدر: `template/paces/` من الأرشيف المرفق. الأصناف التالية ظاهرة في ملفات الأمثلة ويعاد استخدام عناصرها مع ألوان الجامعة.

| المكوّن | ملف المثال | صنف CSS / الأصل |
|---|---|---|
| بطاقة | `ui-cards.html` | `card`, `card-body` |
| جدول | `tables-static.html` | `table`, `table-responsive` |
| جدول مرقّم | `tables-datatables-basic.html` | `table`, `dataTable` |
| نموذج | `form-elements.html` | `form-control`, `form-select` |
| مربع حوار | `ui-modals.html` | `modal`, `modal-dialog` |
| تنبيه | `ui-alerts.html` | `alert` |
| شارة حالة | `ui-badges.html` | `badge` |
| تبويبات | `ui-tabs.html` | `nav`, `nav-tabs` |
| قائمة منسدلة | `ui-dropdowns.html` | `dropdown-menu` |
| صفحات | `ui-pagination.html` | `pagination` |
| تلميح | `ui-tooltips.html` | `tooltip` |
| رسم بياني | `charts-chartjs-bar.html` | `assets/plugins/chartjs/chart.umd.js` |
| اختيار تاريخ ووقت | `form-pickers.html` | `data-provider="flatpickr"` على `input type="text"` |

الأصول المحلية المتاحة تشمل `assets/css/app.min.css`, `assets/css/vendors.min.css`, `assets/plugins/jquery/jquery.min.js`, `assets/plugins/chartjs/chart.umd.js` و`assets/plugins/daterangepicker/daterangepicker.css`.

## المكوّنات المستعملة في التعديل الحالي (2026-11-01)

| المصدر | المقطع/selector | التبعية | التكييف الفعلي |
|---|---|---|---|
| `ui-cards.html` | `.card`, `.card-header`, `.card-body` | Bootstrap المحلي | `site.css`: حدود خفيفة ومسافات 22، بلا ظل |
| `tables-static.html` | `.table-responsive > .table` | Bootstrap المحلي | `site.css`: رأس هادئ وتمرير داخل الجدول |
| `form-elements.html` | `.form-label`, `.form-control`, `.form-select` | Bootstrap المحلي | `site.css`: حقول 44px وfocus كحلي |
| `ui-modals.html` | `.modal-header`, `.modal-body`, `.modal-footer` | Bootstrap المحلي | بُنية مشتركة داخل `<dialog>` الحالي حفاظاً على `showModal()` وPromise |
| `ui-tabs.html` | `.nav-tabs .nav-link` | Bootstrap المحلي | `site.css`: فصلها عن `.sidebar-nav .nav-item` |
| `ui-notifications.html` | Toast dismissal pattern | Bootstrap المحلي، `api.js` | مضيف واحد `#zu-toasts`، `shell.js` يضيف close مترجماً |
| `form-pickers.html` | منتقي تاريخ text | jQuery/Moment/daterangepicker المحلي في التطبيق (وليس flatpickr) | `pickers.js` يقلّد المظهر مع 1-minute precision؛ assets مرة واحدة في layout |

لم يُنقل `app.min.css` أو DataTables أو config/theme scripts؛ الأمثلة مصدر component فقط، لا تطبيق ديمو.
