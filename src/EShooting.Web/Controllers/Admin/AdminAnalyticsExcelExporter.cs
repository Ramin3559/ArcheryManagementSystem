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
        StyleTotalRow(ws.Range(rowIdx - 1, 1, rowIdx - 1, headers.Length));
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
            StyleTotalRow(ws.Range(rowIdx, 1, rowIdx, headers.Length));
            ws.Cell(rowIdx, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
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
            ws.Cell(sumRow, 7).Value = data.EquipmentSaleDetails.Sum(x => x.SoldQuantity);
            ws.Cell(sumRow, 9).Value = data.EquipmentSaleDetails.Sum(x => x.LineTotal);
            ws.Cell(sumRow, 10).Value = data.EquipmentSaleDetails.Sum(x => x.DiscountAmount);
            ws.Cell(sumRow, 11).Value = data.EquipmentSaleDetails.Sum(x => x.PaidCash);
            ws.Cell(sumRow, 12).Value = data.EquipmentSaleDetails.Sum(x => x.PaidCard);
            ApplyMergedCemiLabel(ws, sumRow, totalCols: headers.Length);
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
            "Tarix", "Vaxt", "Müştəri adı", "Telefon", "Resepsiya",
            "Paket adı", "Paket qiyməti (₼)", "Avadanlıq (₼)", "Endirim (₼)",
            "Nağd (₼)", "Kart (₼)", "Ödənilib (₼)", "Ödənişsiz"
        };

        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(3, c + 1).Value = headers[c];
        }

        var rowIdx = 4;
        foreach (var row in data.CustomerVisitDetails)
        {
            ws.Cell(rowIdx, 1).Value = row.DateLocal;
            ws.Cell(rowIdx, 2).Value = row.RecordedAtLocal;
            ws.Cell(rowIdx, 3).Value = row.CustomerName;
            ws.Cell(rowIdx, 4).Value = row.Phone;
            ws.Cell(rowIdx, 5).Value = row.ReceptionStaffName;
            ws.Cell(rowIdx, 6).Value = row.PackageName;
            ws.Cell(rowIdx, 7).Value = row.PriceDue;
            ws.Cell(rowIdx, 8).Value = row.EquipmentAmount;
            ws.Cell(rowIdx, 9).Value = row.DiscountAmount;
            ws.Cell(rowIdx, 10).Value = row.AmountPaidCash;
            ws.Cell(rowIdx, 11).Value = row.AmountPaidCard;
            ws.Cell(rowIdx, 12).Value = row.AmountPaid;
            ws.Cell(rowIdx, 13).Value = row.IsComplimentary ? "Bəli" : "Xeyr";
            rowIdx++;
        }

        if (data.CustomerVisitDetails.Count == 0)
        {
            ws.Cell(4, 1).Value = "Seçilmiş aralıqda ödəniş qeydi yoxdur";
        }
        else
        {
            var sumRow = rowIdx;
            ws.Cell(sumRow, 7).Value = data.CustomerVisitDetails.Sum(x => x.PriceDue);
            ws.Cell(sumRow, 8).Value = data.CustomerVisitDetails.Sum(x => x.EquipmentAmount);
            ws.Cell(sumRow, 9).Value = data.CustomerVisitDetails.Sum(x => x.DiscountAmount);
            ws.Cell(sumRow, 10).Value = data.CustomerVisitDetails.Sum(x => x.AmountPaidCash);
            ws.Cell(sumRow, 11).Value = data.CustomerVisitDetails.Sum(x => x.AmountPaidCard);
            ws.Cell(sumRow, 12).Value = data.CustomerVisitDetails.Sum(x => x.AmountPaid);
            ApplyMergedCemiLabel(ws, sumRow, totalCols: headers.Length);
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

    private static void StyleTotalRow(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontSize = 13;
        range.Style.Font.FontColor = XLColor.Black;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#D0D0D0");
    }

    /// <summary>
    /// In the total row only: merge every cell from col 1 up to (but not including)
    /// the first real value, put "Cəmi" right-aligned in that merged cell.
    /// </summary>
    private static void ApplyMergedCemiLabel(IXLWorksheet ws, int row, int totalCols)
    {
        if (totalCols < 1)
        {
            return;
        }

        var firstValueCol = FindFirstValueColumn(ws, row, totalCols);
        var labelThroughCol = firstValueCol > 1 ? firstValueCol - 1 : 1;

        // Unmerge anything already spanning this row (safe even if none).
        foreach (var merged in ws.MergedRanges
                     .Where(r => r.FirstRow().RowNumber() <= row && r.LastRow().RowNumber() >= row)
                     .ToList())
        {
            merged.Unmerge();
        }

        // Clear only this row's label area (does not touch rows above).
        for (var c = 1; c <= labelThroughCol; c++)
        {
            ws.Cell(row, c).Clear();
        }

        if (labelThroughCol > 1)
        {
            ws.Range(row, 1, row, labelThroughCol).Merge();
        }

        var labelCell = ws.Cell(row, 1);
        labelCell.Value = "Cəmi";

        StyleTotalRow(ws.Range(row, 1, row, totalCols));
        labelCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        labelCell.Style.Font.Bold = true;
        labelCell.Style.Font.FontSize = 13;
        labelCell.Style.Font.FontColor = XLColor.Black;
    }

    private static int FindFirstValueColumn(IXLWorksheet ws, int row, int totalCols)
    {
        for (var c = 1; c <= totalCols; c++)
        {
            var cell = ws.Cell(row, c);
            if (cell.IsEmpty())
            {
                continue;
            }

            var text = cell.GetFormattedString().Trim();
            if (string.IsNullOrEmpty(text)
                || text.Equals("Cəmi", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return c;
        }

        return totalCols + 1;
    }

    private static int FindFirstValueColumn(IReadOnlyList<string> row)
    {
        for (var i = 0; i < row.Count; i++)
        {
            var t = (row[i] ?? "").Trim();
            if (string.IsNullOrEmpty(t)
                || t.Equals("Cəmi", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return i + 1; // 1-based
        }

        return row.Count + 1;
    }

    private static bool IsTotalLabelRow(IReadOnlyList<string> row)
    {
        foreach (var cell in row)
        {
            var t = (cell ?? "").Trim();
            if (t.Equals("Cəmi", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("Gəlir", StringComparison.OrdinalIgnoreCase)
                || t.Contains("ödənilib", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
        var lastDataRowIdx = -1;
        foreach (var row in rows)
        {
            for (var c = 0; c < headers.Count; c++)
            {
                var raw = c < row.Count ? row[c] : "";
                // Prefer numeric cells so totals stay numbers in Excel.
                if (double.TryParse(raw, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var num)
                    && !string.Equals(raw.Trim(), "Cəmi", StringComparison.OrdinalIgnoreCase))
                {
                    ws.Cell(rowIdx, c + 1).Value = num;
                }
                else
                {
                    ws.Cell(rowIdx, c + 1).Value = raw;
                }
            }
            lastDataRowIdx = rowIdx;
            rowIdx++;
        }

        StyleHeader(ws.Range(headerRow, 1, headerRow, headers.Count));

        if (rows.Count > 0 && lastDataRowIdx > 0 && IsTotalLabelRow(rows[^1]))
        {
            var last = rows[^1];
            if (last.Any(c => string.Equals((c ?? "").Trim(), "Cəmi", StringComparison.OrdinalIgnoreCase)))
            {
                // Ensure label area is blank before auto-merge (Cəmi may sit in any label col).
                var firstValueCol = FindFirstValueColumn(last);
                var through = firstValueCol > 1 ? firstValueCol - 1 : 1;
                for (var c = 1; c <= through; c++)
                {
                    ws.Cell(lastDataRowIdx, c).Clear();
                }

                ApplyMergedCemiLabel(ws, lastDataRowIdx, headers.Count);
            }
            else
            {
                StyleTotalRow(ws.Range(lastDataRowIdx, 1, lastDataRowIdx, headers.Count));
            }
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(headerRow);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
