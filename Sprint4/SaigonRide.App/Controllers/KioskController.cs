using Microsoft.AspNetCore.Mvc;

namespace SaigonRide.App.Controllers
{
    public class KioskController : Controller
    {
        // GET: /Kiosk
        public IActionResult Index()
        {
            return View(); // Lúc này nó sẽ tự động tìm đúng Views/Kiosk/Index.cshtml
        }
    }
}