using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Scoring.Models;

namespace Scoring.Services;

/// <summary>
/// Генерирует PDF-договор по одобренной заявке.
/// Использует QuestPDF (лицензия Community — бесплатна для некоммерческих проектов).
/// </summary>
public class ContractPdfService
{
    // Цвета банка
    private static readonly string Navy  = "#1a3a5c";
    private static readonly string Teal  = "#0d6efd";
    private static readonly string Green = "#198754";
    private static readonly string Light = "#f0f4f8";
    private static readonly string Gray  = "#6c757d";

    public byte[] Generate(LoanApplication app, ScoringResult scoring)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        // Аннуитетный платёж
        decimal rate = (app.LoanProduct?.InterestRate ?? 20m) / 100m / 12m;
        decimal monthly;
        if (rate == 0 || app.TermMonths == 0)
            monthly = app.Amount / Math.Max(app.TermMonths, 1);
        else
        {
            double r = (double)rate;
            int n = app.TermMonths;
            monthly = (decimal)((double)app.Amount * r * Math.Pow(1+r,n) / (Math.Pow(1+r,n) - 1));
        }
        decimal totalCost = monthly * app.TermMonths;

        string contractNum = app.Id.ToString().Substring(0, 8).ToUpper();
        string dateStr     = DateTime.Now.ToString("dd.MM.yyyy");

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial));

                // ── Шапка ──────────────────────────────────────────────────
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().AlignCenter().Text(
                            "ОАО «КЫРГЫЗСКИЙ ИНВЕСТИЦИОННО-КРЕДИТНЫЙ БАНК» (КИКБ)")
                            .FontSize(8).FontColor(Gray);
                    });
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().AlignCenter().Text(
                            "г. Бишкек, ул. Московская 137  |  тел: +996 312 610 610  |  www.kicb.net")
                            .FontSize(7).FontColor(Gray);
                    });
                    col.Item().PaddingTop(4).BorderBottom(2).BorderColor(Navy);
                });

                // ── Тело ───────────────────────────────────────────────────
                page.Content().PaddingTop(10).Column(col =>
                {
                    // Заголовок
                    col.Item().AlignCenter().Text("КРЕДИТНЫЙ ДОГОВОР")
                        .FontSize(18).Bold().FontColor(Navy);
                    col.Item().AlignCenter().PaddingBottom(4).Text(
                        "Индивидуальные условия потребительского кредитования")
                        .FontSize(10).FontColor(Gray);
                    col.Item().BorderBottom(0.5f).BorderColor(Teal).PaddingBottom(6);

                    // Номер и дата
                    col.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem().Background(Light).Padding(8).AlignCenter()
                            .Text($"№ {contractNum}").FontSize(18).Bold().FontColor(Teal);
                        row.RelativeItem().Padding(8).AlignCenter()
                            .Text($"Дата: {dateStr}").FontSize(10).FontColor(Gray);
                    });

                    // Бейдж одобрения
                    col.Item().PaddingTop(6).Background("#d1f7e0")
                        .Border(1).BorderColor(Green)
                        .Padding(8).AlignCenter()
                        .Text($"РЕШЕНИЕ: ОДОБРЕНО  |  Скоринговый балл: {scoring.TotalScore} из 620")
                        .FontSize(10).Bold().FontColor(Green);

                    // 1. Стороны
                    col.Item().PaddingTop(12).Text("1. СТОРОНЫ ДОГОВОРА")
                        .FontSize(11).Bold().FontColor(Navy);
                    col.Item().PaddingTop(4).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });

                        // Заголовки
                        t.Header(h =>
                        {
                            h.Cell().Background(Light).Padding(6).Text("КРЕДИТОР").Bold().FontColor(Navy);
                            h.Cell().Background(Light).Padding(6).Text("ЗАЁМЩИК").Bold().FontColor(Navy);
                        });

                        // Данные
                        t.Cell().Border(0.5f).BorderColor("#dee2e6").Padding(8).Column(c =>
                        {
                            c.Item().Text("ОАО «КИКБ»");
                            c.Item().Text("Лицензия НБКР № 0014").FontColor(Gray);
                            c.Item().Text("БИК: 109007").FontColor(Gray);
                        });
                        t.Cell().Border(0.5f).BorderColor("#dee2e6").Padding(8).Column(c =>
                        {
                            c.Item().Text($"{app.LastName} {app.FirstName}").Bold();
                            c.Item().Text($"ИНН: {app.Inn}").FontColor(Gray);
                            c.Item().Text($"Паспорт: {app.PassportSerial}").FontColor(Gray);
                            c.Item().Text($"Дата рождения: {app.BirthDate:dd.MM.yyyy}").FontColor(Gray);
                        });
                    });

                    // 2. Параметры кредита
                    col.Item().PaddingTop(12).Text("2. ПАРАМЕТРЫ КРЕДИТА")
                        .FontSize(11).Bold().FontColor(Navy);
                    col.Item().PaddingTop(4).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(3); });

                        var rows = new[]
                        {
                            ("Кредитный продукт",   app.LoanProduct?.Name ?? "—"),
                            ("Сумма кредита",        $"{app.Amount:N0} сом"),
                            ("Срок кредита",         $"{app.TermMonths} месяцев"),
                            ("Процентная ставка",    $"{app.LoanProduct?.InterestRate ?? 0}% годовых"),
                            ("Ежемесячный платёж",   $"{monthly:N0} сом (аннуитет)"),
                            ("Полная стоимость",     $"{totalCost:N0} сом"),
                            ("Цель кредитования",    "Потребительские нужды"),
                        };

                        foreach (var (label, value) in rows)
                        {
                            t.Cell().Border(0.5f).BorderColor("#dee2e6").Padding(5)
                                .Text(label).FontColor(Gray);
                            t.Cell().Border(0.5f).BorderColor("#dee2e6").Padding(5)
                                .Text(value).Bold();
                        }
                    });

                    // 3. Обязанности
                    col.Item().PaddingTop(12).Text("3. ОБЯЗАННОСТИ ЗАЁМЩИКА")
                        .FontSize(11).Bold().FontColor(Navy);
                    col.Item().PaddingTop(4).Text(
                        "Заёмщик обязуется: (1) своевременно вносить ежемесячные платежи не позднее " +
                        "10-го числа каждого месяца; (2) уведомлять Банк об изменении персональных данных, " +
                        "места работы или финансового положения; (3) использовать кредитные средства " +
                        "исключительно на заявленные цели; (4) не допускать просрочек платежей.")
                        .FontSize(9).LineHeight(1.5f);

                    // 4. Подписи
                    col.Item().PaddingTop(16).Text("4. ПОДПИСИ СТОРОН")
                        .FontSize(11).Bold().FontColor(Navy);
                    col.Item().PaddingTop(6).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });

                        void SignLine(string label) => t.Cell().PaddingVertical(5)
                            .Text(label).FontColor(Gray);

                        SignLine("ОТ КРЕДИТОРА:");
                        SignLine("ОТ ЗАЁМЩИКА:");
                        SignLine("Подпись: ___________________");
                        SignLine("Подпись: ___________________");
                        SignLine("ФИО: ______________________");
                        SignLine($"ФИО: {app.LastName} {app.FirstName}");
                        SignLine("Должность: Кредитный менеджер");
                        SignLine($"Дата: {dateStr}");
                        SignLine("М.П.");
                        SignLine("");
                    });
                });

                // ── Подвал ─────────────────────────────────────────────────
                page.Footer().BorderTop(0.5f).BorderColor(Light).PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Text(
                        "Договор сформирован автоматически скоринговой системой КИКБ. " +
                        "Требуется подпись уполномоченного сотрудника.")
                        .FontSize(7).FontColor(Gray);
                    row.AutoItem().Text(x =>
                    {
                        x.Span("Стр. ").FontSize(7).FontColor(Gray);
                        x.CurrentPageNumber().FontSize(7).FontColor(Gray);
                    });
                });
            });
        });

        return document.GeneratePdf();
    }
}