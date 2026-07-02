using EShooting.Web.Auth;
using EShooting.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

public sealed class HomeController : Controller
{
  /// <summary>
  /// Kök URL — əlfəcinlər birbaşa resepsiya panelinə getsin.
  /// </summary>
  [HttpGet("/")]
  public IActionResult Root()
  {
    return Redirect("/qeydiyyat");
  }

  [HttpGet("/basla")]
  public IActionResult Landing()
  {
    return View();
  }

  /// <summary>
  /// Resepsiya / qeydiyyat paneli (tam funksional UI).
  /// </summary>
  [HttpGet("/qeydiyyat")]
  [HttpGet("/resepsiya")]
  [Authorize(Policy = "ReceptionPanel")]
  public IActionResult Index()
  {
    return View();
  }

    /// <summary>Müştərilərin tam siyahısı (resepsiya).</summary>
    [HttpGet("/qeydiyyat/musteriler")]
    [Authorize(Policy = "ReceptionPanel")]
    public IActionResult Customers()
    {
        if (ReceptionPermissionGate.DenyUnless(this,ReceptionStaffClaims.CanViewCustomerDetails) is { } denied)
        {
            return denied;
        }

        return View("Customers");
    }

    /// <summary>Avadanlıq satışı və geri qaytarma (resepsiya).</summary>
    [HttpGet("/qeydiyyat/avadanliq-satis")]
    [Authorize(Policy = "ReceptionPanel")]
    public IActionResult EquipmentSales()
    {
        if (!User.HasAnyReceptionPermission(
                ReceptionStaffClaims.CanSellEquipment,
                ReceptionStaffClaims.CanReturnEquipment))
        {
            return StatusCode(StatusCodes.Status403Forbidden, "Bu səhifə üçün icazəniz yoxdur.");
        }

        return View("EquipmentSales");
    }

    /// <summary>
  /// Köhnə əlfəcinlər /Home/Index üçün.
  /// </summary>
  [HttpGet("/Home/Index")]
  public IActionResult LegacyIndex()
  {
    return RedirectPermanent("/qeydiyyat");
  }
}
