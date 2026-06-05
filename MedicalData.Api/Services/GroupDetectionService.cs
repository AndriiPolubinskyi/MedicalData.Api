namespace MedicalData.Api.Services;

public static class GroupDetectionService
{
    // Ordered: more specific groups first to avoid false matches.
    // Each entry: (groupId, keywords[]) — first matching keyword wins.
    private static readonly (int GroupId, string[] Keywords)[] Rules =
    [
        // 6 — Простата/ПСА (specific abbreviations — check before generic "антиген")
        (6, ["псА", "psa", "простат-специфічн"]),

        // 3 — Глюкоза та обмін (check "глікозильован" BEFORE group 2's "гемоглобін")
        (3, ["глікозильован", "нома", "homa", "с-пептид", "c peptide", "hba1c", "глюкоз", "інсулін", "insulin", "ins)"]),

        // 5 — Гормони
        (5, ["тестостерон", "testosterone", "fai", "статеві гормони", "лютеїнізуючий", "cortisol", "кортизол", "естрадіол", "прогестерон", "fsh", "фш"]),

        // 4 — Печінка та біохімія
        (4, ["алт", " alt", "аст", " ast", "ггт", " ggt", "білірубін", "лужна фосфатаза", "лактатдегідрогеназа", " ldh", "загальний білок"]),

        // 1 — Ліпідний профіль
        (1, ["холестерин", "тригліцерид", "лпвщ", "лпнщ", "лпднщ", "hdl", "ldl", "vldl", "non-hdl", "атерогенності", "chol"]),

        // 2 — Загальний аналіз крові (most general — last)
        (2, ["гемоглобін", "еритроцит", "гематокрит", "лейкоцит", "нейтрофіл", "мієлоцит", "лімфоцит", "моноцит", "базофіл", "еозинофіл", "тромбоцит", "шое", "esr", " wbc", " rbc", " plt", "mcv", "mchc", " mch", "rdw", "mpv", "pct)", "pdw", "віроцит", "плазматичн"]),
    ];

    public static int? Detect(string testName)
    {
        var lower = testName.ToLowerInvariant();
        foreach (var (groupId, keywords) in Rules)
        {
            if (keywords.Any(k => lower.Contains(k.ToLowerInvariant())))
                return groupId;
        }
        return null;
    }
}
