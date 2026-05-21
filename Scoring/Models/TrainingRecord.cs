using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Scoring.Models;

/// <summary>
/// Одна строка обучающего датасета.
/// Таблица заполняется из seed-данных (1000 синтетических записей),
/// а затем пополняется реальными завершёнными заявками.
/// </summary>
[Table("training_records")]
public class TrainingRecord
{
    [Key]
    public int Id { get; set; }

    // ── Признаки (features) 
    public int     Age           { get; set; }
    public decimal Income        { get; set; }   // сом/мес
    public int     ExpYears      { get; set; }   // стаж
    public decimal Dti           { get; set; }   // %, долговая нагрузка
    public int     Dependents    { get; set; }
    public bool    HasRealEstate { get; set; }
    public bool    HasVehicle    { get; set; }
    public bool    HadDelinquency{ get; set; }   // просрочки в прошлом

    // ── Целевая переменная (target) 
    /// <summary>true = клиент допустил дефолт в течение 12 мес.</summary>
    public bool IsDefault { get; set; }

    // ── Метаданные
    /// <summary>
    /// null = синтетическая запись из seed.
    /// not null = реальная заявка, переведённая в обучение.
    /// </summary>
    public Guid? SourceApplicationId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}