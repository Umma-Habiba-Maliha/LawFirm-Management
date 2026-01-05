using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LawFirmManagement.Data;
using LawFirmManagement.Services;
using LawFirmManagement.Hubs;

var builder = WebApplication.CreateBuilder(args);

// --------------------------------------
// 1. DATABASE
// --------------------------------------
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --------------------------------------
// 2. IDENTITY (Authentication)
// --------------------------------------
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    // Simplified password settings for development
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false; // Easier for testing
    options.Password.RequireUppercase = false;

    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure cookie settings (redirect paths)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// --------------------------------------
// 3. MVC + SignalR (Real-time)
// --------------------------------------
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// --------------------------------------
// 4. CUSTOM SERVICES
// --------------------------------------
// HTTP Client (Required for SSLCommerz)
builder.Services.AddHttpClient();

// Email Sender (For registration emails)
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

// Notification Service (For alerts)
builder.Services.AddScoped<NotificationService>();

// SSLCommerz Service (For Payments)
builder.Services.AddScoped<SSLCommerzService>();
// Invoice Service (For generating invoices)
builder.Services.AddScoped<LawFirmManagement.Services.InvoiceService>();

var app = builder.Build();

// --------------------------------------
// 5. ROLE SEEDING (Run on Startup)
// --------------------------------------
using (var scope = app.Services.CreateScope())
{
    var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    // Ensure these 3 core roles always exist
    string[] roles = { "Admin", "Lawyer", "Client" };

    foreach (var role in roles)
    {
        if (!await roleMgr.RoleExistsAsync(role))
        {
            await roleMgr.CreateAsync(new IdentityRole(role));
        }
    }
}

// --------------------------------------
// 6. MIDDLEWARE PIPELINE
// --------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Critical for serving wwwroot files (CSS, JS, Uploaded Docs)

app.UseRouting();

// Authentication & Authorization must be in this order
app.UseAuthentication();
app.UseAuthorization();

// --------------------------------------
// 7. ROUTING ENDPOINTS
// --------------------------------------
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Map SignalR Hub
app.MapHub<AdminHub>("/hubs/admin");

app.Run();