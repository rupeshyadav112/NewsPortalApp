using Microsoft.AspNetCore.Mvc;

namespace NewsPortalApp.Controllers
{
    public class ProfileController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
