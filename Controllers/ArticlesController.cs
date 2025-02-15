using Microsoft.AspNetCore.Mvc;

namespace NewsPortalApp.Controllers
{
    public class ArticlesController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
