using ClosedXML.Excel;
using PharmacyWarehouse.Models;
using PharmacyWarehouse.Models.Reports;

namespace PharmacyWarehouse.Services.DocumentGeneration;

public interface IExcelReportGenerator
{
    void GenerateStockReport(List<StockReportRow> rows, string outputPath, DateTime reportDate);
    void GenerateMovementReport(List<MovementReportRow> rows, string outputPath, DateTime from, DateTime to);
    void GenerateSalesAnalysisReport(List<SalesAnalysisRow> rows, string outputPath, DateTime from, DateTime to);
    void GenerateInventoryReport(Inventory inventory, List<InventoryLine> lines, string outputPath);
    void GenerateInventoryAnalysisReport(List<InventoryReportRow> rows, string outputPath, DateTime reportDate);
}

public class ExcelReportGenerator : IExcelReportGenerator
{
    public void GenerateStockReport(List<StockReportRow> rows, string outputPath, DateTime reportDate)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Остатки");

        ws.Cell(1, 1).Value = $"Остатки на дату: {reportDate:dd.MM.yyyy}";
        ws.Range(1, 1, 1, 8).Merge().Style.Font.SetBold();

        var headers = new[] { "Наименование", "Категория", "Производитель", "Серия", "Срок годности", "Остаток", "Цена", "Сумма" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(3, i + 1).Value = headers[i];
        ws.Range(3, 1, 3, headers.Length).Style.Font.SetBold();

        int row = 4;
        foreach (var item in rows)
        {
            ws.Cell(row, 1).Value = item.ProductName;
            ws.Cell(row, 2).Value = item.CategoryName;
            ws.Cell(row, 3).Value = item.Manufacturer;
            ws.Cell(row, 4).Value = item.Series;
            ws.Cell(row, 5).Value = item.ExpirationDate?.ToString("dd.MM.yyyy") ?? "";
            ws.Cell(row, 6).Value = item.Quantity;
            ws.Cell(row, 7).Value = item.SellingPrice;
            ws.Cell(row, 8).Value = item.TotalValue;
            row++;
        }

        ws.Cell(row, 5).Value = "ИТОГО:";
        ws.Cell(row, 5).Style.Font.SetBold();
        ws.Cell(row, 6).Value = rows.Sum(r => r.Quantity);
        ws.Cell(row, 8).Value = rows.Sum(r => r.TotalValue);
        ws.Cell(row, 6).Style.Font.SetBold();
        ws.Cell(row, 8).Style.Font.SetBold();

        ws.Columns().AdjustToContents();
        workbook.SaveAs(outputPath);
    }

    public void GenerateMovementReport(List<MovementReportRow> rows, string outputPath, DateTime from, DateTime to)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Движение");

        ws.Cell(1, 1).Value = $"Движение товаров с {from:dd.MM.yyyy} по {to:dd.MM.yyyy}";
        ws.Range(1, 1, 1, 6).Merge().Style.Font.SetBold();

        var headers = new[] { "Наименование", "Начальный остаток", "Приход", "Расход", "Списание", "Конечный остаток" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(3, i + 1).Value = headers[i];
        ws.Range(3, 1, 3, headers.Length).Style.Font.SetBold();

        int row = 4;
        foreach (var item in rows)
        {
            ws.Cell(row, 1).Value = item.ProductName;
            ws.Cell(row, 2).Value = item.OpeningBalance;
            ws.Cell(row, 3).Value = item.IncomingQty;
            ws.Cell(row, 4).Value = item.OutgoingQty;
            ws.Cell(row, 5).Value = item.WriteOffQty;
            ws.Cell(row, 6).Value = item.ClosingBalance;
            row++;
        }

        ws.Cell(row, 1).Value = "ИТОГО:";
        ws.Cell(row, 1).Style.Font.SetBold();
        ws.Cell(row, 2).Value = rows.Sum(r => r.OpeningBalance);
        ws.Cell(row, 3).Value = rows.Sum(r => r.IncomingQty);
        ws.Cell(row, 4).Value = rows.Sum(r => r.OutgoingQty);
        ws.Cell(row, 5).Value = rows.Sum(r => r.WriteOffQty);
        ws.Cell(row, 6).Value = rows.Sum(r => r.ClosingBalance);
        ws.Range(row, 1, row, 6).Style.Font.SetBold();

        ws.Columns().AdjustToContents();
        workbook.SaveAs(outputPath);
    }

    public void GenerateSalesAnalysisReport(List<SalesAnalysisRow> rows, string outputPath, DateTime from, DateTime to)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Анализ продаж");

        ws.Cell(1, 1).Value = $"Анализ продаж с {from:dd.MM.yyyy} по {to:dd.MM.yyyy}";
        ws.Range(1, 1, 1, 5).Merge().Style.Font.SetBold();

        var headers = new[] { "Место", "Наименование", "Продано (шт.)", "Выручка (руб.)", "Средняя цена" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(3, i + 1).Value = headers[i];
        ws.Range(3, 1, 3, headers.Length).Style.Font.SetBold();

        int row = 4;
        foreach (var item in rows)
        {
            ws.Cell(row, 1).Value = item.Rank;
            ws.Cell(row, 2).Value = item.ProductName;
            ws.Cell(row, 3).Value = item.TotalQuantitySold;
            ws.Cell(row, 4).Value = item.TotalRevenue;
            ws.Cell(row, 5).Value = item.AveragePricePerUnit;
            row++;
        }

        ws.Cell(row, 2).Value = "ИТОГО:";
        ws.Cell(row, 2).Style.Font.SetBold();
        ws.Cell(row, 3).Value = rows.Sum(r => r.TotalQuantitySold);
        ws.Cell(row, 4).Value = rows.Sum(r => r.TotalRevenue);
        ws.Cell(row, 3).Style.Font.SetBold();
        ws.Cell(row, 4).Style.Font.SetBold();

        ws.Columns().AdjustToContents();
        workbook.SaveAs(outputPath);
    }

    public void GenerateInventoryReport(Inventory inventory, List<InventoryLine> lines, string outputPath)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Инвентаризация");

        ws.Cell(1, 1).Value = $"Акт инвентаризации №{inventory.Number} от {inventory.InventoryDate:dd.MM.yyyy}";
        ws.Range(1, 1, 1, 7).Merge().Style.Font.SetBold();

        var headers = new[] { "Наименование", "Производитель", "Серия", "Срок годности", "Учётный остаток", "Фактический остаток", "Расхождение" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(3, i + 1).Value = headers[i];
        ws.Range(3, 1, 3, headers.Length).Style.Font.SetBold();

        int row = 4;
        foreach (var line in lines)
        {
            var batch = line.Batch;
            ws.Cell(row, 1).Value = batch?.Product?.Name ?? "";
            ws.Cell(row, 2).Value = batch?.Product?.Manufacturer ?? "";
            ws.Cell(row, 3).Value = batch?.Series ?? "";
            ws.Cell(row, 4).Value = batch?.ExpirationDate.ToString("dd.MM.yyyy") ?? "";
            ws.Cell(row, 5).Value = line.ExpectedQuantity;
            ws.Cell(row, 6).Value = line.ActualQuantity ?? 0;
            ws.Cell(row, 7).Value = line.Difference;
            row++;
        }

        ws.Columns().AdjustToContents();
        workbook.SaveAs(outputPath);
    }

    public void GenerateInventoryAnalysisReport(List<InventoryReportRow> rows, string outputPath, DateTime reportDate)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Инвентаризация");

        ws.Cell(1, 1).Value = $"Отчёт инвентаризации от {reportDate:dd.MM.yyyy}";
        ws.Range(1, 1, 1, 7).Merge().Style.Font.SetBold();

        var headers = new[] { "Наименование", "Серия", "Срок годности", "Учётный остаток", "Фактический остаток", "Расхождение", "Статус" };
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(3, i + 1).Value = headers[i];
        ws.Range(3, 1, 3, headers.Length).Style.Font.SetBold();

        int row = 4;
        foreach (var item in rows)
        {
            ws.Cell(row, 1).Value = item.ProductName;
            ws.Cell(row, 2).Value = item.Series;
            ws.Cell(row, 3).Value = item.ExpirationDate.ToString("dd.MM.yyyy");
            ws.Cell(row, 4).Value = item.AccountedQuantity;
            ws.Cell(row, 5).Value = item.ActualQuantity;
            ws.Cell(row, 6).Value = item.Difference;
            ws.Cell(row, 7).Value = item.DifferenceStatus;
            row++;
        }

        ws.Columns().AdjustToContents();
        workbook.SaveAs(outputPath);
    }
}
