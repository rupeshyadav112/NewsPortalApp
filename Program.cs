using Microsoft.EntityFrameworkCore;
using NewsPortalApp.DataBase;
using Microsoft.AspNetCore.Authentication.Cookies; // कुकी ऑथेंटिकेशन के लिए

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient(); // .NET 6+
// कंट्रोलर और व्यूज़ को सर्विस में जोड़ना
builder.Services.AddControllersWithViews();

// डेटाबेस कॉन्फ़िगरेशन - SQL Server के साथ ApplicationDbContext जोड़ना
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .LogTo(Console.WriteLine, LogLevel.Information)); // डेटाबेस लॉगिंग जोड़ी गई

// ऑथेंटिकेशन सेटअप - कस्टम कुकी स्कीम
builder.Services.AddAuthentication("custom")
    .AddCookie("custom", options =>
    {
        options.LoginPath = "/Account/SignIn";        // लॉगिन पेज का रास्ता
        options.AccessDeniedPath = "/Account/AccessDenied"; // एक्सेस डिनाइड पेज
        options.ExpireTimeSpan = TimeSpan.FromDays(30);      // कुकी 30 दिन तक वैलिड
        options.SlidingExpiration = true;                    // एक्सपायरी रिफ्रेश होगी
    });

// सेशन सेटअप - यूज़र सेशन मैनेजमेंट के लिए
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;         // सेशन कुकी सिक्योर
    options.IdleTimeout = TimeSpan.FromHours(2); // 2 घंटे तक सेशन एक्टिव
});

var app = builder.Build();

// मिडलवेयर सेटअप
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage(); // डेवलपमेंट में डिटेल्ड एरर पेज
}
else
{
    app.UseExceptionHandler("/Home/Error"); // प्रोडक्शन में कस्टम एरर पेज
    app.UseHsts();
}

app.UseHttpsRedirection();      // HTTPS पर रीडायरेक्ट
app.UseStaticFiles();           // स्टैटिक फाइल्स (CSS, JS) सर्व करने के लिए
app.UseRouting();               // रूटिंग सेटअप
app.UseAuthentication();        // ऑथेंटिकेशन मिडलवेयर
app.UseAuthorization();         // ऑथराइज़ेशन मिडलवेयर
app.UseSession();               // सेशन मिडलवेयर

// कस्टम रूट्स
app.MapControllerRoute(
    name: "readarticles",
    pattern: "ReadArticles/{id}",
    defaults: new { controller = "ReadArticles", action = "Index" });

app.MapControllerRoute(
    name: "profile",
    pattern: "Profile/{action=Index}/{id?}",
    defaults: new { controller = "Profile" });
app.MapControllerRoute(
    name: "addComment",
    pattern: "ReadArticles/AddComment",
    defaults: new { controller = "ReadArticles", action = "AddComment" });


// डिफ़ॉल्ट रूट (हमेशा आखिरी में)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run(); // एप्लिकेशन शुरू करना