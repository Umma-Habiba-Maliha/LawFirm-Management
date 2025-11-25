using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LawFirmManagement.Data;
using LawFirmManagement.Services;
using LawFirmManagement.Hubs;

var builder = WebApplication.CreateBuilder(args);

// --------------------------------------
// DATABASE
// --------------------------------------
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// --------------------------------------
// IDENTITY
// --------------------------------------
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// --------------------------------------
// MVC + SignalR
// --------------------------------------
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// --------------------------------------
// EMAIL SENDER (SMTP) 
// --------------------------------------
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

// --------------------------------------
// NOTIFICATION SERVICE
// --------------------------------------
builder.Services.AddScoped<NotificationService>();

var app = builder.Build();

// --------------------------------------
// ROLE SEEDING
// --------------------------------------
using (var scope = app.Services.CreateScope())
{
    var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
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
// MIDDLEWARE
// --------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// --------------------------------------
// ROUTING
// --------------------------------------
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<AdminHub>("/hubs/admin");

app.Run();
