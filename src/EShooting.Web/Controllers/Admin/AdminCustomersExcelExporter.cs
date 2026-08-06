using ClosedXML.Excel;
using EShooting.Application.Common.Models;

namespace EShooting.Web.Controllers.Admin;

public static class AdminCustomersExcelExporter
{
    public static byte[] Export(IReadOnlyCollection<CustomerListItem> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Müştərilər");
        var headers = new[]
        {
            "Ad Soyad", "Telefon", "Email", "Ş/V", "Kart", "Kateqoriya", "VIP", "Status",
            "Paket növü", "Cari paket", "Ödəniş", "Nağd", "Kart",
            "Abunə başlanğıc", "Abunə bitmə",
            "Qeydiyyata alınma", "Qeydiyyata alan", "Silinmə", "Silən",
            "Son zolağa yazılma", "Son zolaq №",
            "Oyun icarəsi", "Aktiv zolaq"
        };
        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
            ws.Cell(1, c + 1).Style.Font.Bold = true;
        }

        var r = 2;
        foreach (var x in rows)
        {
            ws.Cell(r, 1).Value = x.FullName;
            ws.Cell(r, 2).Value = x.PhoneNumber;
            ws.Cell(r, 3).Value = x.Email ?? "";
            ws.Cell(r, 4).Value = x.IdCardNumber ?? "";
            ws.Cell(r, 5).Value = x.ClubCardLabel ?? x.ClubCardNumber ?? "";
            ws.Cell(r, 6).Value = x.CategoryLabel;
            ws.Cell(r, 7).Value = x.IsVip ? "Bəli" : "Xeyr";
            ws.Cell(r, 8).Value = x.IsActive ? "Aktiv" : "Silinmiş";
            ws.Cell(r, 9).Value = x.PackageTypeLabel;
            ws.Cell(r, 10).Value = x.CurrentPackageName ?? "";
            ws.Cell(r, 11).Value = x.LatestPaymentLabel;
            ws.Cell(r, 12).Value = x.LatestAmountPaidCash is decimal csh ? csh : "";
            ws.Cell(r, 13).Value = x.LatestAmountPaidCard is decimal crd ? crd : "";
            ws.Cell(r, 14).Value = x.SubscriptionFromLocal ?? "";
            ws.Cell(r, 15).Value = x.SubscriptionToLocal ?? "";
            ws.Cell(r, 16).Value = x.RegisteredAtLocal;
            ws.Cell(r, 17).Value = x.RegisteredByStaffName;
            ws.Cell(r, 18).Value = x.DeletedAtLocal ?? "";
            ws.Cell(r, 19).Value = x.DeletedByName;
            ws.Cell(r, 20).Value = x.LastLaneVisitLocal ?? "";
            ws.Cell(r, 21).Value = x.LastLaneNumber is int ln ? ln : "";
            ws.Cell(r, 22).Value = FormatSessionRentalLabel(x);
            ws.Cell(r, 23).Value = x.ActiveLaneNumber is int aln ? $"Zolaq {aln}" : "";
            r++;
        }

        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static string FormatSessionRentalLabel(CustomerListItem x)
    {
        if (x.HasPendingSessionRental)
        {
            return "Qaytarılmamış icarə";
        }

        if (x.HasSessionEquipmentRental)
        {
            return "Bəli";
        }

        return "—";
    }
}
