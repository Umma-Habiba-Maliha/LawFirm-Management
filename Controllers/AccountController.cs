using LawFirmManagement.Data;
using LawFirmManagement.Hubs;
using LawFirmManagement.Models;
using LawFirmManagement.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LawFirmManagement.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly IHubContext<AdminHub> _hub;

        public AccountController(
            ApplicationDbContext db,
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SignInManager<IdentityUser> signInManager,
            IEmailSender emailSender,
            IHubContext<AdminHub> hub)
        {
            _db = db;
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _hub = hub;
        }

        // ----------------------------------------------------
        // CLIENT SELF REGISTER (Pending)
        // ----------------------------------------------------
        [HttpGet]
        public IActionResult RegisterPending() => View();

        [HttpPost]
        public async Task<IActionResult> RegisterPending(RegisterPendingViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // ... (Existing validation checks remain the same) ...
            if (await _db.PendingUsers.AnyAsync(x => x.Email == model.Email && !x.IsProcessed))
            {
                ModelState.AddModelError("", "This email is already submitted for approval.");
                return View(model);
            }

            if (await _userManager.FindByEmailAsync(model.Email) != null)
            {
                ModelState.AddModelError("", "Account already exists with this email.");
                return View(model);
            }

            // 1. Save the Pending User
            var pending = new PendingUser
            {
                FullName = model.FullName,
                Email = model.Email,
                Phone = model.Phone,
                Address = model.Address,
                AdditionalInfo = model.AdditionalInfo,
                Role = PendingRole.Client
            };

            _db.PendingUsers.Add(pending);
            await _db.SaveChangesAsync();

            // 2. NEW: Save Notification to Database (So it's not just "Live")
            var notification = new NotificationItem
            {
                Title = "New Registration",
                Message = $"{pending.FullName} has requested to join.",
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };
            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync();

            // 3. Send SignalR Alert (Live Popup)
            await _hub.Clients.Group("Admins").SendAsync("NewPendingUser", pending.FullName);

            // 4. Send Email
            string approveUrl = Url.Action("ApprovePending", "Account", new { id = pending.Id }, Request.Scheme);
            // REMEMBER: Use your REAL email here
            await _emailSender.SendEmailAsync("malihahabiba1703@gmail.com", "New Client Request",
                $"User {pending.FullName} wants to join. <a href='{approveUrl}'>Approve Now</a>");

            TempData["msg"] = "Registration submitted. Admin will approve soon.";
            return RedirectToAction("RegisterPending");
        }

        // ----------------------------------------------------
        // ADMIN PENDING LIST (Was Missing!)
        // ----------------------------------------------------
        [HttpGet]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminPendingList()
        {
            // Looks for Views/Account/AdminPendingList.cshtml
            var pendingList = await _db.PendingUsers
                .Where(x => !x.IsProcessed)
                .OrderByDescending(x => x.RequestedAt)
                .ToListAsync();

            return View(pendingList);
        }

        // ----------------------------------------------------
        // ADMIN: APPROVE
        // ----------------------------------------------------
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApprovePending(Guid id)
        {
            var pending = await _db.PendingUsers.FirstOrDefaultAsync(x => x.Id == id);
            if (pending == null) return NotFound();
            if (pending.IsProcessed) return Content("Already processed.");

            var user = new IdentityUser { UserName = pending.Email, Email = pending.Email, PhoneNumber = pending.Phone };
            string tempPassword = "Law" + Guid.NewGuid().ToString("N").Substring(0, 8) + "!";

            var result = await _userManager.CreateAsync(user, tempPassword);
            if (!result.Succeeded) return Content("Failed: " + string.Join(", ", result.Errors.Select(e => e.Description)));

            // Ensure Role Exists
            if (!await _roleManager.RoleExistsAsync("Client")) await _roleManager.CreateAsync(new IdentityRole("Client"));
            await _userManager.AddToRoleAsync(user, "Client");

            var profile = new UserProfile
            {
                UserId = user.Id,
                FullName = pending.FullName,
                Address = pending.Address,
                Phone = pending.Phone,
                Role = "Client"
            };
            _db.UserProfiles.Add(profile);

            pending.IsProcessed = true;
            await _db.SaveChangesAsync();

            await _emailSender.SendEmailAsync(user.Email, "Approved", $"Pass: {tempPassword}");

            return RedirectToAction("AdminPendingList");
        }

        // ----------------------------------------------------
        // ADMIN: REJECT
        // ----------------------------------------------------
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<IActionResult> RejectPending(Guid id)
        {
            var pending = await _db.PendingUsers.FirstOrDefaultAsync(x => x.Id == id);
            if (pending != null)
            {
                pending.IsProcessed = true;
                pending.AdminNote = "Rejected";
                await _db.SaveChangesAsync();
                await _emailSender.SendEmailAsync(pending.Email, "Rejected", "Your request was rejected.");
            }
            return RedirectToAction("AdminPendingList");
        }

        // ----------------------------------------------------
        // LOGIN
        // ----------------------------------------------------
        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, false, false);
            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (await _userManager.IsInRoleAsync(user, "Admin"))
                    return RedirectToAction("Index", "AdminDashboard");

                return RedirectToAction("Index", "Home");
            }
            ModelState.AddModelError("", "Invalid login.");
            return View(model);
        }

        // ----------------------------------------------------
        // LOGOUT (Was Missing!)
        // ----------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }
    }
}