using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scoring.Models;

/// <summary>
/// Снимок обученной WOE-скоркарты.
/// Каждый раз при обучении создаётся новая запись (аудит-трейл).
/// Активная версия определяется полем IsActive.
/// </summary>
[Table("model_coefficients")]
public class ModelCoefficients
{
    [Key]
    public int Id { get; set; }

    // ── Параметры шкалы ──────────────────────────────────────
    public double ScaleOffset { get; set; }   // ~487
    public double Factor      { get; set; }   // PDO/ln2 ≈ 28.85

    // ── Пороги принятия решений ──────────────────────────────
    public int ThresholdApprove { get; set; } = 580;
    public int ThresholdReview  { get; set; } = 540;

    // ── Баллы по бинам (скоркарта) ───────────────────────────
    // Возраст
    public int PtsAge21_25 { get; set; }
    public int PtsAge26_35 { get; set; }
    public int PtsAge36_45 { get; set; }
    public int PtsAge46_55 { get; set; }
    public int PtsAge56_65 { get; set; }

    // Доход (сом/мес)
    public int PtsIncomeLt30K  { get; set; }
    public int PtsIncome30_60K { get; set; }
    public int PtsIncome60_100K{ get; set; }
    public int PtsIncomeGt100K { get; set; }

    // Стаж
    public int PtsExp0    { get; set; }
    public int PtsExp1_3  { get; set; }
    public int PtsExp3_7  { get; set; }
    public int PtsExpGt7  { get; set; }

    // DTI (%)
    public int PtsDtiLt30  { get; set; }
    public int PtsDti30_45 { get; set; }
    public int PtsDti45_60 { get; set; }
    public int PtsDtiGt60  { get; set; }

    // Иждивенцы
    public int PtsDep0  { get; set; }
    public int PtsDep1  { get; set; }
    public int PtsDep2  { get; set; }
    public int PtsDepGt3{ get; set; }

    // Активы
    public int PtsRealEstateYes { get; set; }
    public int PtsRealEstateNo  { get; set; }
    public int PtsVehicleYes    { get; set; }
    public int PtsVehicleNo     { get; set; }

    // ── Метрики качества модели ──────────────────────────────
    public double Gini         { get; set; }   // 2*AUC - 1
    public double TrainAuc     { get; set; }
    public int    TrainSamples { get; set; }
    public double DefaultRate  { get; set; }

    // ── IV по признакам (JSON) ───────────────────────────────
    public string FeatureIvJson { get; set; } = "{}";

    // ── Аудит ────────────────────────────────────────────────
    public bool     IsActive  { get; set; } = false;
    public DateTime TrainedAt { get; set; } = DateTime.UtcNow;
    public string   TrainedBy { get; set; } = "";
}