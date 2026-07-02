using ClosedXML.Excel;
using EShooting.Application.Common.Models;

namespace EShooting.Web.Controllers.Admin;

public static class AdminAnalyticsExcelExporter
{
    public static byte[] Export(OperationsAnalyticsResult data, string? section = null)
    {
        using var wb = new XLWorkbook();
        var key = (section ?? "all").Trim().ToLowerInvariant();

        switch (key)
        {
            case "finance":
                WriteFinanceSheet(wb, data);
                break;
            case "operations":
                WriteOperationsSheet(wb, data);
                break;
            case "daily":
                WriteDailySheet(wb, data);
                break;
            case "lanes":
                WriteLaneSheet(wb, data);
                break;
            case "equipment":
                WriteEquipmentSheet(wb, data);
                break;
            case "customers":
                WriteCustomersSheet(wb, data);
                break;
            default:
                WriteFinanceSheet(wb, data);
                WriteOperationsSheet(wb, data);
                WriteCustomersSheet(wb, data);
                WriteDailySheet(wb, data);
                WriteLaneSheet(wb, data);
                WriteEquipmentSheet(wb, data);
                break;
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void WriteFinanceSheet(XLWorkbook wb, OperationsAnalyticsResult data)
    {
        var ws = wb.Worksheets.Add("Maliyyə icmalı");
        ws.Cell(1, 1).Value = "Maliyyə icmalı";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = "Tarix aralığı";
        ws.Cell(2, 2).Value = data.Label;

        var headers = new[] { "Göstərici", "Zolaq / paket", "Avadanlıq satışı", "Cəmi" };
        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(4, c + 1).Value = headers[c];
        }

        var tableRows = new (string Label, object Lane, object Sale, object Total)[]
        {
            ("Seans / satış (ədəd)", data.SessionCount, data.StandaloneEquipmentSaleCount, "—"),
            ("Ödənilməli (₼)", data.PackagePriceDue, data.StandaloneEquipmentSaleDue, data.TotalPriceDue),
            ("Nağd (₼)", data.PackagePaidCash, data.StandaloneEquipmentPaidCash, data.TotalPaidCash),
            ("Kart (₼)", data.PackagePaidCard, data.StandaloneEquipmentPaidCard, data.TotalPaidCard),
            ("Gəlir — ödənilib (₼)", data.PackagePaidTotal, data.StandaloneEquipmentPaidTotal, data.TotalPaid)
        };

        var rowIdx = 5;
        foreach (var row in tableRows)
        {
            ws.Cell(rowIdx, 1).Value = row.Label;
            ws.Cell(rowIdx, 2).Value = XLCellValue.FromObject(row.Lane);
            ws.Cell(rowIdx, 3).Value = XLCellValue.FromObject(row.Sale);
            ws.Cell(rowIdx, 4).Value = XLCellValue.FromObject(row.Total);
            rowIdx++;
        }

        StyleHeader(ws.Range(4, 1, 4, headers.Length));
        StyleHeader(ws.Range(rowIdx - 1, 1, rowIdx - 1, headers.Length), bold: true);
        ws.Columns().AdjustToContents();
    }

    private static void WriteOperationsSheet(XLWorkbook wb, OperationsAnalyticsResult data)
    {
        var rows = new (string Label, object Value)[]
        {
            ("Gələn müştəri (unikal)", data.UniqueCustomerCount),
            ("Yeni müştəri", data.NewCustomerCount),
            ("Seans sayı", data.SessionCount),
            ("Yeni abunə yazılışı", data.SubscriptionCreatedCount),
            ("Ödənişsiz yazılış", data.ComplimentaryCount),
            ("Paket qeydi", data.PackageRecordCount),
            ("Avadanlıq satışı (ədəd)", data.EquipmentSaleCount),
            ("İcarə verilib (aralıqda, ədəd)", data.EquipmentRentalIssuedCount),
            ("İcarə qaytarılıb (aralıqda, ədəd)", data.EquipmentRentalReturnedCount),
            ("Müştəridə / zolaqda (cari, ədəd)", data.EquipmentRentalOutstandingCount),
            ("Zolaq aktiv saatı (cəmi)", data.TotalLaneHours),
            ("Ən yüklü zolaq", data.BusiestLaneNumber.HasValue ? $"Zolaq {data.BusiestLaneNumber}" : "—")
        };

        WriteKeyValueSheet(wb, "Əməliyyat icmalı", data.Label, rows);
    }

    private static void WriteKeyValueSheet(
        XLWorkbook wb,
        string title,
        string rangeLabel,
        IReadOnlyList<(string Label, object Value)> rows)
    {
        var ws = wb.Worksheets.Add(title.Length > 31 ? title[..31] : title);
        ws.Cell(1, 1).Value = title;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = "Tarix aralığı";
        ws.Cell(2, 2).Value = rangeLabel;

        var rowIdx = 4;
        foreach (var (label, value) in rows)
        {
            ws.Cell(rowIdx, 1).Value = label;
            ws.Cell(rowIdx, 2).Value = XLCellValue.FromObject(value);
            rowIdx++;
        }

        StyleHeader(ws.Range(4, 1, 4 + rows.Count - 1, 1), bold: true);
        ws.Columns().AdjustToContents();
    }

    private static void WriteDailySheet(XLWorkbook wb, OperationsAnalyticsResult data)
    {
        var ws = wb.Worksheets.Add("Günlük icmal");
        var headers = new[]
        {
            "Tarix", "Gələn müştəri", "Yeni müştəri", "Seans sayı", "Yeni abunə", "Ödənişsiz yazılış",
            "Zolaq ödənilməli (₼)", "Zolaq nağd (₼)", "Zolaq kart (₼)", "Zolaq ödənilib (₼)",
            "Avadanlıq satışı (ədəd)", "Avadanlıq gəliri (₼)", "İcarə verilib (ədəd)", "İcarə qaytarılıb (ədəd)",
            "Cəmi ödənilməli (₼)", "Cəmi nağd (₼)", "Cəmi kart (₼)", "Cəmi ödənilib (₼)",
            "Zolaq saatı"
        };

        ws.Cell(1, 1).Value = "Tarix aralığı";
        ws.Cell(1, 2).Value = data.Label;

        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(3, c + 1).Value = headers[c];
        }

        var rowIdx = 4;
        foreach (var row in data.DailyBreakdown)
        {
            WriteDailyRow(ws, rowIdx, row);
            rowIdx++;
        }

        if (data.DailyBreakdown.Count > 0)
        {
            ws.Cell(rowIdx, 1).Value = "Cəmi";
            ws.Cell(rowIdx, 2).Value = data.DailyTotals.UniqueCustomerCount;
            ws.Cell(rowIdx, 3).Value = data.DailyTotals.NewCustomerCount;
            ws.Cell(rowIdx, 4).Value = data.DailyTotals.SessionCount;
            ws.Cell(rowIdx, 5).Value = data.DailyTotals.SubscriptionCreatedCount;
            ws.Cell(rowIdx, 6).Value = data.DailyTotals.ComplimentaryCount;
            ws.Cell(rowIdx, 7).Value = data.DailyTotals.PackagePriceDue;
            ws.Cell(rowIdx, 8).Value = data.DailyTotals.PackagePaidCash;
            ws.Cell(rowIdx, 9).Value = data.DailyTotals.PackagePaidCard;
            ws.Cell(rowIdx, 10).Value = data.DailyTotals.PackagePaidTotal;
            ws.Cell(rowIdx, 11).Value = data.DailyTotals.EquipmentSaleCount;
            ws.Cell(rowIdx, 12).Value = data.DailyTotals.EquipmentSaleRevenue;
            ws.Cell(rowIdx, 13).Value = data.DailyTotals.EquipmentRentalIssuedCount;
            ws.Cell(rowIdx, 14).Value = data.DailyTotals.EquipmentRentalReturnedCount;
            ws.Cell(rowIdx, 15).Value = data.DailyTotals.TotalPriceDue;
            ws.Cell(rowIdx, 16).Value = data.DailyTotals.TotalPaidCash;
            ws.Cell(rowIdx, 17).Value = data.DailyTotals.TotalPaidCard;
            ws.Cell(rowIdx, 18).Value = data.DailyTotals.TotalPaid;
            ws.Cell(rowIdx, 19).Value = data.DailyTotals.LaneHoursTotal;
            ws.Range(rowIdx, 1, rowIdx, headers.Length).Style.Font.Bold = true;
        }

        StyleHeader(ws.Range(3, 1, 3, headers.Length));
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(3);
    }

    private static void WriteDailyRow(IXLWorksheet ws, int rowIdx, DailyOperationsRow row)
    {
        ws.Cell(rowIdx, 1).Value = row.DateLocal;
        ws.Cell(rowIdx, 2).Value = row.UniqueCustomerCount;
        ws.Cell(rowIdx, 3).Value = row.NewCustomerCount;
        ws.Cell(rowIdx, 4).Value = row.SessionCount;
        ws.Cell(rowIdx, 5).Value = row.SubscriptionCreatedCount;
        ws.Cell(rowIdx, 6).Value = row.ComplimentaryCount;
        ws.Cell(rowIdx, 7).Value = row.PackagePriceDue;
        ws.Cell(rowIdx, 8).Value = row.PackagePaidCash;
        ws.Cell(rowIdx, 9).Value = row.PackagePaidCard;
        ws.Cell(rowIdx, 10).Value = row.PackagePaidTotal;
        ws.Cell(rowIdx, 11).Value = row.EquipmentSaleCount;
        ws.Cell(rowIdx, 12).Value = row.EquipmentSaleRevenue;
        ws.Cell(rowIdx, 13).Value = row.EquipmentRentalIssuedCount;
        ws.Cell(rowIdx, 14).Value = row.EquipmentRentalReturnedCount;
        ws.Cell(rowIdx, 15).Value = row.TotalPriceDue;
        ws.Cell(rowIdx, 16).Value = row.TotalPaidCash;
        ws.Cell(rowIdx, 17).Value = row.TotalPaidCard;
        ws.Cell(rowIdx, 18).Value = row.TotalPaid;
        ws.Cell(rowIdx, 19).Value = row.LaneHoursTotal;
    }

    private static void WriteLaneSheet(XLWorkbook wb, OperationsAnalyticsResult data)
    {
        var ws = wb.Worksheets.Add("Zolaq aktivliyi");
        ws.Cell(1, 1).Value = "Tarix aralığı";
        ws.Cell(1, 2).Value = data.Label;
        ws.Cell(3, 1).Value = "Zolaq nömrəsi";
        ws.Cell(3, 2).Value = "Seans sayı";
        ws.Cell(3, 3).Value = "Aktiv saat";

        var rowIdx = 4;
        foreach (var row in data.LaneActivity.Where(x => x.SessionCount > 0 || x.TotalHours > 0))
        {
            ws.Cell(rowIdx, 1).Value = row.LaneNumber;
            ws.Cell(rowIdx, 2).Value = row.SessionCount;
            ws.Cell(rowIdx, 3).Value = row.TotalHours;
            rowIdx++;
        }

        if (rowIdx == 4)
        {
            ws.Cell(4, 1).Value = "—";
            ws.Cell(4, 2).Value = 0;
            ws.Cell(4, 3).Value = 0;
        }

        StyleHeader(ws.Range(3, 1, 3, 3));
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(3);
    }

    private static void WriteEquipmentSheet(XLWorkbook wb, OperationsAnalyticsResult data)
    {
        var ws = wb.Worksheets.Add("Avadanlıq hesabatı");
        ws.Cell(1, 1).Value = "Tarix aralığı";
        ws.Cell(1, 2).Value = data.Label;

        var headers = new[]
        {
            "Tarix", "Saat", "Avadanlıq adı", "Cəmi stok", "İcarə", "Satış",
            "Satılan (ədəd)", "Vahid qiymət (₼)", "Məbləğ (₼)", "Endirim (₼)", "Nağd (₼)", "Kart (₼)",
            "Müştəri", "Satıcı", "Mənbə"
        };

        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(3, c + 1).Value = headers[c];
        }

        var rowIdx = 4;
        foreach (var row in data.EquipmentSaleDetails)
        {
            ws.Cell(rowIdx, 1).Value = row.DateLocal;
            ws.Cell(rowIdx, 2).Value = row.TimeLocal;
            ws.Cell(rowIdx, 3).Value = row.EquipmentName;
            ws.Cell(rowIdx, 4).Value = row.TotalQuantity;
            ws.Cell(rowIdx, 5).Value = row.InHallQuantity;
            ws.Cell(rowIdx, 6).Value = row.ForSaleQuantity;
            ws.Cell(rowIdx, 7).Value = row.SoldQuantity;
            ws.Cell(rowIdx, 8).Value = row.UnitPrice;
            ws.Cell(rowIdx, 9).Value = row.LineTotal;
            ws.Cell(rowIdx, 10).Value = row.DiscountAmount;
            ws.Cell(rowIdx, 11).Value = row.PaidCash;
            ws.Cell(rowIdx, 12).Value = row.PaidCard;
            ws.Cell(rowIdx, 13).Value = row.CustomerName;
            ws.Cell(rowIdx, 14).Value = row.SoldByStaffName;
            ws.Cell(rowIdx, 15).Value = row.SaleSource;
            rowIdx++;
        }

        if (data.EquipmentSaleDetails.Count == 0)
        {
            ws.Cell(4, 1).Value = "Seçilmiş aralıqda avadanlıq satışı yoxdur";
        }
        else
        {
            var sumRow = rowIdx;
            ws.Cell(sumRow, 1).Value = "Cəmi";
            ws.Cell(sumRow, 7).Value = data.EquipmentSaleDetails.Sum(x => x.SoldQuantity);
            ws.Cell(sumRow, 9).Value = data.EquipmentSaleDetails.Sum(x => x.LineTotal);
            ws.Cell(sumRow, 10).Value = data.EquipmentSaleDetails.Sum(x => x.DiscountAmount);
            ws.Cell(sumRow, 11).Value = data.EquipmentSaleDetails.Sum(x => x.PaidCash);
            ws.Cell(sumRow, 12).Value = data.EquipmentSaleDetails.Sum(x => x.PaidCard);
            ws.Range(sumRow, 1, sumRow, headers.Length).Style.Font.Bold = true;
        }

        StyleHeader(ws.Range(3, 1, 3, headers.Length));
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(3);
    }

    private static void WriteCustomersSheet(XLWorkbook wb, OperationsAnalyticsResult data)
    {
        var ws = wb.Worksheets.Add("Müştəri detalları");
        ws.Cell(1, 1).Value = "Tarix aralığı";
        ws.Cell(1, 2).Value = data.Label;

        var headers = new[]
        {
            "Tarix", "Müştəri adı", "Telefon", "Resepsiya (qeydiyyatçı)", "Nəzarətçi (planşet)",
            "Paket adı", "Yazılış vaxtı", "Zolaq", "Başlama", "Bitmə", "Oyun müddəti",
            "Ödənilməli (₼)", "Nağd (₼)", "Kart (₼)", "Ödənilib (₼)", "Endirim (₼)", "Ödənişsiz"
        };

        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(3, c + 1).Value = headers[c];
        }

        var rowIdx = 4;
        foreach (var row in data.CustomerVisitDetails)
        {
            ws.Cell(rowIdx, 1).Value = row.DateLocal;
            ws.Cell(rowIdx, 2).Value = row.CustomerName;
            ws.Cell(rowIdx, 3).Value = row.Phone;
            ws.Cell(rowIdx, 4).Value = row.ReceptionStaffName;
            ws.Cell(rowIdx, 5).Value = row.SupervisorStaffName;
            ws.Cell(rowIdx, 6).Value = row.PackageName;
            ws.Cell(rowIdx, 7).Value = row.RecordedAtLocal;
            ws.Cell(rowIdx, 8).Value = row.LaneNumber.HasValue ? $"Zolaq {row.LaneNumber}" : "—";
            ws.Cell(rowIdx, 9).Value = row.StartTimeLocal;
            ws.Cell(rowIdx, 10).Value = row.EndTimeLocal;
            ws.Cell(rowIdx, 11).Value = row.DurationLabel;
            ws.Cell(rowIdx, 12).Value = row.PriceDue;
            ws.Cell(rowIdx, 13).Value = row.AmountPaidCash;
            ws.Cell(rowIdx, 14).Value = row.AmountPaidCard;
            ws.Cell(rowIdx, 15).Value = row.AmountPaid;
            ws.Cell(rowIdx, 16).Value = row.DiscountAmount;
            ws.Cell(rowIdx, 17).Value = row.IsComplimentary ? "Bəli" : "Xeyr";
            rowIdx++;
        }

        if (data.CustomerVisitDetails.Count == 0)
        {
            ws.Cell(4, 1).Value = "Seçilmiş aralıqda seans qeydi yoxdur";
        }

        StyleHeader(ws.Range(3, 1, 3, headers.Length));
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(3);
    }

    private static void StyleHeader(IXLRange range, bool bold = true)
    {
        if (bold)
        {
            range.Style.Font.Bold = true;
        }

        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F5E9");
    }

    public static byte[] ExportGrid(
        string sheetName,
        string? subtitle,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        using var wb = new XLWorkbook();
        var safeName = sheetName.Length > 31 ? sheetName[..31] : sheetName;
        var ws = wb.Worksheets.Add(safeName);

        var headerRow = string.IsNullOrWhiteSpace(subtitle) ? 1 : 3;
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            ws.Cell(1, 1).Value = subtitle;
            ws.Cell(1, 1).Style.Font.Bold = true;
        }

        for (var c = 0; c < headers.Count; c++)
        {
            ws.Cell(headerRow, c + 1).Value = headers[c];
        }

        var rowIdx = headerRow + 1;
        foreach (var row in rows)
        {
            for (var c = 0; c < headers.Count; c++)
            {
                ws.Cell(rowIdx, c + 1).Value = c < row.Count ? row[c] : "";
            }
            rowIdx++;
        }

        StyleHeader(ws.Range(headerRow, 1, headerRow, headers.Count));
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(headerRow);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
