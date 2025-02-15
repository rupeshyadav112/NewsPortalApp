using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NewsPortalApp.DataBase; // ApplicationDbContext के लिए
using NewsPortalApp.Models;
using System;
using System.IO;
using System.Linq;

namespace NewsPortalApp.Controllers
{
    public class PostController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly ApplicationDbContext _context;

        // Constructor: IWebHostEnvironment और ApplicationDbContext को Inject करें
        public PostController(IWebHostEnvironment env, ApplicationDbContext context)
        {
            _env = env;
            _context = context;
        }

        // GET: Create Page
        public IActionResult Create()
        {
            // Categories को ViewBag के जरिए पास करें
            ViewBag.Categories = new[] { "World News", "Local News", "Technology", "Sports", "Entertainment" };
            return View();
        }

        // POST: Create New Post
        [HttpPost]
        public IActionResult Create(Post post, IFormFile fileUpload)
        {
            if (ModelState.IsValid)
            {
                // अगर Image Upload है, तो SaveUploadedFile() से Save करें
                if (fileUpload != null)
                {
                    post.ImagePath = SaveUploadedFile(fileUpload);
                }

                // Post को Database में सेव करें
                SaveToDatabase(post);

                // सफलता का संदेश
                TempData["Message"] = "Post published successfully!";
                return RedirectToAction("Create");
            }

            // Validation फेल होने पर पुनः Categories पास करें
            ViewBag.Categories = new[] { "World News", "Local News", "Technology", "Sports", "Entertainment" };
            return View(post);
        }

        // 📂 Save Image to wwwroot/Uploads
        private string SaveUploadedFile(IFormFile file)
        {
            // केवल इमेज फाइल्स की अनुमति दें
            string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif" };
            string fileExtension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
            {
                throw new Exception("Only image files (.jpg, .jpeg, .png, .gif) are allowed");
            }

            // Upload Folder का Path
            string uploadFolder = Path.Combine(_env.WebRootPath, "Uploads");
            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }

            // Unique File Name Generate करें
            string uniqueFileName = Guid.NewGuid().ToString() + fileExtension;
            string filePath = Path.Combine(uploadFolder, uniqueFileName);

            // File को Save करें
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                file.CopyTo(stream);
            }

            // File Path लौटाएं
            return $"/Uploads/{uniqueFileName}";
        }

        // 🗃️ Save Post to Database
        private void SaveToDatabase(Post post)
        {
            post.CreatedAt = DateTime.Now; // Post की तारीख सेट करें
            _context.Posts.Add(post);      // Database में Post Add करें
            _context.SaveChanges();        // Changes सेव करें
        }
    }
}
