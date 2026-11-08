# قرارات المستخدم ومعلّقات التنفيذ

**حُسمت الأسئلة الخمسة في 28 أكتوبر 2026:**

1. ثُبّت `System.Security.Cryptography.Pkcs` على `10.0.13`، أول إصدار مصحّح ضمن سلسلة 10.x بحسب تنبيهي Microsoft المنشورين في 13 أكتوبر 2026: [GHSA-846f-f764-76mx](https://github.com/dotnet/runtime/security/advisories/GHSA-846f-f764-76mx) و[GHSA-2fmv-rv2c-59x7](https://github.com/advisories/GHSA-2fmv-rv2c-59x7). يُقدّم التنبيه الأمني على قاعدة انتظار أسبوعين.
2. اختبار PostgreSQL المحلي يقبل `ZU_TEST_PG` عند غياب Docker. هذا بديل مؤقت؛ علامة قبول مرحلة قاعدة البيانات **معلّقة** حتى ينجح Testcontainers على PostgreSQL 17 في GitHub Actions. أُضيفت بوابة CI، لكن لا يوجد remote لهذا المستودع المحلي، لذلك لم تُشغّل في GitHub بعد.
3. أصبح `sync_review_items.first_seen_run_id` اختيارياً، وأُضيف `origin` بقيم `Sync|ForgotPassword|Provisioning`. فرع البريد المشترك يستخدم `INSERT ... ON CONFLICT DO NOTHING`، ولا يُنشئ `sync_runs` اصطناعياً.
4. تدوير كلمة المرور للموظفين فقط؛ الحساب الخارجي و`Local` مستثنيان. التغيير الإلزامي لا يطلب الكلمة الحالية. الجلسة المحدودة بعمر `auth.access_token_minutes` وبلا refresh token.
5. مولّد الكلمة الأولية يستخدم الطول `clamp(16, min_length, max_length)`، ويضمّن كل صنف مفروض في السياسة. الرموز المسموحة حصراً `!@#%*-_+=?`، مع عشر محاولات وخطأ واضح عند استحالة اجتياز السياسة.

## معلّق تشغيلي

- عامل إرسال `email_outbox` مكتوب ومربوط، لكن لا يوجد تكوين SMTP حقيقي، ولم يُختبر التسليم أو تصنيف الأخطاء مع خادم بريد فعلي. عند غياب إعدادات SMTP يبقى الطابور دون معالجة.
- لا يوجد remote في المستودع المحلي، لذلك لا يمكن بعد إثبات نجاح بوابة GitHub Actions الخاصة بـ PostgreSQL 17.
