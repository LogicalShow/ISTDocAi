# تشغيل أداة ISTDocAi.OperatorTool

## تقارير العميل بفترة تواريخ محلية

```text
pages statement --consumer CODE --from YYYY-MM-DD --to YYYY-MM-DD --time-zone egypt|saudi [--pdf FILE | --json] [--company-name NAME] [--force]
pages documents-by-date --consumer CODE --from YYYY-MM-DD --to YYYY-MM-DD --time-zone egypt|saudi [--csv FILE | --json] [--force]
```

الأمرين بيقبلوا `--sql-server SERVER --database DATABASE --trust-server-certificate` وبيقرؤوا قاعدة البيانات بس. المنطقة الزمنية إجبارية كل مرة: `egypt` لتوقيت مصر بقواعد التوقيت الصيفي، أو `saudi` لتوقيت السعودية. التاريخين شاملين بصيغة `yyyy-MM-dd`، والبداية لازم ما تكونش بعد النهاية، وأقصى فترة 3660 يوم شاملين الطرفين. التواريخ المستقبلية مسموحة. بداية الفترة منتصف الليل المحلي ونهايتها منتصف الليل بعد يوم النهاية؛ الحد النهائي مش داخل الفترة. منتصف الليل غير الموجود بيتحول لأول لحظة صحيحة، والمتكرر بيتاخد منه توقيت UTC الأسبق.

من قائمة تقارير العميل اختار 1 للكشف بالتواريخ أو 2 للمستندات بالتواريخ؛ 3 و4 هما تقارير الشحنات القديمة، و5 تقرير واجهة الخدمة. الأداة بتسأل كود العميل ثم المنطقة الزمنية ثم تاريخ البداية والنهاية، والنهاية افتراضيًا النهارده في المنطقة المختارة. الإدخال الغلط بيتسأل تاني. اسم الملف الافتراضي تحت `Documents\ISTDocAi` هو `<consumer>-statement-<from>-to-<to>.pdf` أو `<consumer>-documents-<from>-to-<to>.csv`، وبعده الاتصال لو مش محفوظ. مفيش اختيار تشغيل أو توليد سكريبت للقراءة دي.

الكشف فيه الفترة المحلية وUTC والأرصدة والحركات والاستخدام اليومي. الرصيد من الشحنات والتعديلات والعكس والاستخدام المسجل؛ الصفحات المحجوزة لمستندات لسه بتتعالج مش داخلة. المستندات معناها مرات المعالجة اللي اتحاسبت. الحركات بتقف عند أول 1000 مع تنبيه، والأرصدة بتفضل كاملة. CSV بيكتب تدريجيًا لملف مؤقت وبيستبدل الملف النهائي بعد اكتمال القراءة؛ الفشل بيمسح المؤقت ويحافظ على الملف القديم. JSON بيرفض أكتر من 10000 صف وبيطلب CSV. عدم تحديد ملف معناه JSON. الخيارات `--generate-sql` و`--grant-id` و`--audience` مرفوضة للأمرين دول. `--force` مطلوب لاستبدال ملف موجود.

## القائمة التفاعلية

دبل كليك على الأداة أو شغّلها من غير أوامر عشان تفتح القائمة. كود العميل واتصال قاعدة البيانات بيتحفظوا طول الجلسة. كل قائمة فرعية فيها 0 للرجوع؛ و0 في القائمة الرئيسية للخروج. بعد العملية اضغط إدخال عشان ترجع لنفس القائمة. لو الإدخال خلص، الأداة بتقفل بهدوء.

```text
1) الصفحات والرصيد
   1) الرصيد
   2) شحن صفحات
   3) تعديل الصفحات بالموجب أو السالب
   4) عكس شحنة
   5) تغيير نسبة التحذير
   6) تغيير الوضع: مسبق الدفع أو غير محدود
2) تقارير العميل
   1) كشف العميل بالتواريخ بصيغة PDF
   2) مستندات العميل بالتواريخ بصيغة CSV
   3) كشف العميل حسب الشحنة بصيغة PDF
   4) مستندات العميل حسب الشحنة بصيغة CSV
   5) تقرير العميل من واجهة الخدمة بملف أسرار الحزمة
3) تقارير الشركة الداخلية
   1) التقرير الداخلي بصيغة PDF
   2) المستندات بتكلفة أزور بصيغة CSV
4) الموديلات
   1) عرض الموديلات النشطة
   2) ترقية موديل خطوة بخطوة
   3) فحص موديل أزور للقراءة بس
   4) تجهيز مسودة ربط من غير تفعيل
   5) إعادة فحص مسودة بعد تعديلها
   6) تطبيق ترقية اتراجعت قبل كده — استخدام متقدم
   7) استبدال ربط موديل شغال — استخدام متقدم
   8) تسجيل أول موديل بوليصة — لسيرفر جديد بس
   9) تسجيل أول مجموعة موديلات — لسيرفر جديد بس
5) التجهيز والإدارة
   1) منح مجموعات الدليل النشط صلاحيات المشغّل
   2) إنشاء هوية أزور
   3) تجديد هوية أزور
   4) حذف شهادة أزور القديمة
   5) تجهيز بنية أزور
   6) تصدير قوالب سكربتات قاعدة البيانات
9) عرض كل أوامر سطر الأوامر
c) تغيير اتصال قاعدة البيانات
0) خروج
```

عكس الشحنة وتقارير المستندات بيعرضوا الشحنات بالأرقام، الأحدث الأول، ومع كل شحنة تاريخها بالتوقيت العالمي وعدد الصفحات ورقم المنحة. الشحنة المعكوسة عليها علامة واضحة: ينفع تختارها للتقرير، لكن مينفعش تعكسها تاني. في المستندات، 0 بيختار الفترة اللي قبل أول شحنة لو موجودة؛ ولو مفيش شحنات بتظهر الفترة دي لوحدها. لو فيه أكتر من ٢٠٠ شحنة، تقدر تكتب `id 123` بدل رقم الاختيار. لو قراءة الشحنات فشلت، اكتب رقم المنحة يدويًا، أو `prelude` للمستندات. اختيار الشحنة مش تصريح بالتنفيذ؛ معاينة العملية وتأكيدها لسه مطلوبين.

الأوامر اللي بتدعم `--generate-sql` بتديك اختيار: 1 للتنفيذ دلوقتي على السيرفر، أو 2 لفولدر سكربتات تراجعه وتبعته لمسؤول قاعدة البيانات. تقارير القراءة وأوامر أزور والفحص والمسودة وإعادة الفحص وتقرير واجهة الخدمة مفيهاش الاختيار ده. قائمة الشحنات بتحاول تقرأ قاعدة البيانات الأول حتى لو هتختار سكربت بعد كده؛ لو القراءة فشلت تقدر تدخل رقم المنحة بنفسك.

العمليات الحساسة بتطلب كتابة `yes` بحروف صغيرة؛ المسافات قبلها وبعدها مقبولة، لكن `y` أو `YES` أو الإجابة الفاضية بتلغي. الوضع غير المحدود معناه إن العميل ملوش حد صفحات. في وضع الدفع المسبق من غير شحن موجب، فيه سؤال موافقة على احتمال وقف العميل فورًا؛ معاينة قاعدة البيانات هي اللي بتحسم حالة الرصيد. تعديل الصفحات وعكس الشحنة بيسألوك لو مسموح الرصيد يبقى بالسالب. التقرير الداخلي وتكاليف أزور للاستخدام الداخلي وممنوع يتبعتوا للعميل.

تطبيق ترقية اتراجعت قبل كده بيقرأ نوع المستند والموديل ورمز اللقطة من فولدر المراجعة، وبيستخدم نفس فحص وأمان الترقية الموجّهة. النتيجة الحمرا بترفض التفعيل، والصفرا محتاجة كتابة `yes`. الأداة بتطلب دقة الاختبار المقاسة ومرجع الدليل، وبتجمّد نسخة الربط اللي اتراجعت قبل التنفيذ. راجع الشهادة البديلة على السيرفر قبل حذف القديمة؛ أمر أزور هيطلب تأكيده المعتاد كمان.

> **إزاي تشغّلها:** الأوامر تحت مكتوبة بـ `ISTDocAi.OperatorTool.exe`. لو بتشغّلها من الـ repo من غير publish، استبدل `ISTDocAi.OperatorTool.exe` بـ `dotnet run --project tools/src/ISTDocAi.OperatorTool --` من فولدر الـ repo (مثال: `dotnet run --project tools/src/ISTDocAi.OperatorTool -- pages status --consumer FWD-UNF`). ولو عايز exe لوحده، شوف أوامر الـ publish في [tools/README.md](../README.md).

1. **مين يشغّلها؟** مشغّل IST بحساب Windows مصرح له على قاعدة DocAi. افتح Terminal عادي؛ الأداة `asInvoker` ومش محتاجة elevation. إدارة الـ logins محتاجة صلاحية مسؤول SQL، مش مسؤول Windows. شغّل `ISTDocAi.OperatorTool.exe --help` للتفاصيل. الخروج: 0 نجاح، 1 رفض، 2 إجراء مطلوب، 3 إلغاء.

2. **الاتصال:** حدّد `--sql-server SQL01 --database ISTDocAi` أو سيب الأداة تسألك. الاتصال Windows Authentication وتشفيره Mandatory. `TrustServerCertificate` افتراضيًا no؛ استخدم شهادة موثوقة، أو اختار `--trust-server-certificate` صراحةً لو مسؤول SQL طلب ده. مفيش connection strings أو مفاتيح بتتطبع.

3. **الرصيد والشحن:** الصلاحية المطلوبة `db_owner` على قاعدة DocAi.
   ```powershell
   ISTDocAi.OperatorTool.exe pages status --consumer FWD-UNF
   ISTDocAi.OperatorTool.exe pages topup --consumer FWD-UNF --pages 100 --note 'Approved allowance' --reference ORDER-100
   ISTDocAi.OperatorTool.exe pages set-mode --consumer FWD-UNF --mode Prepaid --top-up-pages 100 --note 'Initial prepaid allowance'
   ISTDocAi.OperatorTool.exe pages set-warning --consumer FWD-UNF --percent 10 --note 'Approved warning threshold'
   ISTDocAi.OperatorTool.exe pages reverse --consumer FWD-UNF --grant-id 123 --note 'Approved reversal'
   ```
   احفظ `RequestId` اللي بيظهر قبل الاتصال. المعاينة بتتراجع تلقائيًا؛ راجع Before → After وRemaining وStatus وبعدها اكتب `yes`. لو الرصيد اتغير، لازم تعاين تاني. عند انقطاع الاتصال، افحص status وأعد نفس الأمر بنفس `--request-id GUID` ونفس البيانات؛ مفيش شحن مكرر. نفس الرقم ببيانات مختلفة بيتِرفض. `adjust --pages -10` لتسوية معتمدة؛ `--allow-debt` تصريح صريح بالرصيد السالب. `--confirm-blocked` في تحويل Prepaid من غير رصيد محتاج تأكيد إضافي لأنه بيوقف العميل فورًا. الشحن مع التحويل في transaction واحدة هو الاختيار الموصى به.

4. **التقارير:** افتراضيًا كشف عميل بصفحات فقط، بدون أسعار أو تكلفة. دور quota يقرأ من SQL؛ API العميل بيتحقق بمفتاح الكيت.
   ```powershell
   ISTDocAi.OperatorTool.exe pages cycles --consumer FWD-UNF --json
   ISTDocAi.OperatorTool.exe pages documents --consumer FWD-UNF --grant-id 0 --limit 100 --csv D:\reports\prelude.csv
   ISTDocAi.OperatorTool.exe pages cycles --consumer FWD-UNF --grant-id 123 --pdf D:\reports\statement.pdf
   ISTDocAi.OperatorTool.exe report customer --base-url https://docai.example.org --kit-secrets D:\Kit\App_Data\docai.secrets.config --grant-id prelude --documents --csv D:\reports\documents.csv
   ```
   اعمل فولدر التقارير الأول. الملفات الموجودة مابتتستبدلش إلا مع `--force`. SQL Documents مع CSV صفحة واحدة؛ استخدم `--after-charge-id` للصفحة التالية. API مع CSV وPDF بيتابع الصفحات لحد 10000 صف ويحذر لو فيه باقي. Grant 0 في SQL أو `prelude` في API = قبل أول شحن. Cycles من غير grant تعرض آخر 200 فترة مع إجماليات كل التاريخ. الشحن المعكوس يفضل حد للفترة؛ الترتيب timestamp ثم grants قبل charges ثم identifier، مش FIFO. كل صفحة snapshot مستقل؛ الاستهلاك المتزامن ممكن يغيّر الإجماليات. المحجوز مش استهلاك نهائي، وإعادة قراءة المستند ممكن تتحسب تاني.

5. **تقرير IST الداخلي:** حدّد `--audience ist` صراحةً مع pages cycles/documents. فيه تقديرات Azure بالدولار USD وعبارة **IST internal — do not send to the customer**. **ممنوع تبعته للعميل.** فاتورة Azure هي المرجع النهائي؛ الصفوف اللي مالهاش سعر مش داخلة في التقدير وبتطلع تحذير. `report customer` دايمًا كشف عميل. الاتصال بالـ API لازم HTTPS origin بشهادة موثوقة، بدون redirects؛ timeout 30 ثانية ومحاولات إضافية بحد أقصى 3 على 429/503 مع Retry-After. المفتاح بيتقرأ من XML ومش بيتبعت للـ renderer. PDF محتاج Arial: `arial.ttf` و`arialbd.ttf`؛ غيابهم يرجّع ACTION NEEDED. المكتبة ثابتة PDFsharp-MigraDoc 6.2.4؛ ممنوع ترقيتها تلقائيًا.

6. **الموديلات:** شغّل بعد اكتمال migrations وبصلاحية `db_owner` على قاعدة DocAi. استخدم دقة اختبار حقيقية وملفات mapping كاملة مراجعة وفيها `ChangeReference`.
   ```powershell
   ISTDocAi.OperatorTool.exe models register-set --bl-model-id bl-v3 --bl-test-field-accuracy 0.97 --bl-mapping-file reviewed-bl.json --manifest-model-id manifest-v3 --manifest-test-field-accuracy 0.92 --manifest-mapping-file reviewed-manifest.json --classifier-model-id router-v3 --classifier-test-field-accuracy 0.91
   ISTDocAi.OperatorTool.exe models register-bl --model-id bl-v4 --test-field-accuracy 0.98 --mapping-file reviewed-bl.json
   ISTDocAi.OperatorTool.exe models mapping --model-id manifest-v3 --document-type-code MANIFEST --mapping-file reviewed-manifest.json
   ```
   راجع SQL المعروض وبعدها `yes`. المجموعة الكاملة بتتسجل وتتفعّل في transaction واحدة؛ أي فشل بيرجع المجموعة كلها. BL افتراضيًا minimum accuracy 0.95، والـ router 0.6. الموديل Active وإعادة نفس الـ mapping مابتغيّرش البيانات؛ المحتوى المختلف بيتِرفض. التراجع للـ BL يتم بـ register-bl ورقم الموديل القديم والدقة المقاسة؛ الربط القديم بيفضل محفوظ. تغيير mapping محتاج ModelId جديد، مش تعديل profile موجود.

7. **صلاحيات المشغّلين ومسار IT:**
   ```powershell
   ISTDocAi.OperatorTool.exe pages topup --consumer FWD-UNF --pages 100 --note 'Approved allowance' --generate-sql D:\it-sql
   ISTDocAi.OperatorTool.exe --export-sql D:\templates
   ```
   أوامر SQL تقبل `--generate-sql FOLDER` بدون اتصال؛ الملفات الموجودة بتترفض. في أوامر الرصيد اللي بتغيّر البيانات، مسؤول IT يشغّل `<RequestId>-preview.sql`، يراجع النتيجة، وينقل PREVIEW_TOKEN إلى `@ReviewedPreviewToken` ويضبط `@OperatorConfirmed=1` في ملف apply. أعِد نفس الملف عند retry. models بتنتج `05-register-models.sql` أو `05-register-model.sql` أو `05-register-model-mapping.sql`. `--export-sql` بيصدّر القوالب المرجعية بدون استبدال.

# أوامر Azure

المشغّل محتاج صلاحيات إنشاء app registrations وservice principals وتعيين الأدوار على الموارد والمجموعة اللي فيها Document Intelligence. تسجيل الدخول بيفتح المتصفح؛ مفيش احتياج لـAzure CLI أو PowerShell في تشغيل المنتج. البرنامج بيعرض الحساب والـtenant قبل أي تغيير.

```text
ISTDocAi.OperatorTool.exe azure identity create --tenant TENANT_GUID --env dev
ISTDocAi.OperatorTool.exe azure infra --tenant TENANT_GUID
```

الإنشاء بيطلب كلمة سر PFX مرتين بشكل مخفي، ومجلد خاص خارج أي repository؛ الافتراضي `%LOCALAPPDATA%\DocAi\backups`. ملف JSON جنب الـPFX بنفس الاسم، وبيستخدمه ServerSetup وDevSetup. صلاحيات ملف الـPFX للمستخدم الحالي وSYSTEM وAdministrators بس. ابعت الـPFX لمسؤول IT وكلمة السر من قناة مختلفة. الاسم الموجود بيترفض؛ استخدم `--client-id GUID` بس لو قاصد تضيف شهادة للتطبيق الموجود.

`azure infra` بيعرض الاشتراكات ويطلب resource group والموقع (الافتراضي francecentral)، dev أو prod، هوية التطبيق أو service principal object id، اسم ومجموعة DI الموجود، مجموعات SQL والمطورين، التنبيهات والميزانية وتاريخ بدايتها وpurge protection. وضع الاستضافة ثابت `onPremises`. راجع ملخص what-if قبل التأكيد؛ الموارد لها تكلفة. الميزانية للتنبيه ومش سقف للصرف. تفعيل purge protection نهائي. البرنامج بيحفظ outputs JSON بدون مفاتيح في المسار اللي تختاره؛ استخدمه مع ServerSetup `package`. لو المجموعة مش موجودة، إنشاء المجموعة له تأكيد منفصل حتى مع `--yes`.

التجديد السنوي قبل انتهاء الشهادة بـ30 يوم على الأقل:

```text
ISTDocAi.OperatorTool.exe azure identity renew --tenant TENANT_GUID --client-id CLIENT_GUID
ISTDocAi.ServerSetup.exe identity-renew
ISTDocAi.ServerSetup.exe check
ISTDocAi.OperatorTool.exe azure identity remove-old --tenant TENANT_GUID --client-id CLIENT_GUID --thumbprint OLD_HEX
```

شغّل `identity-renew` و`check` على السيرفر من Terminal كمسؤول، وراجع `/health/ready` وجرّب استخراج جديد قبل `remove-old`. التجديد بيحتفظ بالشهادات العامة الموجودة، والسيرفر بيحتفظ بالشهادة القديمة. ما تشيلش الشهادة اللي السيرفر لسه بيستخدمها؛ آخر credential ما ينفعش يتشال. نفّذ تغييرات credentials بالتتابع من مشغّل واحد: البرنامج بيرفض أي تغيير متزامن رصده قبل PATCH، لكن Graph مش بيوفر transaction للخطوات دي. لو الاتصال اتقطع، راجع حالة Azure وملفات التصدير قبل إعادة المحاولة؛ البرنامج مش بيعيد POST تلقائيًا.

`--yes` بيتخطّى التأكيد النهائي فقط؛ مش بيتخطّى الأسئلة أو فحص كلمات السر أو تأكيد إنشاء resource group. في التجديد اسم ملفات التصدير بيشمل thumbprint علشان يحتفظ بالنسخة القديمة. الملفات الموجودة ما بيتكتبش فوقها.


## ترقية موديل واحد بعد المراجعة (Phase A)

لو هتغيّر موديل واحد، استخدم الأوامر دي بدل `register-set` عشان باقي المجموعات تفضل زي ما هي. الأمر ده مبيعملش اتصالات بأزور، وموجود في قائمة الموديلات.

```text
models active --document-type-code MANIFEST --json
models upgrade-extract --document-type-code MANIFEST --model-id manifest-new --test-field-accuracy 0.99 --mapping-file manifest.json --expected-snapshot TOKEN
models upgrade-extract --document-type-code BL --model-id bl-new --test-field-accuracy 0.99 --mapping-file bl.json --expected-snapshot TOKEN
models active --document-type-code AUTO --json
models upgrade-classifier --model-id router-new --test-accuracy 0.99 --min-confidence 0.5 --bill-class-name bill --manifest-class-name manifest --other-class-name other --expected-snapshot TOKEN
```

ابدأ بـ`models active` وخد `SNAPSHOT_TOKEN` بعد مراجعة الموديل والـmapping أو الـroutes. لو مفيش موديل Active، الـsnapshot بتعرض `Model: null`؛ راجعها برضه قبل أول تفعيل. الـextract يقبل `--shipping-line LINE`، ولازم تستخدم نفس المجموعة في القراءة والترقية. `AUTO` ملوش shipping line؛ `--bill-shipping-line` بيحدد مسار فئة BL فقط. الـmapping لازم يبقى بنفس envelope المستخدم في `models mapping`. حد الدقة الافتراضي للـextract هو `0.95`، وتقدر تغيّره بـ`--minimum-accuracy`؛ حد الـrouter الافتراضي `0.6` وتقدر تغيّره بـ`--router-minimum-accuracy`. `--evidence TEXT` اختيارية وبتظهر في المعاينة.

الـtoken هي SHA-256 لحروف JSON الـsnapshot بترميز UTF-16LE بدون BOM، بترتيب خصائص ثابت وصفوف مرتبة بـBIN2، مع إظهار nulls. بتشمل المجموعة والموديل وعتباته وإصداره ووقت تفعيله والـmapping بكل صفوفه وChangeReference وProfileId وmapping fingerprint (algorithm 2)، أو routes المصنّف. مفيش وقت قراءة متغيّر. حقل `SNAPSHOT_TOKEN` نفسه خارج المحتوى المحسوب. كل قراءة وفحص للـtoken بياخدوا نفس الأقفال داخل transaction؛ لو الحالة اتغيّرت، الترقية بتترفض وبتحتاج `models active` من جديد.

راجع المعاينة وبعدين اكتب `yes`. التفعيل لوحده مش بيعيد قراءة مستندات؛ لو اترفعت تاني وثيقة اتقرت بالموديل القديم، بتتقري بالجديد وبتتحاسب، إلا لو page quota منعت إعادة القراءة.

للـIT، أضف `--generate-sql FOLDER`: مفيش اتصال بقاعدة البيانات. شغّل `model-upgrade-preview.sql` الأول؛ المعاينة بتعمل rollback. انسخ `PREVIEW_TOKEN` في `@ReviewedPreviewToken` داخل `model-upgrade.sql` واضبط `@OperatorConfirmed=1` بعد المراجعة. أي تغيير في الـsnapshot أو معطيات الترقية بيرفض التنفيذ. لازم ModelId جديد؛ الموجود بيترفض. إعادة نفس script بعد نجاحه بتترفض بالـsnapshot القديمة من غير أي تغيير إضافي. كل التسجيل والربط والتفعيل atomic، بصلاحية `db_owner` على قاعدة DocAi. الأدلة بتظهر في نتائج المعاينة؛ احتفظ بيها مع scripts المراجعة.
## فحص الموديل وتجهيز مسودة الترقية (Phase B)

```text
models inspect --document-type-code MANIFEST --model-id manifest-new --endpoint https://RESOURCE.cognitiveservices.azure.com --json
models draft --document-type-code MANIFEST --model-id manifest-new --endpoint https://RESOURCE.cognitiveservices.azure.com --sql-server SERVER --database ISTDocAi --output C:\Review\manifest-new
```

الأمرين بيفتحوا تسجيل دخول في المتصفح؛ حسابك محتاج دور `Cognitive Services User` على مورد Document Intelligence نفسه. استخدم custom subdomain من غير path أو query؛ تقدر تحدد `--tenant GUID`. لو extract model فيه أكتر من docType، لازم `--doc-type NAME`. `inspect` بيعرض التواريخ، الأنواع وشجرة الحقول وconfidence من Azure؛ القيمة الناقصة بتظهر `unavailable`.

`draft` بيقرأ snapshot للموديل النشط من SQL، وبيقرأ schema القديم والجديد من نفس مورد Azure عشان يقارن الأنواع. لازم الموديل القديم يفضل موجود في المورد؛ مفيش offline mode. بيحافظ على `SNAPSHOT_TOKEN`، ومش بيكتب حاجة في قاعدة البيانات. اختار `--shipping-line LINE` لمجموعة extract لو مطلوبة. `AzureName` اسم property حرفي: `Field/Header` من الجذر و`Bill/Container` من عناصر المصفوفة؛ النقطة مش مسار متداخل. الاسم والنوع المتوافقين بيتنقلوا زي ما هم، والمفقود بيتسجل removed، وتغيير النوع بيتسجل removed + new. الحقول الجديدة بتحتاج اختيار canonical key أو `ignore`؛ مفيش تخمين لتغيير الأسماء. كسر selectors بيمنع المسودة.

هيسألك عن `ChangeReference` مطلوب، وعن ربط الحقول الجديدة. مع `AUTO` بيحافظ على routes للفئات الموجودة وبيسألك عن بديل الفئات المتغيرة، وبيسيب bill shipping line القديم إلا لو غيرته. `draft-classifier.json` بيحفظ خيارات routes؛ أمر `upgrade-classifier` المطبوع بيحتويها مباشرة.

المجلد الافتراضي `%USERPROFILE%\Documents\ISTDocAi\models\<yyyyMMdd-HHmmss>-<NEW>`، وأي مجلد موجود وفيه ملفات بيترفض. راجع `review.txt` و`review.json` و`draft-mapping.json` (أو `draft-classifier.json`). لو الإدخال redirected، مفيش أسئلة ولا draft قابل للتنفيذ: ملفات مراجعة بس وexit `2`. الـblocked أو الإجابات الناقصة برضه exit `2` من غير draft. رفض الموديل المفقود/المنتهي أو الدور الناقص exit `1`، وفشل الاتصال بـAzure exit `2`.

بعد المراجعة استخدم أمر الترقية المطبوع بنفس snapshot token؛ دقة الاختبار لازم تكون القيمة المقاسة فعليًا من فريق الموديل. فحص أثر التعديل على الحزمة وتطبيق الترقية متاحين من قائمة الموديلات.
## ترقية موديل — قائمة الموديلات، اختيار 2

اختار BL أو MANIFEST أو AUTO واكتب ID الموديل الجديد. الأداة بتسأل عن Document Intelligence endpoint والـtenant الاختياري مرة في الجلسة، وعن اتصال SQL لو مش محفوظ. سجّل دخول بحساب عنده Cognitive Services User. لو Azure مش متاح أو الصلاحية مرفوضة أو الموديل منتهي، الترقية بتترفض ومفيش مسار offline. حتى إخراج SQL محتاج مراجعة Azure وقراءة snapshot الأول.

مرحلة draft بتقارن القديم بالجديد، وتسألك تربط الحقول الجديدة بأنهي canonical keys أو تتجاهلها، وعن ChangeReference. افتح draft-mapping.json أو draft-classifier.json في فولدر المراجعة وعدّل لو محتاج، وبعدها اضغط Enter. الأداة بتقرأ الملف من جديد وبتتحقق منه وبتكتب verdict.txt وverdict.json.

- GREEN: مفيش تأثير مكتشف على حقول kit؛ تغيير classifier كامل الـroutes ما بيغيرش حقول النتيجة.
- BLUE: مفتاح جديد مسموح في dictionary مفتوحة، لكن مش ضمن حقول الحفظ المعروفة للـkit. الخدمة تقبله؛ علشان يظهر ويتحفظ في الوجهة المطلوبة، عدّل الـkit واعمل publish. هتأكد الاستمرار. Extras العامة مش بديل لحقل حفظ typed.
- YELLOW: مفتاح كان mapping القديم بينتجه والـkit بيستخدمه اختفى. راجع قائمة المفاتيح واكتب الكلمة yes صراحة بعد القائمة.
- RED: مفتاح خارج properties الثابتة أو مشكلة structural/blocked أو routes ناقصة. التفعيل مرفوض؛ راجع التغييرات المطلوبة في عقد ISTDocAi والـkit وانشر التعديلات اللازمة قبل المحاولة تاني.
- WHITE: حقول Azure أو classes جديدة متجاهلة؛ معلومة للمراجعة والتأكيد.

اكتب measured test accuracy بين 0 و1 وevidence reference مطلوب (لحد 400 حرف printable، من غير SQLCMD substitutions). Azure estimate متوسط fieldConfidence المتاح للحقول اللي الـmapping النهائي بيستخدمها، مش دقة اختبار فعلية. لو مفيش قيم أو الموديل classifier، هيظهر unavailable. فرق أكبر من 0.05 (خمس نقاط مئوية) بيطلع تحذير بالرقمين وبيطلب تأكيد الاستمرار؛ فرق 0.05 بالضبط مش بيطلع التحذير.

الملخص بيعرض القديم والجديد والـverdict وتنبيه الشحن: التفعيل لوحده مش بيعيد قراءة المستندات؛ إعادة رفع مستند اتقري بالموديل القديم ممكن تتقري بالموديل الجديد وتتحاسب، إلا لو quota منعت إعادة القراءة. بعدها اختار 1 للتشغيل على SQL أو 2 لإنشاء script لـIT. التنفيذ بيستخدم expected-snapshot ومراجعة preview الحالية؛ لو الموديل النشط اتغير الترقية تترفض.

لإعادة التحقق من ملف معدل محليًا:

```powershell
ISTDocAi.OperatorTool.exe models check --document-type-code BL --review "C:\Reviews\model-upgrade"
ISTDocAi.OperatorTool.exe models check --document-type-code MANIFEST --review "C:\Reviews\manifest" --mapping-file "C:\Reviews\manifest\edited.json"
```

Exit code: 0 لـGREEN/BLUE/WHITE، و2 لـYELLOW المحتاج acknowledgement، و1 لـRED. لازم review صادر من draft بيحفظ ActiveSnapshot؛ لو فولدر قديم مفيهوش snapshot اعمل draft جديد. الأمر check بيعيد ModelSql.Binding validation على الملف النهائي، ومش بيعتبر verdict قديم موافقة. BL container schema مفتوحة حسب JSON Schema، لكن DTO المنتج خصائصه ثابتة؛ إضافة خاصية حاوية جديدة محتاجة مراجعة وتعديل projection/DTO المنتج حتى لو اللون BLUE.



### Azure cost per page (IST internal)

```text
report azure-cost --from YYYY-MM-DD --to YYYY-MM-DD --time-zone egypt|saudi [--consumer CODE] [--pdf FILE | --csv FILE | --json] [--force] [--sql-server SERVER --database DB --trust-server-certificate]
```

IST internal ? do not send to the customer. Omit consumer for all customers. No audience or SQL-script option. Menu: IST reports ? 1) Azure cost per page by dates (PDF or CSV), 2) IST internal report by top-up (PDF), 3) IST documents with Azure cost by top-up (CSV), 0) Back. The time zone is requested every time.

This is a PAYG list-price ESTIMATE; the Azure invoice is authoritative. Commitment tiers can exceed the estimate; discounts can differ. Charged cohorts use charge recording dates and ALL usage for the same consumer/job/run. Uncharged usage uses occurrence dates and excludes any run with a recorded charge at report time. Frozen costs are retained; missing prices are recovered by usage date when possible. Unresolved pages make per-page values incomplete (*); zero charged pages displays '-'. Totals divide total charged estimated cost by total charged pages. Current price list dates are effective dates, not retrieval timestamps; future prices are shown separately.


### Customer priced usage statement

```text
report customer-price --consumer CODE --from YYYY-MM-DD --to YYYY-MM-DD --time-zone egypt|saudi --price-per-page AMOUNT --currency EGP|USD|SAR [--pdf FILE | --csv FILE | --json] [--company-name NAME] [--force] [--sql-server SERVER] [--database DB] [--trust-server-certificate]
```

All six selection/rate options are required. The rate uses invariant decimal syntax, is greater than zero and at most 100000, with at most four decimal places. Currency is EGP, USD or SAR; each uses two decimals. Each charged processing run is priced separately and rounded to two decimals away from zero; totals and daily amounts sum those rounded lines. This is a usage statement at the entered rate, not a tax invoice. No output option prints JSON (maximum 10000 documents); PDF shows the first 1000 documents; CSV streams the full list with a TOTAL row. Company names use the existing customer-safe text rules.

Customer reports menu: 1 date statement PDF; 2 date documents CSV; 3 priced date statement PDF/CSV; 4 top-up statement PDF; 5 top-up documents CSV; 6 service API report; 0 Back. Choice 3 asks for consumer, time zone, dates, currency, rate, format, destination and connection. Zone and currency are selected every time.

?? ????? ?????? ?????? ????? 3? ???? ??????? ??????? ?????????? ?? ?????? ???? ?????? ??? ???. ???????? ????? ????? ????????? ??? ????? ?? ????? ????????? ????? ??? ?????? ??????.

### ????? ????? Azure

????????? ??????? ????? ????? `"AzurePriceRefresh": { "Enabled": true, "Interval": "12:00:00", "StartupDelay": "00:01:00" }` ??? ??????? ???????. ???? ?????? HTTPS ???? ??? `prices.azure.com` ?? ???? IIS app-pool. ??????? ??????? ???? ?? ????? ?????? ?????? UTC? ??? ??? ??????? ????? ????? ????????? ???????? ???????.

## متابعة أسعار Azure — قراءة فقط

الأمر `prices show [--json] --sql-server SERVER --database ISTDocAi` أو القائمة **3) IST reports → 4) Azure prices (read only)** يعرض السعر الحالي والأسعار المجدولة ومصدر السعر وآخر فحص مكتمل وآخر تغيير لكل عداد. الأداة لا تكتب أسعارًا أو سجل فحوصات، ولا تدعم `--generate-sql` لهذا الأمر.

بعد ضبط الاتصال أو تغييره، تظهر رسالة فوق القائمة الرئيسية لو آخر نتيجة Refused أو Failed، مع بداية سلسلة الفشل المستمرة. تختفي بعد نتيجة NoChange أو Updated لاحقة لنفس العداد. يظهر تحذير أيضًا لو آخر فحص مكتمل أقدم من 26 ساعة. تقرير تكلفة Azure يعرض نفس التحذيرات في الشاشة وPDF وعمود CSV باسم `priceCheckWarnings`. لا يظهر بانر لو المهمة لم تعمل بعد أو جدول السجل غير موجود أو تعذرت القراءة؛ شاشة الأسعار توضح عدم توفر السجل أو مشكلة صلاحية SELECT.

المهمة تعمل كل 12 ساعة عند تفعيلها، وتكون معطلة على أجهزة التطوير. لا يوجد شرط رفض عند تغير السعر بنسبة 50%. عند التحذير راجع https://prices.azure.com وصفحة تسعير Azure، واتصال الشبكة والجدار الناري والبروكسي، وتفعيل المهمة ونشر الخدمة والصلاحيات. لو هناك تعارض راجع السعر الموجود أو المجدول قبل أي تعديل.

لو احتجت سعرًا يدويًا، مسؤول قاعدة البيانات المخول يضيف صفًا بتاريخ EffectiveFrom **مستقبلي حسب UTC**. المثال التالي للتوضيح فقط؛ استبدل التاريخ والسعر بعد التحقق:

```sql
INSERT dbo.AzureMeterPrices(MeterCode, EffectiveFrom, PricePer1000, Currency, Source)
VALUES ('DI_CUSTOM_EXTRACT', '2099-01-01', 35.0000, 'USD', 'Manual price verified by operator');
```

المهمة لا تستبدل أي صف موجود. على السيرفر القديم تظهر رسالة أن سجل الفحص غير متاح لأن الخدمة لم تُحدّث بعد؛ اتبع إجراءات النشر المعتادة لتحديث الخدمة والمهاجرات.

المشغّلون أو فريق IT محتاجين Windows login بصلاحية `db_owner` على قاعدة بيانات DocAi.
# نشر الأدوات

افتح `ISTDocAi.sln`، وخلي `ISTDocAi.Tools` هو Startup ودوس F5 أو Ctrl+F5 لقائمة النشر بس. اختار الأداة أو All four؛ الناتج في `tools/publish/<Name>` وبيستبدل القديم، فاقفل الـ exe القديم الأول. لازم نسخة الريبو والـ .NET SDK. All four بيكمل باقي الأدوات لو واحدة فشلت وبيطبع ملخص، وبعد النشر تقدر تفتح الفولدر أو تشغل الأداة.

CLI من غير أسئلة: `dotnet run --project tools/src/ISTDocAi.Tools -- publish OperatorTool`، أو `publish all`. للـ Debug خلي مشروع الأداة نفسها Startup ودوس F5؛ DevSetup محتاج Visual Studio كـ Administrator.
