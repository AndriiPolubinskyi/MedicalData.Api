using Microsoft.JSInterop;

namespace MedicalData.Client.Services;

public class LocalizationService(IJSRuntime js)
{
    private string _lang = "uk";

    public string Lang => _lang;
    public event Action? LanguageChanged;

    public async Task InitAsync()
    {
        try
        {
            var saved = await js.InvokeAsync<string?>("localStorage.getItem", "app_lang");
            if (saved is "uk" or "en" or "ru") _lang = saved;
        }
        catch { }
    }

    public async Task SetLangAsync(string lang)
    {
        if (_lang == lang) return;
        _lang = lang;
        try { await js.InvokeVoidAsync("localStorage.setItem", "app_lang", lang); } catch { }
        LanguageChanged?.Invoke();
    }

    // ── Lookup ────────────────────────────────────────────────────────────────
    public string this[string key] =>
        _dict.TryGetValue(_lang, out var d) && d.TryGetValue(key, out var v) ? v :
        _dict["uk"].TryGetValue(key, out var fb) ? fb : key;

    public string Format(string key, params object[] args) =>
        string.Format(this[key], args);

    // ── Date helpers ──────────────────────────────────────────────────────────
    public string FormatDate(DateTime d) =>
        _lang == "en"
            ? $"{d:MMMM d, yyyy}"
            : $"{d.Day} {_months[_lang][d.Month - 1]} {d.Year}";

    public string ShortDate(DateTime d) =>
        _lang == "en"
            ? $"{d:MMM d}"
            : $"{d.Day} {_months[_lang][d.Month - 1]}";

    private static readonly Dictionary<string, string[]> _months = new()
    {
        ["uk"] = ["січня","лютого","березня","квітня","травня","червня",
                  "липня","серпня","вересня","жовтня","листопада","грудня"],
        ["ru"] = ["января","февраля","марта","апреля","мая","июня",
                  "июля","августа","сентября","октября","ноября","декабря"],
        ["en"] = ["January","February","March","April","May","June",
                  "July","August","September","October","November","December"],
    };

    // ── Lab group display names ───────────────────────────────────────────────
    public string GetGroupName(string ukName) =>
        _groupNames.TryGetValue(_lang, out var d) && d.TryGetValue(ukName, out var v) ? v : ukName;

    private static readonly Dictionary<string, Dictionary<string, string>> _groupNames = new()
    {
        ["uk"] = new()
        {
            ["Загальний аналіз крові"]    = "Загальний аналіз крові",
            ["Ліпідограма"]               = "Ліпідограма",
            ["Біохімія"]                  = "Біохімія",
            ["Гормони та ендокринологія"] = "Гормони та ендокринологія",
            ["Онкомаркери"]               = "Онкомаркери",
            ["Інфекційна серологія"]      = "Інфекційна серологія",
            ["Інше"]                      = "Інше",
        },
        ["en"] = new()
        {
            ["Загальний аналіз крові"]    = "Blood Count",
            ["Ліпідограма"]               = "Lipid Profile",
            ["Біохімія"]                  = "Biochemistry",
            ["Гормони та ендокринологія"] = "Hormones",
            ["Онкомаркери"]               = "Tumor Markers",
            ["Інфекційна серологія"]      = "Serology",
            ["Інше"]                      = "Other",
        },
        ["ru"] = new()
        {
            ["Загальний аналіз крові"]    = "Общий анализ крови",
            ["Ліпідограма"]               = "Липидограмма",
            ["Біохімія"]                  = "Биохимия",
            ["Гормони та ендокринологія"] = "Гормоны",
            ["Онкомаркери"]               = "Онкомаркеры",
            ["Інфекційна серологія"]      = "Серология",
            ["Інше"]                      = "Прочее",
        },
    };

    // ── Translation dictionaries ──────────────────────────────────────────────
    private static readonly Dictionary<string, Dictionary<string, string>> _dict = new()
    {
        ["uk"] = new()
        {
            ["brand"]              = "Мої аналізи",
            ["logout"]             = "Вийти",
            ["loading"]            = "Завантаження…",
            ["generating"]         = "Генерую висновок…",

            ["tab.dashboard"]      = "Дашборд",
            ["tab.upload"]         = "PDF",
            ["tab.history"]        = "Історія",
            ["tab.charts"]         = "Графіки",

            ["stat.lastDate"]      = "Останній аналіз",
            ["stat.totalCount"]    = "Показників відстежується",
            ["stat.abnCount"]      = "Відхилень (остання дата)",
            ["stat.normal"]        = "✓ Норма",
            ["section.categories"] = "Групи показників",
            ["section.aiSummary"]  = "ШІ-аналіз останніх результатів",
            ["ai.btn"]             = "🤖 Отримати висновок ІІ",
            ["ai.loading"]         = "⏳ Генерую…",
            ["ai.header"]          = "🤖 Висновок ШІ",
            ["ai.failed"]          = "Не вдалося отримати висновок.",
            ["ai.error"]           = "Помилка при зверненні до ШІ.",
            ["ai.empty"]           = "Порожня відповідь.",
            ["ai.short"]           = "🤖 ШІ",
            ["ai.analyze"]         = "🤖 Аналіз ШІ",
            ["ai.explain"]         = "Пояснення ШІ",
            ["cat.count"]          = "показн.",
            ["empty.noData"]       = "Немає аналізів. Завантажте перший PDF!",
            ["empty.upload"]       = "📄 Завантажити PDF",

            ["upload.strip.hint"]  = "Оновити результати:",
            ["upload.strip.btn"]   = "Завантажити PDF",
            ["upload.title"]       = "Завантаження PDF аналізів",
            ["upload.choose"]      = "Оберіть PDF файл",
            ["upload.hint"]        = "PDF до 5 МБ",
            ["upload.parse"]       = "Розібрати PDF",
            ["upload.parsing"]     = "⏳ Розпізнаю…",
            ["upload.save"]        = "💾 Зберегти в базу",
            ["upload.saving"]      = "⏳ Зберігаю…",
            ["upload.notFound"]    = "Показники не знайдено в PDF.",
            ["upload.tooLarge"]    = "Файл завеликий (макс. 5 МБ).",
            ["upload.found"]       = "Розпізнано {0} показників. Перевірте і збережіть.",
            ["upload.saved"]       = "✅ Збережено {0} показників.",
            ["upload.saveFailed"]  = "Помилка збереження: {0}",
            ["upload.error"]       = "Помилка: {0}",

            ["tbl.indicator"]      = "Показник",
            ["tbl.value"]          = "Значення",
            ["tbl.unit"]           = "Од.",
            ["tbl.range"]          = "Норма",

            ["history.title"]          = "Історія аналізів",
            ["share.latest"]           = "🔗 Останній",
            ["share.all"]              = "🔗 Усі",
            ["share.latestScope"]      = "останній аналіз",
            ["share.allScope"]         = "всі аналізи",
            ["share.copy"]             = "📋 Копіювати",
            ["filter.label"]           = "Фільтр:",
            ["history.empty"]          = "Немає збережених аналізів. Завантажте перший PDF.",
            ["history.emptyCategory"]  = "Немає аналізів у категорії «{0}».",
            ["col.date"]               = "Дата",
            ["col.indicators"]         = "Показники",
            ["col.deviations"]         = "Відхилення",
            ["delete.confirm"]         = "Видалити всі аналізи за {0} {1}?",

            ["hero.eyebrow"]    = "✨ AI-аналіз",
            ["hero.title"]      = "Розшифруйте свої аналізи за допомогою AI",
            ["hero.sub"]        = "Завантажте PDF з лабораторними результатами та отримайте зрозуміле пояснення кожного показника.",
            ["hero.badge.ai"]   = "AI висновок",
            ["hero.badge.norm"] = "Норма",
            ["ai.conclusion"]   = "AI висновок",
            ["ai.deviation.summary"] = "відхилень від норми. Натисніть для повного AI-аналізу.",
            ["ai.normal.summary"]    = "Показники в нормі. Натисніть для детального AI-аналізу.",
            ["col.status"]      = "Статус",
            ["col.norm"]        = "Норма",
            ["status.warn"]     = "⚠ Відхилення",
            ["status.ok"]       = "✓ Норма",
            ["dv.high"]         = "▲ Підвищений",
            ["dv.low"]          = "▼ Знижений",
            ["dv.other"]        = "⚠ Відхилення",

            ["charts.title"]    = "Динаміка показника",
            ["charts.empty"]    = "Немає даних. Спочатку завантажте аналізи.",
            ["charts.select"]   = "— оберіть показник —",
            ["charts.xLabel"]   = "Дата",

            ["profile.title"]       = "Мій профіль",
            ["profile.sex"]         = "Стать",
            ["profile.female"]      = "Жінка",
            ["profile.male"]        = "Чоловік",
            ["profile.birthdate"]   = "Дата народження",
            ["profile.years"]       = "р.",
            ["profile.cycleday"]    = "День циклу (поточний)",
            ["profile.cycleday.hint"] = "Від 1 до 28. AI враховуватиме при розшифровці гормонів.",
            ["profile.save"]        = "Зберегти",
            ["profile.saved"]       = "Збережено",

            ["login.subtitle"]  = "Персональний журнал лабораторних досліджень",
            ["login.google"]    = "Увійти через Google",
            ["login.google.short"] = "Увійти",
            ["demo.try"]        = "Спробувати демо",

            ["landing.hero.title"]       = "Розшифруйте свої аналізи",
            ["landing.hero.title.accent"]= "за секунди",
            ["landing.hero.sub"]         = "Завантажте PDF з будь-якої лабораторії — AI пояснить кожен показник простою мовою, без медичного жаргону.",
            ["landing.hero.cta"]         = "Завантажити PDF",
            ["landing.hero.labs"]        = "Synevo, Діла, CSD, Invivo та інші лабораторії",
            ["landing.trust.label"]      = "Підтримує результати з",
            ["landing.trust.more"]       = "та інші",
            ["landing.how.eyebrow"]      = "Як це працює",
            ["landing.how.title"]        = "Три кроки до ясності",
            ["landing.step1.title"]      = "Завантажте PDF",
            ["landing.step1.sub"]        = "Оберіть файл з будь-якої лабораторії",
            ["landing.step2.title"]      = "AI аналізує",
            ["landing.step2.sub"]        = "Розпізнає показники й порівнює з нормами",
            ["landing.step3.title"]      = "Бачиш результати",
            ["landing.step3.sub"]        = "Зрозумілі пояснення та динаміка в часі",
            ["landing.features.eyebrow"] = "Можливості",
            ["landing.features.title"]   = "Все що потрібно",
            ["landing.f1.title"]         = "AI пояснення",
            ["landing.f1.sub"]           = "Кожен показник — зрозумілою мовою, без медичного жаргону",
            ["landing.f2.title"]         = "Всі лабораторії",
            ["landing.f2.sub"]           = "Synevo, Діла, CSD, Invivo та будь-який стандартний PDF",
            ["landing.f3.title"]         = "Динаміка",
            ["landing.f3.sub"]           = "Відстежуйте зміни в показниках з часом на графіках",
            ["landing.f4.title"]         = "Безпечно",
            ["landing.f4.sub"]           = "Дані зберігаються тільки у вашому акаунті",
            ["landing.cta.title"]        = "Готові перевірити свої аналізи?",
            ["landing.cta.sub"]          = "Безкоштовно. Без кредитної картки.",
            ["landing.cta.btn"]          = "Почати зараз",
            ["landing.cta.demo"]         = "Або спробуйте демо →",
            ["landing.demo.title"]       = "Аналіз крові",
            ["landing.demo.date"]        = "05.06.2025",
            ["landing.demo.m1"]          = "Гемоглобін",
            ["landing.demo.m2"]          = "Лейкоцити",
            ["landing.demo.m3"]          = "Глюкоза",
            ["landing.demo.m4"]          = "Феритин",
            ["landing.demo.ai"]          = "Феритин знижений — можлива анемія",
            ["landing.demo.warn"]        = "Відхилення",
            ["demo.banner"]     = "Демо-режим — дані приклади, не ваші. Увійдіть, щоб зберегти свої аналізи.",
            ["demo.login"]      = "Увійти",

            ["paywall.title"]   = "AI-кредити вичерпані",
            ["paywall.sub"]     = "Ви використали всі безкоштовні розшифровки. Поповніть кредити, щоб продовжити.",
            ["paywall.pack3"]   = "Стартовий",
            ["paywall.pack10"]  = "Стандарт",
            ["paywall.pack25"]  = "Про",
            ["paywall.credits"] = "розшифровки",
            ["paywall.popular"] = "Популярний",
            ["paywall.contact"] = "Для оплати пишіть:",
            ["paywall.close"]   = "Закрити",
        },

        ["en"] = new()
        {
            ["brand"]              = "My Lab Results",
            ["logout"]             = "Sign out",
            ["loading"]            = "Loading…",
            ["generating"]         = "Generating summary…",

            ["tab.dashboard"]      = "Dashboard",
            ["tab.upload"]         = "Upload",
            ["tab.history"]        = "History",
            ["tab.charts"]         = "Charts",

            ["stat.lastDate"]      = "Latest test",
            ["stat.totalCount"]    = "Tracked metrics",
            ["stat.abnCount"]      = "Abnormal (latest)",
            ["stat.normal"]        = "✓ Normal",
            ["section.categories"] = "Test groups",
            ["section.aiSummary"]  = "AI analysis of latest results",
            ["ai.btn"]             = "🤖 Get AI summary",
            ["ai.loading"]         = "⏳ Generating…",
            ["ai.header"]          = "🤖 AI Summary",
            ["ai.failed"]          = "Failed to get summary.",
            ["ai.error"]           = "Error contacting AI.",
            ["ai.empty"]           = "Empty response.",
            ["ai.short"]           = "🤖 AI",
            ["ai.analyze"]         = "🤖 AI Analysis",
            ["ai.explain"]         = "AI explanation",
            ["cat.count"]          = "metrics",
            ["empty.noData"]       = "No tests yet. Upload your first PDF!",
            ["empty.upload"]       = "📄 Upload PDF",

            ["upload.strip.hint"]  = "Update results:",
            ["upload.strip.btn"]   = "Upload PDF",
            ["upload.title"]       = "Upload PDF Lab Results",
            ["upload.choose"]      = "Choose a PDF file",
            ["upload.hint"]        = "PDF up to 5 MB",
            ["upload.parse"]       = "Parse PDF",
            ["upload.parsing"]     = "⏳ Parsing…",
            ["upload.save"]        = "💾 Save to database",
            ["upload.saving"]      = "⏳ Saving…",
            ["upload.notFound"]    = "No lab results found in PDF.",
            ["upload.tooLarge"]    = "File too large (max 5 MB).",
            ["upload.found"]       = "Found {0} metrics. Review and save.",
            ["upload.saved"]       = "✅ Saved {0} metrics.",
            ["upload.saveFailed"]  = "Save error: {0}",
            ["upload.error"]       = "Error: {0}",

            ["tbl.indicator"]      = "Indicator",
            ["tbl.value"]          = "Value",
            ["tbl.unit"]           = "Unit",
            ["tbl.range"]          = "Reference",

            ["history.title"]          = "Lab History",
            ["share.latest"]           = "🔗 Latest",
            ["share.all"]              = "🔗 All",
            ["share.latestScope"]       = "latest results",
            ["share.allScope"]          = "all results",
            ["share.copy"]             = "📋 Copy",
            ["filter.label"]           = "Filter:",
            ["history.empty"]          = "No saved tests. Upload your first PDF.",
            ["history.emptyCategory"]  = "No tests in category \"{0}\".",
            ["col.date"]               = "Date",
            ["col.indicators"]         = "Metrics",
            ["col.deviations"]         = "Abnormal",
            ["delete.confirm"]         = "Delete all tests for {0} {1}?",

            ["hero.eyebrow"]    = "✨ AI analysis",
            ["hero.title"]      = "Decode your lab results with AI",
            ["hero.sub"]        = "Upload a PDF with your lab results and get a plain-language explanation of every metric.",
            ["hero.badge.ai"]   = "AI summary",
            ["hero.badge.norm"] = "Normal",
            ["ai.conclusion"]   = "AI summary",
            ["ai.deviation.summary"] = "abnormal values. Click for full AI analysis.",
            ["ai.normal.summary"]    = "All values normal. Click for detailed AI analysis.",
            ["col.status"]      = "Status",
            ["col.norm"]        = "Reference",
            ["status.warn"]     = "⚠ Abnormal",
            ["status.ok"]       = "✓ Normal",
            ["dv.high"]         = "▲ High",
            ["dv.low"]          = "▼ Low",
            ["dv.other"]        = "⚠ Abnormal",

            ["charts.title"]    = "Metric trend",
            ["charts.empty"]    = "No data. Upload tests first.",
            ["charts.select"]   = "— select a metric —",
            ["charts.xLabel"]   = "Date",

            ["profile.title"]       = "My Profile",
            ["profile.sex"]         = "Sex",
            ["profile.female"]      = "Female",
            ["profile.male"]        = "Male",
            ["profile.birthdate"]   = "Date of birth",
            ["profile.years"]       = "y.o.",
            ["profile.cycleday"]    = "Current cycle day",
            ["profile.cycleday.hint"] = "1 to 28. AI will account for it when interpreting hormones.",
            ["profile.save"]        = "Save",
            ["profile.saved"]       = "Saved",

            ["login.subtitle"]  = "Personal lab results journal",
            ["login.google"]    = "Sign in with Google",
            ["login.google.short"] = "Sign in",
            ["demo.try"]        = "Try demo",

            ["landing.hero.title"]       = "Decode your lab results",
            ["landing.hero.title.accent"]= "in seconds",
            ["landing.hero.sub"]         = "Upload a PDF from any lab — AI explains every metric in plain language, no medical jargon.",
            ["landing.hero.cta"]         = "Upload PDF",
            ["landing.hero.labs"]        = "Synevo, Díla, CSD, Invivo and more",
            ["landing.trust.label"]      = "Supports results from",
            ["landing.trust.more"]       = "and more",
            ["landing.how.eyebrow"]      = "How it works",
            ["landing.how.title"]        = "Three steps to clarity",
            ["landing.step1.title"]      = "Upload PDF",
            ["landing.step1.sub"]        = "Pick a file from any lab",
            ["landing.step2.title"]      = "AI analyses",
            ["landing.step2.sub"]        = "Recognises metrics and compares with reference ranges",
            ["landing.step3.title"]      = "See your results",
            ["landing.step3.sub"]        = "Plain-language explanations and trends over time",
            ["landing.features.eyebrow"] = "Features",
            ["landing.features.title"]   = "Everything you need",
            ["landing.f1.title"]         = "AI explanations",
            ["landing.f1.sub"]           = "Every metric explained in plain language, no jargon",
            ["landing.f2.title"]         = "All labs",
            ["landing.f2.sub"]           = "Synevo, Díla, CSD, Invivo and any standard PDF",
            ["landing.f3.title"]         = "Trends",
            ["landing.f3.sub"]           = "Track how your metrics change over time with charts",
            ["landing.f4.title"]         = "Secure",
            ["landing.f4.sub"]           = "Your data stays in your account only",
            ["landing.cta.title"]        = "Ready to check your results?",
            ["landing.cta.sub"]          = "Free. No credit card required.",
            ["landing.cta.btn"]          = "Get started",
            ["landing.cta.demo"]         = "Or try the demo →",
            ["landing.demo.title"]       = "Blood count",
            ["landing.demo.date"]        = "05 Jun 2025",
            ["landing.demo.m1"]          = "Haemoglobin",
            ["landing.demo.m2"]          = "Leukocytes",
            ["landing.demo.m3"]          = "Glucose",
            ["landing.demo.m4"]          = "Ferritin",
            ["landing.demo.ai"]          = "Ferritin low — possible anaemia",
            ["landing.demo.warn"]        = "Abnormal",
            ["demo.banner"]     = "Demo mode — sample data only. Sign in to track your own results.",
            ["demo.login"]      = "Sign in",

            ["paywall.title"]   = "AI credits used up",
            ["paywall.sub"]     = "You've used all your free interpretations. Top up to continue.",
            ["paywall.pack3"]   = "Starter",
            ["paywall.pack10"]  = "Standard",
            ["paywall.pack25"]  = "Pro",
            ["paywall.credits"] = "interpretations",
            ["paywall.popular"] = "Popular",
            ["paywall.contact"] = "To purchase, write to:",
            ["paywall.close"]   = "Close",
        },

        ["ru"] = new()
        {
            ["brand"]              = "Мои анализы",
            ["logout"]             = "Выйти",
            ["loading"]            = "Загрузка…",
            ["generating"]         = "Генерирую заключение…",

            ["tab.dashboard"]      = "Главная",
            ["tab.upload"]         = "PDF",
            ["tab.history"]        = "История",
            ["tab.charts"]         = "Графики",

            ["stat.lastDate"]      = "Последний анализ",
            ["stat.totalCount"]    = "Отслеживается показателей",
            ["stat.abnCount"]      = "Отклонений (последняя дата)",
            ["stat.normal"]        = "✓ Норма",
            ["section.categories"] = "Группы показателей",
            ["section.aiSummary"]  = "ИИ-анализ последних результатов",
            ["ai.btn"]             = "🤖 Получить заключение ИИ",
            ["ai.loading"]         = "⏳ Генерирую…",
            ["ai.header"]          = "🤖 Заключение ИИ",
            ["ai.failed"]          = "Не удалось получить заключение.",
            ["ai.error"]           = "Ошибка при обращении к ИИ.",
            ["ai.empty"]           = "Пустой ответ.",
            ["ai.short"]           = "🤖 ИИ",
            ["ai.analyze"]         = "🤖 Анализ ИИ",
            ["ai.explain"]         = "Пояснение ИИ",
            ["cat.count"]          = "показ.",
            ["empty.noData"]       = "Нет анализов. Загрузите первый PDF!",
            ["empty.upload"]       = "📄 Загрузить PDF",

            ["upload.strip.hint"]  = "Обновить результаты:",
            ["upload.strip.btn"]   = "Загрузить PDF",
            ["upload.title"]       = "Загрузка PDF анализов",
            ["upload.choose"]      = "Выберите PDF файл",
            ["upload.hint"]        = "PDF до 5 МБ",
            ["upload.parse"]       = "Разобрать PDF",
            ["upload.parsing"]     = "⏳ Распознаю…",
            ["upload.save"]        = "💾 Сохранить в базу",
            ["upload.saving"]      = "⏳ Сохраняю…",
            ["upload.notFound"]    = "Показатели не найдены в PDF.",
            ["upload.tooLarge"]    = "Файл слишком большой (макс. 5 МБ).",
            ["upload.found"]       = "Распознано {0} показателей. Проверьте и сохраните.",
            ["upload.saved"]       = "✅ Сохранено {0} показателей.",
            ["upload.saveFailed"]  = "Ошибка сохранения: {0}",
            ["upload.error"]       = "Ошибка: {0}",

            ["tbl.indicator"]      = "Показатель",
            ["tbl.value"]          = "Значение",
            ["tbl.unit"]           = "Ед.",
            ["tbl.range"]          = "Норма",

            ["history.title"]          = "История анализов",
            ["share.latest"]           = "🔗 Последний",
            ["share.all"]              = "🔗 Все",
            ["share.latestScope"]       = "последний анализ",
            ["share.allScope"]          = "все анализы",
            ["share.copy"]             = "📋 Копировать",
            ["filter.label"]           = "Фильтр:",
            ["history.empty"]          = "Нет сохранённых анализов. Загрузите первый PDF.",
            ["history.emptyCategory"]  = "Нет анализов в категории «{0}».",
            ["col.date"]               = "Дата",
            ["col.indicators"]         = "Показатели",
            ["col.deviations"]         = "Отклонения",
            ["delete.confirm"]         = "Удалить все анализы за {0} {1}?",

            ["hero.eyebrow"]    = "✨ AI-анализ",
            ["hero.title"]      = "Расшифруйте свои анализы с помощью AI",
            ["hero.sub"]        = "Загрузите PDF с лабораторными результатами и получите понятное объяснение каждого показателя.",
            ["hero.badge.ai"]   = "AI заключение",
            ["hero.badge.norm"] = "Норма",
            ["ai.conclusion"]   = "AI заключение",
            ["ai.deviation.summary"] = "отклонений от нормы. Нажмите для полного AI-анализа.",
            ["ai.normal.summary"]    = "Показатели в норме. Нажмите для детального AI-анализа.",
            ["col.status"]      = "Статус",
            ["col.norm"]        = "Норма",
            ["status.warn"]     = "⚠ Отклонение",
            ["status.ok"]       = "✓ Норма",
            ["dv.high"]         = "▲ Повышен",
            ["dv.low"]          = "▼ Снижен",
            ["dv.other"]        = "⚠ Отклонение",

            ["charts.title"]    = "Динамика показателя",
            ["charts.empty"]    = "Нет данных. Сначала загрузите анализы.",
            ["charts.select"]   = "— выберите показатель —",
            ["charts.xLabel"]   = "Дата",

            ["profile.title"]       = "Мой профиль",
            ["profile.sex"]         = "Пол",
            ["profile.female"]      = "Женщина",
            ["profile.male"]        = "Мужчина",
            ["profile.birthdate"]   = "Дата рождения",
            ["profile.years"]       = "л.",
            ["profile.cycleday"]    = "День цикла (текущий)",
            ["profile.cycleday.hint"] = "От 1 до 28. AI учтёт при расшифровке гормонов.",
            ["profile.save"]        = "Сохранить",
            ["profile.saved"]       = "Сохранено",

            ["login.subtitle"]  = "Личный журнал лабораторных исследований",
            ["login.google"]    = "Войти через Google",
            ["login.google.short"] = "Войти",
            ["demo.try"]        = "Посмотреть демо",

            ["landing.hero.title"]       = "Расшифруйте свои анализы",
            ["landing.hero.title.accent"]= "за секунды",
            ["landing.hero.sub"]         = "Загрузите PDF из любой лаборатории — AI объяснит каждый показатель понятным языком, без медицинского жаргона.",
            ["landing.hero.cta"]         = "Загрузить PDF",
            ["landing.hero.labs"]        = "Synevo, Діла, CSD, Invivo и другие лаборатории",
            ["landing.trust.label"]      = "Поддерживает результаты из",
            ["landing.trust.more"]       = "и другие",
            ["landing.how.eyebrow"]      = "Как это работает",
            ["landing.how.title"]        = "Три шага к ясности",
            ["landing.step1.title"]      = "Загрузите PDF",
            ["landing.step1.sub"]        = "Выберите файл из любой лаборатории",
            ["landing.step2.title"]      = "AI анализирует",
            ["landing.step2.sub"]        = "Распознаёт показатели и сравнивает с нормами",
            ["landing.step3.title"]      = "Видите результаты",
            ["landing.step3.sub"]        = "Понятные объяснения и динамика во времени",
            ["landing.features.eyebrow"] = "Возможности",
            ["landing.features.title"]   = "Всё что нужно",
            ["landing.f1.title"]         = "AI объяснения",
            ["landing.f1.sub"]           = "Каждый показатель — понятным языком, без жаргона",
            ["landing.f2.title"]         = "Все лаборатории",
            ["landing.f2.sub"]           = "Synevo, Діла, CSD, Invivo и любой стандартный PDF",
            ["landing.f3.title"]         = "Динамика",
            ["landing.f3.sub"]           = "Отслеживайте изменения показателей на графиках",
            ["landing.f4.title"]         = "Безопасно",
            ["landing.f4.sub"]           = "Данные хранятся только в вашем аккаунте",
            ["landing.cta.title"]        = "Готовы проверить свои анализы?",
            ["landing.cta.sub"]          = "Бесплатно. Без кредитной карты.",
            ["landing.cta.btn"]          = "Начать сейчас",
            ["landing.cta.demo"]         = "Или посмотрите демо →",
            ["landing.demo.title"]       = "Анализ крови",
            ["landing.demo.date"]        = "05.06.2025",
            ["landing.demo.m1"]          = "Гемоглобин",
            ["landing.demo.m2"]          = "Лейкоциты",
            ["landing.demo.m3"]          = "Глюкоза",
            ["landing.demo.m4"]          = "Ферритин",
            ["landing.demo.ai"]          = "Ферритин снижен — возможна анемия",
            ["landing.demo.warn"]        = "Отклонение",
            ["demo.banner"]     = "Демо-режим — примерные данные. Войдите, чтобы сохранить свои анализы.",
            ["demo.login"]      = "Войти",

            ["paywall.title"]   = "AI-кредиты исчерпаны",
            ["paywall.sub"]     = "Вы использовали все бесплатные расшифровки. Пополните кредиты, чтобы продолжить.",
            ["paywall.pack3"]   = "Стартовый",
            ["paywall.pack10"]  = "Стандарт",
            ["paywall.pack25"]  = "Про",
            ["paywall.credits"] = "расшифровки",
            ["paywall.popular"] = "Популярный",
            ["paywall.contact"] = "Для оплаты пишите:",
            ["paywall.close"]   = "Закрыть",
        },
    };
}
