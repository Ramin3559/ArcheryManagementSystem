using ClosedXML.Excel;
using EShooting.Application.Athletes.Queries;

namespace EShooting.Web.Controllers.Admin;

public static class AdminClubCardsExcelExporter
{
    public static byte[] ExportSummary(IReadOnlyList<ClubCardStockTypeSummary> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Kart icmalı");
        ws.Cell(1, 1).Value = "Kart icmalı";
        ws.Cell(1, 1).Style.Font.Bold = true;

        var headers = new[] { "Növ", "Cəmi", "Sərbəst", "Müştəridə", "Mövcud deyil" };
        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(3, c + 1).Value = headers[c];
            ws.Cell(3, c + 1).Style.Font.Bold = true;
        }

        var r = 4;
        foreach (var x in rows)
        {
            ws.Cell(r, 1).Value = x.TypeLabel;
            ws.Cell(r, 2).Value = x.Total;
            ws.Cell(r, 3).Value = x.Available;
            ws.Cell(r, 4).Value = x.Issued;
            ws.Cell(r, 5).Value = x.Deleted;
            r++;
        }

        ws.Cell(r, 1).Value = "Cəmi";
        ws.Cell(r, 1).Style.Font.Bold = true;
        ws.Cell(r, 2).Value = rows.Sum(x => x.Total);
        ws.Cell(r, 3).Value = rows.Sum(x => x.Available);
        ws.Cell(r, 4).Value = rows.Sum(x => x.Issued);
        ws.Cell(r, 5).Value = rows.Sum(x => x.Deleted);
        ws.Range(r, 1, r, 5).Style.Font.Bold = true;

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static byte[] ExportCatalog(IReadOnlyCollection<ClubCardCatalogItem> rows, string title)
    {
        using var wb = new XLWorkbook();
        var heading = string.IsNullOrWhiteSpace(title) ? "Kart məlumatları" : title.Trim();
        var sheetName = heading.Length > 31 ? heading[..31] : heading;
        var ws = wb.Worksheets.Add(sheetName);
        ws.Cell(1, 1).Value = heading;
        ws.Cell(1, 1).Style.Font.Bold = true;

        var includeName = rows.Any(x => !string.IsNullOrWhiteSpace(x.HolderName));
        var includePhone = rows.Any(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
        var headers = new List<string> { "Növ", "Nömrə", "Status" };
        if (includeName) headers.Add("Müştəri");
        if (includePhone) headers.Add("Telefon");

        for (var c = 0; c < headers.Count; c++)
        {
            ws.Cell(3, c + 1).Value = headers[c];
            ws.Cell(3, c + 1).Style.Font.Bold = true;
        }

        var r = 4;
        foreach (var x in rows)
        {
            var c = 1;
            ws.Cell(r, c++).Value = x.TypeLabel;
            ws.Cell(r, c++).Value = x.CardNumber;
            ws.Cell(r, c++).Value = string.IsNullOrWhiteSpace(x.StatusLabel)
                ? (x.Status == "free" ? "Müştəridə olmayan" : x.Status == "deleted" ? "Mövcud deyil" : x.Status)
                : x.StatusLabel;
            if (includeName) ws.Cell(r, c++).Value = x.HolderName;
            if (includePhone) ws.Cell(r, c++).Value = x.PhoneNumber;
            r++;
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
