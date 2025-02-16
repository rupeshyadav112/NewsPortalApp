using Microsoft.AspNetCore.Mvc;
using NewsPortalApp.DataBase;
using NewsPortalApp.Models;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace NewsPortalApp.Controllers
{
    public class ReadArticlesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReadArticlesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(int id)
        {
            var post = _context.Posts
                .Include(p => p.Comments)
                .ThenInclude(c => c.User)
                .FirstOrDefault(p => p.PostID == id);

            if (post == null)
            {
                return NotFound();
            }

            ViewBag.RecentArticles = _context.Posts
                .Where(p => p.PostID != id)
                .OrderByDescending(p => p.CreatedAt)
                .Take(3)
                .ToList();

            return View(post);
        }

        [HttpPost]
        public IActionResult AddComment(int postId, string commentText)
        {
            // Implement comment adding logic
            return RedirectToAction("Index", new { id = postId });
        }
    }
}