using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using CapYandal.Web.Models;
using Microsoft.AspNetCore.Authorization;

namespace CapYandal.Web.Controllers;

[AllowAnonymous]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        return RedirectToAction("Giris", "Hesap");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
