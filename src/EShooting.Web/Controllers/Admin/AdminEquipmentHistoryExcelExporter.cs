using ClosedXML.Excel;
using EShooting.Application.Common.Models;
using EShooting.Domain.Enums;

namespace EShooting.Web.Controllers.Admin;

public static class AdminEquipmentHistoryExcelExporter
{
    public static byte[] Export(EquipmentIssueHistoryResult result, EquipmentIssueType? issueType)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Avadanlıq jurnalı");
        var mode = issueType switch
        {
            EquipmentIssueType.Sale => "sale",
            EquipmentIssueType.Rental => "rental",
            _ => "all"
        };
        var showPrice = mode is "all" or "sale";
        var showDamaged = mode is "all" or "rental";

        var headers = new List<string> { "Tarix", "Avadanlıq", "Kateqoriya", "Növ", "Say" };
        if (showPrice)
        {
            headers.Add("Vahid qiymət (AZN)");
            headers.Add("Cəm (AZN)");
        }
        headers.AddRange(["Müştəri", "Zolaq", "Verən işçi", "Təhvil tarixi", "Təhvil alan işçi"]);
        if (showDamaged)
        {
            headers.Add("Xarab");
        }

        for (var c = 0; c < headers.Count; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
            ws.Cell(1, c + 1).Style.Font.Bold = true;
        }

        var r = 2;
        foreach (var x in result.Items)
        {
            var c = 1;
            ws.Cell(r, c++).Value = x.IssuedAtLocal;
            ws.Cell(r, c++).Value = x.EquipmentName;
            ws.Cell(r, c++).Value = x.Category ?? "";
            ws.Cell(r, c++).Value = x.IssueTypeLabel;
            ws.Cell(r, c++).Value = x.Quantity;
            if (showPrice)
            {
                if (x.IssueType is EquipmentIssueType.Rental or EquipmentIssueType.AdminDamage)
                {
                    ws.Cell(r, c++).Value = "—";
                    ws.Cell(r, c++).Value = "—";
                }
                else
                {
                    ws.Cell(r, c++).Value = x.UnitPrice;
                    ws.Cell(r, c++).Value = x.LineTotal;
                }
            }
            ws.Cell(r, c++).Value = x.CustomerName;
            ws.Cell(r, c++).Value = x.LaneNumber is int ln ? ln : "";
            ws.Cell(r, c++).Value = x.IssuedByStaffName;
            if (x.IssueType == EquipmentIssueType.AdminDamage)
            {
                ws.Cell(r, c++).Value = "—";
                ws.Cell(r, c++).Value = "—";
            }
            else
            {
                ws.Cell(r, c++).Value = x.ReturnedAtLocal ?? "";
                ws.Cell(r, c++).Value = x.ReturnedByStaffName ?? "";
            }
            if (showDamaged)
            {
                ws.Cell(r, c++).Value = FormatDamaged(x);
            }
            r++;
        }

        if (result.Items.Count > 0)
        {
            ws.Cell(r, 1).Value = "Cəmi";
            var summary = mode switch
            {
                "sale" => $"Satış: {result.SaleQuantityTotal} ədəd · {result.GrandTotal:0.00} AZN",
                "rental" => $"İcarə: {result.RentalQuantityTotal} ədəd · Xarab: {result.DamagedQuantityTotal} ədəd",
                _ => $"Satış: {result.SaleQuantityTotal} ədəd · {result.GrandTotal:0.00} AZN · İcarə: {result.RentalQuantityTotal} ədəd · Xarab: {result.DamagedQuantityTotal} ədəd"
            };
            ws.Cell(r, 4).Value = summary;
            ws.Range(r, 1, r, headers.Count).Style.Font.Bold = true;
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return stream.ToArray();
    }

    private static string FormatDamaged(EquipmentIssueHistoryRow x)
    {
        if (x.IssueType is not (EquipmentIssueType.Rental or EquipmentIssueType.AdminDamage))
        {
            return "—";
        }

        return x.DamagedQuantity is int d ? d.ToString() : "—";
    }
}
