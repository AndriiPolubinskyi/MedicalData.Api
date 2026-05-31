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

            ["charts.title"]    = "Динаміка показника",
            ["charts.empty"]    = "Немає даних. Спочатку завантажте аналізи.",
            ["charts.select"]   = "— оберіть показник —",
            ["charts.xLabel"]   = "Дата",

            ["login.subtitle"]  = "Персональний журнал лабораторних досліджень",
            ["login.google"]    = "Увійти через Google",
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

            ["charts.title"]    = "Metric trend",
            ["charts.empty"]    = "No data. Upload tests first.",
            ["charts.select"]   = "— select a metric —",
            ["charts.xLabel"]   = "Date",

            ["login.subtitle"]  = "Personal lab results journal",
            ["login.google"]    = "Sign in with Google",
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

            ["charts.title"]    = "Динамика показателя",
            ["charts.empty"]    = "Нет данных. Сначала загрузите анализы.",
            ["charts.select"]   = "— выберите показатель —",
            ["charts.xLabel"]   = "Дата",

            ["login.subtitle"]  = "Личный журнал лабораторных исследований",
            ["login.google"]    = "Войти через Google",
        },
    };
}
