using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using DentalClinic.Web.Models;

namespace DentalClinic.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("Secretary"))
                return RedirectToAction("Dashboard", "Secretary");
            if (User.IsInRole("Doctor"))
                return RedirectToAction("Dashboard", "Doctor");
            if (User.IsInRole("Customer"))
                return RedirectToAction("Dashboard", "Customer");
        }
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
