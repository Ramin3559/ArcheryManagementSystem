using ClosedXML.Excel;

namespace EShooting.Web.Controllers.Admin;

public static class AdminExcelExporter
{
    public static byte[] Export(IReadOnlyCollection<HistoryRow> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Tarixçə");

        ws.Cell(1, 1).Value = "Tarix";
        ws.Cell(1, 2).Value = "Müştəri adı";
        ws.Cell(1, 3).Value = "Telefon";
        ws.Cell(1, 4).Value = "Kateqoriya";
        ws.Cell(1, 5).Value = "Zolaq";
        ws.Cell(1, 6).Value = "Giriş";
        ws.Cell(1, 7).Value = "Çıxış";
        ws.Cell(1, 8).Value = "Atış sayı";
        ws.Cell(1, 9).Value = "Cəmi xal";

        var rowIdx = 2;
        foreach (var r in rows)
        {
            ws.Cell(rowIdx, 1).Value = r.DateLocal;
            ws.Cell(rowIdx, 2).Value = r.AthleteName;
            ws.Cell(rowIdx, 3).Value = r.Phone;
            ws.Cell(rowIdx, 4).Value = r.Category;
            ws.Cell(rowIdx, 5).Value = r.LaneNumber <= 0 ? "" : r.LaneNumber;
            ws.Cell(rowIdx, 6).Value = r.StartTimeLocal;
            ws.Cell(rowIdx, 7).Value = r.EndTimeLocal;
            ws.Cell(rowIdx, 8).Value = r.ScoreCount;
            ws.Cell(rowIdx, 9).Value = r.TotalScore;
            rowIdx++;
        }

        if (rows.Count > 0)
        {
            ws.Cell(rowIdx, 1).Value = "Yekun";
            ws.Cell(rowIdx, 8).Value = rows.Sum(x => x.ScoreCount);
            ws.Cell(rowIdx, 9).Value = rows.Sum(x => x.TotalScore);
            var totalRow = ws.Range(rowIdx, 1, rowIdx, 9);
            totalRow.Style.Font.Bold = true;
            totalRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F4F6");
        }

        var header = ws.Range(1, 1, 1, 9);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F5E9");
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}

