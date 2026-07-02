using ClosedXML.Excel;
using EShooting.Application.Common.Models;

namespace EShooting.Web.Controllers.Admin;

public static class AdminEquipmentCatalogExcelExporter
{
    public static byte[] Export(IReadOnlyCollection<EquipmentCatalogItem> items)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Avadanlıqlar");
        var headers = new[]
        {
            "Ad", "Kateqoriya", "Zalda", "Satışda", "Cəmi", "Xarab", "Vahid qiymət (AZN)"
        };
        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
            ws.Cell(1, c + 1).Style.Font.Bold = true;
        }

        var r = 2;
        foreach (var x in items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            ws.Cell(r, 1).Value = x.Name;
            ws.Cell(r, 2).Value = x.Category ?? "";
            ws.Cell(r, 3).Value = x.RentalQuantity;
            ws.Cell(r, 4).Value = x.SaleQuantity;
            ws.Cell(r, 5).Value = x.Quantity;
            ws.Cell(r, 6).Value = x.DamagedQuantity;
            ws.Cell(r, 7).Value = x.UnitPrice ?? x.Price ?? 0m;
            r++;
        }

        if (items.Count > 0)
        {
            ws.Cell(r, 1).Value = "Cəmi";
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 3).Value = items.Sum(x => x.RentalQuantity);
            ws.Cell(r, 4).Value = items.Sum(x => x.SaleQuantity);
            ws.Cell(r, 5).Value = items.Sum(x => x.Quantity);
            ws.Cell(r, 6).Value = items.Sum(x => x.DamagedQuantity);
            ws.Range(r, 1, r, 7).Style.Font.Bold = true;
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
