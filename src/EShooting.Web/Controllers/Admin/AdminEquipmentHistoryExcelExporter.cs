using ClosedXML.Excel;
using EShooting.Application.Common.Models;

namespace EShooting.Web.Controllers.Admin;

public static class AdminEquipmentHistoryExcelExporter
{
    public static byte[] Export(EquipmentIssueHistoryResult result)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Avadanlıq jurnalı");
        var headers = new[]
        {
            "Tarix", "Avadanlıq", "Kateqoriya", "Növ", "Say", "Vahid qiymət (AZN)", "Cəm (AZN)",
            "Müştəri", "Zolaq", "Verən işçi", "Təhvil tarixi", "Təhvil alan işçi"
        };
        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
            ws.Cell(1, c + 1).Style.Font.Bold = true;
        }

        var r = 2;
        foreach (var x in result.Items)
        {
            ws.Cell(r, 1).Value = x.IssuedAtLocal;
            ws.Cell(r, 2).Value = x.EquipmentName;
            ws.Cell(r, 3).Value = x.Category ?? "";
            ws.Cell(r, 4).Value = x.IssueTypeLabel;
            ws.Cell(r, 5).Value = x.Quantity;
            ws.Cell(r, 6).Value = x.UnitPrice;
            ws.Cell(r, 7).Value = x.LineTotal;
            ws.Cell(r, 8).Value = x.CustomerName;
            ws.Cell(r, 9).Value = x.LaneNumber is int ln ? ln : "";
            ws.Cell(r, 10).Value = x.IssuedByStaffName;
            ws.Cell(r, 11).Value = x.ReturnedAtLocal ?? "";
            ws.Cell(r, 12).Value = x.ReturnedByStaffName ?? "";
            r++;
        }

        if (result.Items.Count > 0)
        {
            ws.Cell(r, 1).Value = "Cəmi";
            ws.Cell(r, 4).Value = $"Satış: {result.SaleQuantityTotal} ədəd";
            ws.Cell(r, 5).Value = result.SaleQuantityTotal + result.RentalQuantityTotal;
            ws.Cell(r, 7).Value = result.GrandTotal;
            ws.Range(r, 1, r, headers.Length).Style.Font.Bold = true;
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
