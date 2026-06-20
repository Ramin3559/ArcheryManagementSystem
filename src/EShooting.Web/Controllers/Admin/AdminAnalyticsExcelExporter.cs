using ClosedXML.Excel;
using EShooting.Application.Common.Models;

namespace EShooting.Web.Controllers.Admin;

public static class AdminAnalyticsExcelExporter
{
    public static byte[] Export(OperationsAnalyticsResult data)
    {
        using var wb = new XLWorkbook();

        WriteSummarySheet(wb, data);
        WriteDailySheet(wb, data);
        WriteLaneSheet(wb, data);
        WriteEquipmentSheet(wb, data);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void WriteSummarySheet(XLWorkbook wb, OperationsAnalyticsResult data)
    {
        var ws = wb.Worksheets.Add("İcmal");
        ws.Cell(1, 1).Value = "Analitika icmalı";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = "Tarix aralığı";
        ws.Cell(2, 2).Value = data.Label;

        var rows = new (string Label, object Value)[]
        {
            ("Gələn müştəri (unikal)", data.UniqueCustomerCount),
            ("Yeni müştəri", data.NewCustomerCount),
            ("Seans sayı", data.SessionCount),
            ("Yeni abunə yazılışı", data.SubscriptionCreatedCount),
            ("Avadanlıq satışı", data.EquipmentSaleCount),
            ("Avadanlıq icarəsi", data.EquipmentRentalCount),
            ("Satış gəliri (₼)", data.EquipmentSaleRevenue),
            ("İcarə dəyəri (₼)", data.EquipmentRentalRevenue),
            ("Zolaq aktiv saatı (cəmi)", data.TotalLaneHours),
            ("Ən yüklü zolaq", data.BusiestLaneNumber.HasValue ? $"Zolaq {data.BusiestLaneNumber}" : "—")
        };

        var rowIdx = 4;
        foreach (var (label, value) in rows)
        {
            ws.Cell(rowIdx, 1).Value = label;
            ws.Cell(rowIdx, 2).Value = XLCellValue.FromObject(value);
            rowIdx++;
        }

        StyleHeader(ws.Range(4, 1, 4 + rows.Length - 1, 1), bold: true);
        ws.Columns().AdjustToContents();
    }

    private static void WriteDailySheet(XLWorkbook wb, OperationsAnalyticsResult data)
    {
        var ws = wb.Worksheets.Add("Günlük");
        var headers = new[]
        {
            "Tarix", "Gələn müştəri", "Yeni müştəri", "Seans", "Yeni abunə",
            "Satış", "İcarə", "Satış gəliri (₼)", "İcarə dəyəri (₼)", "Zolaq saatı"
        };

        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
        }

        var rowIdx = 2;
        foreach (var row in data.DailyBreakdown)
        {
            ws.Cell(rowIdx, 1).Value = row.DateLocal;
            ws.Cell(rowIdx, 2).Value = row.UniqueCustomerCount;
            ws.Cell(rowIdx, 3).Value = row.NewCustomerCount;
            ws.Cell(rowIdx, 4).Value = row.SessionCount;
            ws.Cell(rowIdx, 5).Value = row.SubscriptionCreatedCount;
            ws.Cell(rowIdx, 6).Value = row.EquipmentSaleCount;
            ws.Cell(rowIdx, 7).Value = row.EquipmentRentalCount;
            ws.Cell(rowIdx, 8).Value = row.EquipmentSaleRevenue;
            ws.Cell(rowIdx, 9).Value = row.EquipmentRentalRevenue;
            ws.Cell(rowIdx, 10).Value = row.LaneHoursTotal;
            rowIdx++;
        }

        if (data.DailyBreakdown.Count > 0)
        {
            ws.Cell(rowIdx, 1).Value = "Yekun";
            ws.Cell(rowIdx, 2).Value = data.UniqueCustomerCount;
            ws.Cell(rowIdx, 3).Value = data.NewCustomerCount;
            ws.Cell(rowIdx, 4).Value = data.SessionCount;
            ws.Cell(rowIdx, 5).Value = data.SubscriptionCreatedCount;
            ws.Cell(rowIdx, 6).Value = data.EquipmentSaleCount;
            ws.Cell(rowIdx, 7).Value = data.EquipmentRentalCount;
            ws.Cell(rowIdx, 8).Value = data.EquipmentSaleRevenue;
            ws.Cell(rowIdx, 9).Value = data.EquipmentRentalRevenue;
            ws.Cell(rowIdx, 10).Value = data.TotalLaneHours;
            ws.Range(rowIdx, 1, rowIdx, headers.Length).Style.Font.Bold = true;
        }

        StyleHeader(ws.Range(1, 1, 1, headers.Length));
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
    }

    private static void WriteLaneSheet(XLWorkbook wb, OperationsAnalyticsResult data)
    {
        var ws = wb.Worksheets.Add("Zolaqlar");
        ws.Cell(1, 1).Value = "Zolaq";
        ws.Cell(1, 2).Value = "Seans sayı";
        ws.Cell(1, 3).Value = "Aktiv saat";

        var rowIdx = 2;
        foreach (var row in data.LaneActivity.Where(x => x.SessionCount > 0 || x.TotalHours > 0))
        {
            ws.Cell(rowIdx, 1).Value = row.LaneNumber;
            ws.Cell(rowIdx, 2).Value = row.SessionCount;
            ws.Cell(rowIdx, 3).Value = row.TotalHours;
            rowIdx++;
        }

        if (rowIdx == 2)
        {
            ws.Cell(2, 1).Value = "—";
            ws.Cell(2, 2).Value = 0;
            ws.Cell(2, 3).Value = 0;
        }

        StyleHeader(ws.Range(1, 1, 1, 3));
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
    }

    private static void WriteEquipmentSheet(XLWorkbook wb, OperationsAnalyticsResult data)
    {
        var ws = wb.Worksheets.Add("Avadanlıq");
        ws.Cell(1, 1).Value = "Avadanlıq";
        ws.Cell(1, 2).Value = "Satış";
        ws.Cell(1, 3).Value = "İcarə";
        ws.Cell(1, 4).Value = "Satış gəliri (₼)";
        ws.Cell(1, 5).Value = "İcarə dəyəri (₼)";

        var rowIdx = 2;
        foreach (var row in data.EquipmentBreakdown)
        {
            ws.Cell(rowIdx, 1).Value = row.EquipmentName;
            ws.Cell(rowIdx, 2).Value = row.SaleCount;
            ws.Cell(rowIdx, 3).Value = row.RentalCount;
            ws.Cell(rowIdx, 4).Value = row.SaleRevenue;
            ws.Cell(rowIdx, 5).Value = row.RentalRevenue;
            rowIdx++;
        }

        if (data.EquipmentBreakdown.Count == 0)
        {
            ws.Cell(2, 1).Value = "Seçilmiş aralıqda avadanlıq əməliyyatı yoxdur";
        }

        StyleHeader(ws.Range(1, 1, 1, 5));
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
    }

    private static void StyleHeader(IXLRange range, bool bold = true)
    {
        if (bold)
        {
            range.Style.Font.Bold = true;
        }

        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F5E9");
    }
}
