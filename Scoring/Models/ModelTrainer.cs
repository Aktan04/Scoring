using Scoring.Models;
using System.Text.Json;

namespace Scoring.Services;

/// <summary>
/// Движок обучения WOE-скоркарты.
/// Реализует полный ML-пайплайн на чистом C# без внешних зависимостей:
///   1. Бинирование признаков
///   2. Расчёт WOE и IV для каждого бина
///   3. Обучение логистической регрессии (gradient descent)
///   4. Перевод коэффициентов в баллы скоркарты (Basel II-шкала)
///   5. Расчёт метрик качества (AUC, Gini)
/// </summary>
public class ModelTrainer
{
    // ── Параметры шкалы Basel II ─────────────────────────────────────────────
    private const double Pdo       = 20;           // Points to Double Odds
    private const double BaseScore = 600;          // балл при Odds = BaseOdds
    private const double BaseOdds  = 50;           // good/bad при BaseScore
    private readonly double _factor;
    private readonly double _offset;

    // ── Параметры обучения ───────────────────────────────────────────────────
    private const double LearningRate = 0.05;
    private const int    MaxIter      = 2000;
    private const double Epsilon      = 1e-8;

    public ModelTrainer()
    {
        _factor = Pdo / Math.Log(2);
        _offset = BaseScore - _factor * Math.Log(BaseOdds);
    }

    // =========================================================================
    // ГЛАВНЫЙ МЕТОД: принимает записи → возвращает готовую ModelCoefficients
    // =========================================================================
    public ModelCoefficients Train(List<TrainingRecord> data, string trainedBy)
    {
        if (data.Count < 50)
            throw new InvalidOperationException("Недостаточно данных для обучения (минимум 50 записей).");

        int totalGood = data.Count(r => !r.IsDefault);
        int totalBad  = data.Count(r =>  r.IsDefault);
        if (totalBad == 0 || totalGood == 0)
            throw new InvalidOperationException("В датасете должны быть оба класса (default и non-default).");

        double dr = (double)totalBad / data.Count;

        // ── 1. Расчёт WOE/IV ────────────────────────────────────────────────
        var woeAge = CalcWoe(data, r => AgeBin(r.Age),    totalGood, totalBad);
        var woeInc = CalcWoe(data, r => IncomeBin(r.Income), totalGood, totalBad);
        var woeExp = CalcWoe(data, r => ExpBin(r.ExpYears),  totalGood, totalBad);
        var woeDti = CalcWoe(data, r => DtiBin(r.Dti),       totalGood, totalBad);
        var woeDep = CalcWoe(data, r => DepBin(r.Dependents),totalGood, totalBad);
        var woeRe  = CalcWoe(data, r => r.HasRealEstate ? "1" : "0", totalGood, totalBad);
        var woeVeh = CalcWoe(data, r => r.HasVehicle    ? "1" : "0", totalGood, totalBad);

        var ivMap = new Dictionary<string, double>
        {
            ["DTI (Долговая нагрузка)"]   = TotalIv(woeDti),
            ["Доход"]                      = TotalIv(woeInc),
            ["Возраст"]                    = TotalIv(woeAge),
            ["Стаж работы"]                = TotalIv(woeExp),
            ["Наличие недвижимости"]       = TotalIv(woeRe),
            ["Количество иждивенцев"]      = TotalIv(woeDep),
            ["Наличие автомобиля"]         = TotalIv(woeVeh),
        };

        // ── 2. WOE-трансформация датасета ────────────────────────────────────
        var X = data.Select(r => new double[]
        {
            LookupWoe(woeAge, AgeBin(r.Age)),
            LookupWoe(woeInc, IncomeBin(r.Income)),
            LookupWoe(woeExp, ExpBin(r.ExpYears)),
            LookupWoe(woeDti, DtiBin(r.Dti)),
            LookupWoe(woeDep, DepBin(r.Dependents)),
            LookupWoe(woeRe,  r.HasRealEstate ? "1" : "0"),
            LookupWoe(woeVeh, r.HasVehicle    ? "1" : "0"),
        }).ToArray();

        var y = data.Select(r => r.IsDefault ? 1.0 : 0.0).ToArray();

        // ── 3. Логистическая регрессия (gradient descent) ───────────────────
        var (b0, coefs) = FitLogisticRegression(X, y);

        // ── 4. Перевод в баллы (Basel II) ───────────────────────────────────
        int nFeat = 7;
        double b0Share = b0 / nFeat;

        int Pts(double woe, double coef) =>
            (int)Math.Round(-(b0Share + coef * woe) * _factor);

        // ── 5. Метрики (AUC / Gini) ──────────────────────────────────────────
        var probs = X.Select(x =>
        {
            double logit = b0;
            for (int j = 0; j < coefs.Length; j++) logit += coefs[j] * x[j];
            return Sigmoid(logit);
        }).ToArray();

        double auc  = CalcAuc(y, probs);
        double gini = 2 * auc - 1;

        // ── 6. Собираем ModelCoefficients ────────────────────────────────────
        var mc = new ModelCoefficients
        {
            ScaleOffset = _offset,
            Factor      = _factor,
            TrainSamples = data.Count,
            DefaultRate  = dr,
            TrainAuc     = auc,
            Gini         = gini,
            TrainedBy    = trainedBy,
            TrainedAt    = DateTime.UtcNow,
            IsActive     = false,
            FeatureIvJson = JsonSerializer.Serialize(ivMap),

            // Возраст
            PtsAge21_25 = Pts(LookupWoe(woeAge, "21-25"), coefs[0]),
            PtsAge26_35 = Pts(LookupWoe(woeAge, "26-35"), coefs[0]),
            PtsAge36_45 = Pts(LookupWoe(woeAge, "36-45"), coefs[0]),
            PtsAge46_55 = Pts(LookupWoe(woeAge, "46-55"), coefs[0]),
            PtsAge56_65 = Pts(LookupWoe(woeAge, "56-65"), coefs[0]),

            // Доход
            PtsIncomeLt30K   = Pts(LookupWoe(woeInc, "lt30"),    coefs[1]),
            PtsIncome30_60K  = Pts(LookupWoe(woeInc, "30-60"),   coefs[1]),
            PtsIncome60_100K = Pts(LookupWoe(woeInc, "60-100"),  coefs[1]),
            PtsIncomeGt100K  = Pts(LookupWoe(woeInc, "gt100"),   coefs[1]),

            // Стаж
            PtsExp0   = Pts(LookupWoe(woeExp, "0"),   coefs[2]),
            PtsExp1_3 = Pts(LookupWoe(woeExp, "1-3"), coefs[2]),
            PtsExp3_7 = Pts(LookupWoe(woeExp, "3-7"), coefs[2]),
            PtsExpGt7 = Pts(LookupWoe(woeExp, "7+"),  coefs[2]),

            // DTI
            PtsDtiLt30  = Pts(LookupWoe(woeDti, "lt30"),  coefs[3]),
            PtsDti30_45 = Pts(LookupWoe(woeDti, "30-45"), coefs[3]),
            PtsDti45_60 = Pts(LookupWoe(woeDti, "45-60"), coefs[3]),
            PtsDtiGt60  = Pts(LookupWoe(woeDti, "gt60"),  coefs[3]),

            // Иждивенцы
            PtsDep0   = Pts(LookupWoe(woeDep, "0"),  coefs[4]),
            PtsDep1   = Pts(LookupWoe(woeDep, "1"),  coefs[4]),
            PtsDep2   = Pts(LookupWoe(woeDep, "2"),  coefs[4]),
            PtsDepGt3 = Pts(LookupWoe(woeDep, "3+"), coefs[4]),

            // Недвижимость
            PtsRealEstateYes = Pts(LookupWoe(woeRe, "1"), coefs[5]),
            PtsRealEstateNo  = Pts(LookupWoe(woeRe, "0"), coefs[5]),

            // Автомобиль
            PtsVehicleYes = Pts(LookupWoe(woeVeh, "1"), coefs[6]),
            PtsVehicleNo  = Pts(LookupWoe(woeVeh, "0"), coefs[6]),
        };

        return mc;
    }

    // =========================================================================
    // БИНИРОВАНИЕ
    // =========================================================================
    public static string AgeBin(int age) => age switch
    {
        <= 25 => "21-25", <= 35 => "26-35", <= 45 => "36-45",
        <= 55 => "46-55", _     => "56-65"
    };

    public static string IncomeBin(decimal income) => income switch
    {
        < 30_000  => "lt30", < 60_000  => "30-60",
        < 100_000 => "60-100", _       => "gt100"
    };

    public static string ExpBin(int years) => years switch
    {
        0       => "0", <= 3 => "1-3",
        <= 7    => "3-7", _  => "7+"
    };

    public static string DtiBin(decimal dti) => dti switch
    {
        < 30 => "lt30", < 45 => "30-45",
        < 60 => "45-60", _   => "gt60"
    };

    public static string DepBin(int dep) => dep switch
    {
        0 => "0", 1 => "1", 2 => "2", _ => "3+"
    };

    // =========================================================================
    // WOE / IV
    // =========================================================================
    private static Dictionary<string, (double woe, double iv)> CalcWoe(
        List<TrainingRecord> data,
        Func<TrainingRecord, string> binFn,
        int totalGood,
        int totalBad)
    {
        var groups = data.GroupBy(binFn).ToDictionary(
            g => g.Key,
            g => (good: g.Count(r => !r.IsDefault),
                  bad:  g.Count(r =>  r.IsDefault)));

        var result = new Dictionary<string, (double, double)>();
        foreach (var (bin, (good, bad)) in groups)
        {
            double dg = Math.Max((double)good / totalGood, Epsilon);
            double db = Math.Max((double)bad  / totalBad,  Epsilon);
            double woe = Math.Log(dg / db);
            double iv  = (dg - db) * woe;
            result[bin] = (woe, iv);
        }
        return result;
    }

    private static double LookupWoe(Dictionary<string, (double woe, double iv)> map, string bin)
        => map.TryGetValue(bin, out var v) ? v.woe : 0.0;

    private static double TotalIv(Dictionary<string, (double woe, double iv)> map)
        => map.Values.Sum(v => v.iv);

    // =========================================================================
    // ЛОГИСТИЧЕСКАЯ РЕГРЕССИЯ (mini-batch gradient descent)
    // =========================================================================
    private static (double b0, double[] coefs) FitLogisticRegression(double[][] X, double[] y)
    {
        int n = X.Length;
        int m = X[0].Length;
        double b0 = 0;
        var coefs = new double[m];

        for (int iter = 0; iter < MaxIter; iter++)
        {
            double db0 = 0;
            var dc = new double[m];

            for (int i = 0; i < n; i++)
            {
                double logit = b0;
                for (int j = 0; j < m; j++) logit += coefs[j] * X[i][j];
                double pred = Sigmoid(logit);
                double err  = pred - y[i];
                db0 += err;
                for (int j = 0; j < m; j++) dc[j] += err * X[i][j];
            }

            b0 -= LearningRate * db0 / n;
            for (int j = 0; j < m; j++) coefs[j] -= LearningRate * dc[j] / n;
        }

        return (b0, coefs);
    }

    private static double Sigmoid(double x) => 1.0 / (1.0 + Math.Exp(-Math.Clamp(x, -20, 20)));

    // =========================================================================
    // AUC (трапециевидный метод)
    // =========================================================================
    private static double CalcAuc(double[] labels, double[] scores)
    {
        var pairs = labels.Zip(scores, (l, s) => (l, s))
                          .OrderByDescending(p => p.s).ToList();
        int tp = 0, fp = 0;
        int totalPos = (int)labels.Sum();
        int totalNeg = labels.Length - totalPos;
        if (totalPos == 0 || totalNeg == 0) return 0.5;

        double auc = 0;
        int prevTp = 0, prevFp = 0;
        foreach (var (label, _) in pairs)
        {
            if (label == 1) tp++; else fp++;
            auc += (double)(tp + prevTp) / 2 * (fp - prevFp);
            prevTp = tp; prevFp = fp;
        }
        return auc / ((double)totalPos * totalNeg);
    }
}