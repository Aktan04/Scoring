using System.Text.Json;
using Scoring.Models;

namespace Scoring.Services;

/// <summary>
/// Скоринговый сервис — применяет активную WOE-скоркарту из БД.
/// Коэффициенты загружаются при каждом вызове Calculate(),
/// поэтому сразу после переобучения и активации новой модели
/// следующая заявка уже считается по новым баллам.
/// </summary>
public class ScoringService
{
    private readonly ApplicationDbContext _context;

    public ScoringService(ApplicationDbContext context)
    {
        _context = context;
    }

    // =========================================================================
    // ГЛАВНЫЙ МЕТОД
    // =========================================================================
    public ScoringServiceResult Calculate(LoanApplication app)
    {
        // ── Загружаем активную модель из БД ──────────────────────────────────
        var mc = _context.ModelCoefficients
                         .Where(m => m.IsActive)
                         .OrderByDescending(m => m.TrainedAt)
                         .FirstOrDefault()
                 ?? GetFallbackCoefficients();   // fallback если БД пуста

        var breakdown = new Dictionary<string, string>();

        // ── Возраст ──────────────────────────────────────────────────────────
        int age = DateTime.Today.Year - app.BirthDate.Year;
        if (app.BirthDate > DateTime.Today.AddYears(-age)) age--;
        int pAge = AgePts(mc, age);
        breakdown["Возраст"] = $"{age} лет → {pAge:+#;-#;0} б.";

        // ── Доход ─────────────────────────────────────────────────────────────
        decimal totalIncome = app.IncomeAmount + app.AdditionalIncome;
        int pInc = IncomePts(mc, totalIncome);
        breakdown["Доход"] = $"{totalIncome:N0} сом → {pInc:+#;-#;0} б.";

        // ── Стаж ──────────────────────────────────────────────────────────────
        int pExp = ExpPts(mc, app.EmploymentYears);
        breakdown["Стаж"] = $"{app.EmploymentYears} лет → {pExp:+#;-#;0} б.";

        // ── DTI (аннуитетный платёж с учётом % ставки) ───────────────────────
        decimal rate = (app.LoanProduct?.InterestRate ?? 20m) / 100m / 12m;
        decimal monthlyPayment;
        if (rate == 0 || app.TermMonths == 0)
            monthlyPayment = app.Amount / Math.Max(app.TermMonths, 1);
        else
        {
            double r = (double)rate;
            int    n = app.TermMonths;
            monthlyPayment = (decimal)((double)app.Amount * r * Math.Pow(1+r,n) / (Math.Pow(1+r,n) - 1));
        }
        decimal dti = totalIncome > 0 ? monthlyPayment / totalIncome * 100m : 100m;
        int pDti = DtiPts(mc, dti);
        breakdown["DTI"] = $"{dti:F1}% (платёж {monthlyPayment:N0} сом) → {pDti:+#;-#;0} б.";

        // ── Иждивенцы ─────────────────────────────────────────────────────────
        int pDep = DepPts(mc, app.DependentsCount);
        breakdown["Иждивенцы"] = $"{app.DependentsCount} чел. → {pDep:+#;-#;0} б.";

        // ── Недвижимость ──────────────────────────────────────────────────────
        int pRe = app.HasRealEstate ? mc.PtsRealEstateYes : mc.PtsRealEstateNo;
        breakdown["Недвижимость"] = $"{(app.HasRealEstate ? "Есть" : "Нет")} → {pRe:+#;-#;0} б.";

        // ── Автомобиль ────────────────────────────────────────────────────────
        int pVeh = app.HasVehicle ? mc.PtsVehicleYes : mc.PtsVehicleNo;
        breakdown["Автомобиль"] = $"{(app.HasVehicle ? "Есть" : "Нет")} → {pVeh:+#;-#;0} б.";

        // ── Итоговый балл ─────────────────────────────────────────────────────
        int rawSum     = pAge + pInc + pExp + pDti + pDep + pRe + pVeh;
        int totalScore = rawSum + (int)Math.Round(mc.ScaleOffset);

        // ── Решение ───────────────────────────────────────────────────────────
        ScoringDecision decision = totalScore >= mc.ThresholdApprove
            ? ScoringDecision.Approved
            : totalScore >= mc.ThresholdReview
                ? ScoringDecision.ManualReview
                : ScoringDecision.Rejected;

        string jsonLog = JsonSerializer.Serialize(new
        {
            modelId     = mc.Id,
            modelDate   = mc.TrainedAt,
            methodology = "WOE Scorecard v2 (DB-driven)",
            bins        = breakdown,
            rawSum,
            scaleOffset = (int)Math.Round(mc.ScaleOffset),
            totalScore,
            decision    = decision.ToString()
        }, new JsonSerializerOptions
        {
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        return new ScoringServiceResult
        {
            TotalScore = totalScore,
            Decision   = decision,
            JsonLog    = jsonLog,
            Breakdown  = breakdown,
            ModelId    = mc.Id
        };
    }

    // =========================================================================
    // БАЛЛЫ ПО БИНАМ (берём из ModelCoefficients)
    // =========================================================================
    private static int AgePts(ModelCoefficients mc, int age) => age switch
    {
        <= 25 => mc.PtsAge21_25, <= 35 => mc.PtsAge26_35,
        <= 45 => mc.PtsAge36_45, <= 55 => mc.PtsAge46_55,
        _     => mc.PtsAge56_65
    };

    private static int IncomePts(ModelCoefficients mc, decimal inc) => inc switch
    {
        < 30_000  => mc.PtsIncomeLt30K,  < 60_000  => mc.PtsIncome30_60K,
        < 100_000 => mc.PtsIncome60_100K, _        => mc.PtsIncomeGt100K
    };

    private static int ExpPts(ModelCoefficients mc, int y) => y switch
    {
        0     => mc.PtsExp0,   <= 3 => mc.PtsExp1_3,
        <= 7  => mc.PtsExp3_7, _   => mc.PtsExpGt7
    };

    private static int DtiPts(ModelCoefficients mc, decimal dti) => dti switch
    {
        < 30 => mc.PtsDtiLt30,  < 45 => mc.PtsDti30_45,
        < 60 => mc.PtsDti45_60, _    => mc.PtsDtiGt60
    };

    private static int DepPts(ModelCoefficients mc, int dep) => dep switch
    {
        0 => mc.PtsDep0, 1 => mc.PtsDep1, 2 => mc.PtsDep2, _ => mc.PtsDepGt3
    };

    // =========================================================================
    // FALLBACK: хардкод если в БД нет ни одной модели
    // =========================================================================
    private static ModelCoefficients GetFallbackCoefficients() => new()
    {
        Id = 0, ScaleOffset = 487, Factor = 28.85,
        ThresholdApprove = 580, ThresholdReview = 540,
        PtsAge21_25=-6, PtsAge26_35=10, PtsAge36_45=17, PtsAge46_55=20, PtsAge56_65=-5,
        PtsIncomeLt30K=-9, PtsIncome30_60K=19, PtsIncome60_100K=14, PtsIncomeGt100K=15,
        PtsExp0=-17, PtsExp1_3=2, PtsExp3_7=4, PtsExpGt7=12,
        PtsDtiLt30=42, PtsDti30_45=24, PtsDti45_60=16, PtsDtiGt60=-13,
        PtsDep0=14, PtsDep1=6, PtsDep2=11, PtsDepGt3=1,
        PtsRealEstateYes=16, PtsRealEstateNo=1,
        PtsVehicleYes=10, PtsVehicleNo=6,
    };
}

public class ScoringServiceResult
{
    public int    TotalScore                      { get; set; }
    public ScoringDecision Decision               { get; set; }
    public string JsonLog                         { get; set; } = "";
    public Dictionary<string, string> Breakdown   { get; set; } = new();
    public int    ModelId                         { get; set; }
}