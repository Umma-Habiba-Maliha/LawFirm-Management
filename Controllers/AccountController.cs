using Microsoft.AspNetCore.Mvc;
using LawFirmManagement.Models;
using LawFirmManagement.Data;
using Microsoft.AspNetCore.Identity;
using LawFirmManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using LawFirmManagement.Hubs;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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

        // -----------------------------------------
        // LOGIN PAGE
        // -----------------------------------------
        [HttpGet]
        public IActionResult Login() => View(new LoginViewModel());

        [HttpPost]
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var user = await _userManager.FindByEmailAsync(vm.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "Invalid email or password.");
                return View(vm);
            }

            var result = await _signInManager.PasswordSignInAsync(user, vm.Password, false, false);

            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Invalid email or password.");
                return View(vm);
            }

            // Check role and redirect properly
            if (await _userManager.IsInRoleAsync(user, "Admin"))
                return RedirectToAction("Index", "AdminDashboard");

            if (await _userManager.IsInRoleAsync(user, "Lawyer"))
                return RedirectToAction("Index", "LawyerDashboard");

            if (await _userManager.IsInRoleAsync(user, "Client"))
                return RedirectToAction("Index", "ClientDashboard");

            return RedirectToAction("Index", "Home");
        }


        // LOGOUT
        // LOGOUT
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }


        // -----------------------------------------
        // ADMIN REGISTRATION (only once)
        // -----------------------------------------
        [HttpGet]
        public IActionResult RegisterAdmin() => View();

        [HttpPost]
        public async Task<IActionResult> RegisterAdmin(string email, string password)
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            if (admins.Any()) return BadRequest("Admin already exists.");

            var user = new IdentityUser { UserName = email, Email = email };
            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded) return BadRequest(result.Errors);

            if (!await _roleManager.RoleExistsAsync("Admin"))
                await _roleManager.CreateAsync(new IdentityRole("Admin"));
            await _userManager.AddToRoleAsync(user, "Admin");

            return RedirectToAction(nameof(AdminPendingList));
        }

        // -----------------------------------------
        // PENDING REGISTRATION
        // -----------------------------------------
        [HttpGet]
        public IActionResult RegisterPending() => View(new RegisterPendingViewModel());

        [HttpPost]
        public async Task<IActionResult> RegisterPending(RegisterPendingViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var existingUser = await _userManager.FindByEmailAsync(vm.Email);
            var existingPending = await _db.PendingUsers.FirstOrDefaultAsync(
                p => p.Email == vm.Email && !p.IsProcessed);

            if (existingUser != null)
            {
                ModelState.AddModelError("", "An account with this email already exists.");
                return View(vm);
            }
            if (existingPending != null)
            {
                ModelState.AddModelError("", "A pending request with this email already exists.");
                return View(vm);
            }

            var pending = new PendingUser
            {
                FullName = vm.FullName,
                Email = vm.Email,
                Phone = vm.Phone,
                Address = vm.Address,
                Role = vm.Role,
                AdditionalInfo = vm.AdditionalInfo
            };
            _db.PendingUsers.Add(pending);

            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var title = $"New {pending.Role} registration";
            var message = $"{pending.FullName} ({pending.Email}) requested registration.";

            foreach (var admin in admins)
            {
                await _emailSender.SendEmailAsync(admin.Email, title, message);
                _db.Notifications.Add(new NotificationItem
                {
                    Title = title,
                    Message = message,
                    ForUserId = admin.Id
                });
            }

            await _db.SaveChangesAsync();

            await _hub.Clients.All.SendAsync("ReceiveNotification",
                new { Title = title, Message = message, PendingId = pending.Id });

            ViewBag.Message = "Registration submitted and sent for admin approval.";
            return View(new RegisterPendingViewModel());
        }

        // -----------------------------------------
        // ADMIN PENDING LIST
        // -----------------------------------------
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminPendingList()
        {
            var list = await _db.PendingUsers
                        .Where(p => !p.IsProcessed)
                        .OrderByDescending(p => p.RequestedAt)
                        .ToListAsync();
            return View(list);
        }

        // APPROVE
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> ApprovePending(Guid id)
        {
            var pending = await _db.PendingUsers.FirstOrDefaultAsync(p => p.Id == id);
            if (pending == null) return NotFound();

            // 1) Create Identity user with temporary password
            var user = new IdentityUser { UserName = pending.Email, Email = pending.Email };
            var tempPassword = "Temp@" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var createResult = await _userManager.CreateAsync(user, tempPassword);

            if (!createResult.Succeeded)
            {
                // log errors (in production use ILogger)
                ModelState.AddModelError("", "Failed to create user: " + string.Join("; ", createResult.Errors.Select(e => e.Description)));
                return RedirectToAction(nameof(AdminPendingList));
            }

            // 2) Ensure role exists and assign
            var roleName = pending.Role == PendingRole.Client ? "Client" : "Lawyer";
            if (!await _roleManager.RoleExistsAsync(roleName))
                await _roleManager.CreateAsync(new IdentityRole(roleName));
            await _userManager.AddToRoleAsync(user, roleName);

            // 3) Save profile details in UserProfile table
            var profile = new UserProfile
            {
                UserId = user.Id,
                FullName = pending.FullName,
                Phone = pending.Phone,
                Address = pending.Address,
                AdditionalInfo = pending.AdditionalInfo,
                Role = roleName
            };
            _db.UserProfiles.Add(profile);

            // 4) Mark pending as processed
            pending.IsProcessed = true;
            pending.AdminNote = $"Approved by {User?.Identity?.Name} on {DateTime.UtcNow}";
            _db.PendingUsers.Update(pending);

            // 5) Save DB changes (profile + pending)
            await _db.SaveChangesAsync();

            // 6) Send email to approved user with temp password (recommend password reset in prod)
            var emailBody = $@"
        <p>Hi {pending.FullName},</p>
        <p>Your registration has been approved by the admin.</p>
        <p><b>Login:</b> {pending.Email}</p>
        <p><b>Temporary password:</b> {tempPassword}</p>
        <p>Please login and change your password immediately.</p>
    ";
            await _emailSender.SendEmailAsync(pending.Email, "Registration Approved - LawFirmManagement", emailBody);

            // 7) Create notification for admins and push via SignalR
            var notifTitle = "Registration approved";
            var notifMessage = $"{pending.FullName} ({pending.Email}) has been approved and account created.";
            _db.Notifications.Add(new NotificationItem { Title = notifTitle, Message = notifMessage, ForUserId = null });
            await _db.SaveChangesAsync();

            await _hub.Clients.All.SendAsync("ReceiveNotification", new { Title = notifTitle, Message = notifMessage, UserId = user.Id });

            return RedirectToAction(nameof(AdminPendingList));
        }


        // REJECT
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> RejectPending(Guid id, string reason)
        {
            var pending = await _db.PendingUsers.FirstOrDefaultAsync(p => p.Id == id);
            if (pending == null) return NotFound();

            pending.IsProcessed = true;
            pending.AdminNote = $"Rejected: {reason}";
            _db.PendingUsers.Update(pending);
            await _db.SaveChangesAsync();
            
            await _emailSender.SendEmailAsync(pending.Email, "Rejected",
                $"Your request was rejected. Reason: {reason}");

            return RedirectToAction(nameof(AdminPendingList));
        }
    }
}
