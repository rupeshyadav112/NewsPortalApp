using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewsPortalApp.DataBase;
using NewsPortalApp.Models;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NewsPortalApp.Controllers
{
    public class ReadArticlesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReadArticlesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: ReadArticles/Index/{id}
        public IActionResult Index(int id)
        {
            var post = _context.Posts
                .Include(p => p.Comments)
                    .ThenInclude(c => c.User)
                .Include(p => p.Comments)
                    .ThenInclude(c => c.Likes)
                .FirstOrDefault(p => p.PostID == id);

            if (post == null) return NotFound();

            ViewBag.RecentArticles = _context.Posts
                .Where(p => p.PostID != id)
                .OrderByDescending(p => p.CreatedAt)
                .Take(3)
                .ToList();

            return View(post);
        }

        // DTO for comment submission
        public class CommentDto
        {
            public string CommentText { get; set; }
        }

        // POST: ReadArticles/AddComment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int postId, [FromBody] CommentDto commentDto)
        {
            // Debug logging
            Console.WriteLine($"AddComment called: postId={postId}, commentText={commentDto?.CommentText}");

            if (!User.Identity.IsAuthenticated)
            {
                Console.WriteLine("User not authenticated");
                return Unauthorized();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Console.WriteLine($"User ID from claims: {userId}");
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int parsedUserId))
            {
                Console.WriteLine("Invalid user ID");
                return BadRequest("Invalid user ID.");
            }

            if (string.IsNullOrEmpty(commentDto?.CommentText))
            {
                Console.WriteLine("Comment text is empty");
                return BadRequest("Comment text cannot be empty.");
            }

            if (!_context.Posts.Any(p => p.PostID == postId))
            {
                Console.WriteLine($"Post ID {postId} not found");
                return BadRequest("Invalid Post ID.");
            }

            if (!_context.Users.Any(u => u.UserID == parsedUserId))
            {
                Console.WriteLine($"User ID {parsedUserId} not found");
                return BadRequest("Invalid User ID.");
            }

            var comment = new Comment
            {
                PostID = postId,
                UserID = parsedUserId,
                CommentText = commentDto.CommentText,
                CreatedAt = DateTime.Now,
                ModifiedAt = null,
                NumberOfLikes = 0
            };

            try
            {
                Console.WriteLine("Adding comment to context");
                _context.Comments.Add(comment);
                Console.WriteLine("Saving changes to database");
                await _context.SaveChangesAsync();
                Console.WriteLine("Comment saved successfully");
                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database error: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                return StatusCode(500, $"Database error: {ex.Message}");
            }
        }

        // POST: ReadArticles/EditComment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditComment(int commentId, [FromBody] CommentDto commentDto)
        {
            // Debug logging
            Console.WriteLine($"EditComment called: commentId={commentId}, commentText={commentDto?.CommentText}");

            if (!User.Identity.IsAuthenticated)
            {
                Console.WriteLine("User not authenticated");
                return Unauthorized();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Console.WriteLine($"User ID from claims: {userId}");
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int parsedUserId))
            {
                Console.WriteLine("Invalid user ID");
                return BadRequest("Invalid user ID.");
            }

            var comment = await _context.Comments
                .FirstOrDefaultAsync(c => c.CommentID == commentId && c.UserID == parsedUserId);

            if (comment == null)
            {
                Console.WriteLine($"Comment {commentId} not found or not owned by user {parsedUserId}");
                return NotFound("Comment not found.");
            }

            if (string.IsNullOrEmpty(commentDto?.CommentText))
            {
                Console.WriteLine("Comment text is empty");
                return BadRequest("Comment text cannot be empty.");
            }

            comment.CommentText = commentDto.CommentText;
            comment.ModifiedAt = DateTime.Now;

            try
            {
                Console.WriteLine("Updating comment in database");
                _context.Comments.Update(comment);
                await _context.SaveChangesAsync();
                Console.WriteLine("Comment updated successfully");
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Database error: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                return StatusCode(500, $"Database error: {ex.Message}");
            }
        }

        // POST: ReadArticles/DeleteComment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int commentId)
        {
            if (!User.Identity.IsAuthenticated)
                return Unauthorized();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int parsedUserId))
                return BadRequest("Invalid user ID.");

            var comment = await _context.Comments
                .FirstOrDefaultAsync(c => c.CommentID == commentId && c.UserID == parsedUserId);

            if (comment == null)
                return NotFound("Comment not found.");

            try
            {
                _context.Comments.Remove(comment);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Database error: {ex.Message}");
            }
        }

        // POST: ReadArticles/ToggleLike
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLike(int commentId)
        {
            if (!User.Identity.IsAuthenticated)
                return Unauthorized();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId) || !int.TryParse(userId, out int parsedUserId))
                return BadRequest("Invalid user ID.");

            var comment = await _context.Comments
                .Include(c => c.Likes)
                .FirstOrDefaultAsync(c => c.CommentID == commentId);

            if (comment == null)
                return NotFound("Comment not found.");

            var existingLike = comment.Likes
                .FirstOrDefault(l => l.UserID == parsedUserId);

            try
            {
                if (existingLike == null)
                {
                    _context.CommentLikes.Add(new CommentLike
                    {
                        CommentID = commentId,
                        UserID = parsedUserId,
                        CreatedAt = DateTime.Now
                    });
                    comment.NumberOfLikes += 1;
                }
                else
                {
                    _context.CommentLikes.Remove(existingLike);
                    comment.NumberOfLikes -= 1;
                }

                _context.Comments.Update(comment);
                await _context.SaveChangesAsync();
                return Json(new { success = true, numberOfLikes = comment.NumberOfLikes });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Database error: {ex.Message}");
            }
        }
    }
}