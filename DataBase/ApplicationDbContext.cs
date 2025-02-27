using Microsoft.EntityFrameworkCore;
using NewsPortalApp.Models;

namespace NewsPortalApp.DataBase
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // डेटाबेस टेबल्स के लिए DbSet
        public DbSet<User> Users { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<CommentLike> CommentLikes { get; set; }

        // मॉडल कॉन्फ़िगरेशन - रिलेशनशिप्स सेट करना
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Comment और Post का रिलेशन
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Post)
                .WithMany(p => p.Comments)
                .HasForeignKey(c => c.PostID);

            // Comment और User का रिलेशन
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(c => c.UserID);

            // CommentLike और Comment का रिलेशन
            modelBuilder.Entity<CommentLike>()
                .HasOne(cl => cl.Comment)
                .WithMany(c => c.Likes)
                .HasForeignKey(cl => cl.CommentID);

            // CommentLike और User का रिलेशन
            modelBuilder.Entity<CommentLike>()
                .HasOne(cl => cl.User)
                .WithMany(u => u.Likes)
                .HasForeignKey(cl => cl.UserID);
        }

        // डेटाबेस लॉगिंग जोड़ना (ऑप्शनल, डिबगिंग के लिए)
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer("DefaultConnection"); // अगर जरूरत हो तो यहाँ सेट करें
            }
            optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information); // SQL क्वेरीज़ और एरर लॉग करना
        }
    }
}