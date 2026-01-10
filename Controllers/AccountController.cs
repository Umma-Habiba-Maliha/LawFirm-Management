using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using LawFirmManagement.Models;
using LawFirmManagement.Data; // Needed for PendingUsers
using System.Threading.Tasks;
using LawFirmManagement.Hubs;
using Microsoft.AspNetCore.SignalR;
using LawFirmManagement.Services;
using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.ComponentModel.DataAnnotations; // For ViewModels

namespace LawFirmManagement.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ApplicationDbContext _db;
        private readonly IEmailSender _emailSender;
        private readonly IHubContext<AdminHub> _hub;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AccountController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ApplicationDbContext db,
            IEmailSender emailSender,
            IHubContext<AdminHub> hub,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _db = db;
            _emailSender = emailSender;
            _hub = hub;
            _roleManager = roleManager;
        }

        // ---------------------------------------------------------
        // 1. LOGIN (GET)
        // ---------------------------------------------------------
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // ---------------------------------------------------------
        // 2. LOGIN (POST) - With Smart Redirection
        // ---------------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, false, false); // Removed RememberMe for simplicity if not in model

                if (result.Succeeded)
                {
                    // Get the user to check their Role
                    var user = await _userManager.FindByEmailAsync(model.Email);
                    var roles = await _userManager.GetRolesAsync(user);

                    // REDIRECT LOGIC: Where do they go?
                    if (roles.Contains("Admin"))
                    {
                        // Admin goes to the Home Dashboard (Admin View) or AdminDashboard Controller
                        return RedirectToAction("Index", "AdminDashboard");
                    }
                    else if (roles.Contains("Lawyer"))
                    {
                        // Lawyer goes strictly to their Lawyer Dashboard
                        return RedirectToAction("Index", "Lawyer");
                    }
                    else if (roles.Contains("Client"))
                    {
                        // Client goes to Home (or Client Dashboard later)
                        return RedirectToAction("Index", "Client"); // Changed to Client Dashboard based on recent work
                    }

                    return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError("", "Invalid login attempt.");
            }
            return View(model);
        }

        // ---------------------------------------------------------
        // 3. REGISTER PENDING (GET) - The Public Form
        // ---------------------------------------------------------
        [HttpGet]
        public IActionResult RegisterPending()
        {
            return View();
        }

        // ---------------------------------------------------------
        // 4. REGISTER PENDING (POST) - Saves to "Waiting Room"
        // ---------------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> RegisterPending(RegisterPendingViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check if email already exists in the real system
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("", "This email is already registered. Please login.");
                    return View(model);
                }

                // Check if already in pending list
                bool alreadyPending = await _db.PendingUsers.AnyAsync(p => p.Email == model.Email);
                if (alreadyPending)
                {
                    ModelState.AddModelError("", "You have already submitted a request. Please wait for approval.");
                    return View(model);
                }

                // Create the Pending Request
                var pendingUser = new PendingUser
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    Phone = model.Phone,
                    Address = model.Address,
                    AdditionalInfo = model.AdditionalInfo,
                    Role = PendingRole.Client, // Force Role to Client for public form, or map if enum
                    RequestedAt = DateTime.UtcNow
                };

                _db.PendingUsers.Add(pendingUser);
                await _db.SaveChangesAsync();

                // 2. NEW: Save Notification to Database (So it's not just "Live")
                var notification = new NotificationItem
                {
                    Title = "New Registration",
                    Message = $"{pendingUser.FullName} has requested to join.",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };
                _db.Notifications.Add(notification);
                await _db.SaveChangesAsync();

                // 3. Send SignalR Alert (Live Popup)
                await _hub.Clients.Group("Admins").SendAsync("NewPendingUser", pendingUser.FullName);

                // Show success message
                ViewBag.Message = "Your registration request has been submitted successfully! An administrator will review it shortly.";
                ModelState.Clear(); // Clear the form
                return View();
            }

            return View(model);
        }

        // ----------------------------------------------------
        // ADMIN PENDING LIST (Helper actions if needed here or keep in AdminController)
        // ----------------------------------------------------
        [HttpGet]
        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<IActionResult> AdminPendingList()
        {
            var pendingList = await _db.PendingUsers
                .Where(x => !x.IsProcessed)
                .OrderByDescending(x => x.RequestedAt)
                .ToListAsync();

            return View(pendingList);
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApprovePending(Guid id)
        {
            var pending = await _db.PendingUsers.FirstOrDefaultAsync(x => x.Id == id);

            if (pending == null) return NotFound();

            if (pending.IsProcessed)
                return Content("Already processed.");

            // CREATE USER
            var user = new IdentityUser
            {
                UserName = pending.Email,
                Email = pending.Email,
                PhoneNumber = pending.Phone
            };

            string tempPassword = "Law" + Guid.NewGuid().ToString("N").Substring(0, 8) + "!";

            var createUser = await _userManager.CreateAsync(user, tempPassword);

            if (!createUser.Succeeded)
                return Content("User creation failed: " + string.Join(", ", createUser.Errors.Select(e => e.Description)));

            // ASSIGN ROLE
            if (!await _roleManager.RoleExistsAsync("Client"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Client"));
            }
            await _userManager.AddToRoleAsync(user, "Client");

            // CREATE PROFILE
            var profile = new UserProfile
            {
                UserId = user.Id,
                FullName = pending.FullName,
                Address = pending.Address,
                Phone = pending.Phone,
                Role = pending.Role.ToString()
            };

            _db.UserProfiles.Add(profile);

            // MARK AS PROCESSED
            pending.IsProcessed = true;
            await _db.SaveChangesAsync();

            // SEND EMAIL TO USER
            await _emailSender.SendEmailAsync(user.Email, "Registration Approved",
                $"Your account is approved. Password: <b>{tempPassword}</b>");

            return RedirectToAction("AdminPendingList");
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
        public async Task<IActionResult> RejectPending(Guid id)
        {
            var pending = await _db.PendingUsers.FirstOrDefaultAsync(x => x.Id == id);

            if (pending == null) return NotFound();

            pending.IsProcessed = true;
            pending.AdminNote = "Rejected";

            await _db.SaveChangesAsync();

            await _emailSender.SendEmailAsync(pending.Email, "Registration Update",
                "Your registration request was rejected.");

            return RedirectToAction("AdminPendingList");
        }

        // ---------------------------------------------------------
        // 5. LOGOUT
        // ---------------------------------------------------------
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        // ---------------------------------------------------------
        // 6. ACCESS DENIED
        // ---------------------------------------------------------
        public IActionResult AccessDenied()
        {
            return View();
        }

        // ----------------------------------------------------
        // 7. FORGOT PASSWORD (GET)
        // ----------------------------------------------------
        [HttpGet]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public IActionResult ForgotPassword() => View();

        // ----------------------------------------------------
        // 8. FORGOT PASSWORD (POST)
        // ----------------------------------------------------
        [HttpPost]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null)
                {
                    // Generate Token
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                    // Create Link
                    var callbackUrl = Url.Action("ResetPassword", "Account",
                        new { email = model.Email, token = token }, Request.Scheme);

                    // Send Email
                    await _emailSender.SendEmailAsync(model.Email, "Reset Password",
                        $"Please reset your password by <a href='{callbackUrl}'>clicking here</a>.");
                }

                // Show confirmation page regardless
                return View("ForgotPasswordConfirmation");
            }
            return View(model);
        }

        // ----------------------------------------------------
        // 9. RESET PASSWORD (GET)
        // ----------------------------------------------------
        [HttpGet]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public IActionResult ResetPassword(string token, string email)
        {
            if (token == null || email == null) ModelState.AddModelError("", "Invalid password reset token");
            return View(new ResetPasswordViewModel { Token = token, Email = email });
        }

        // ----------------------------------------------------
        // 10. RESET PASSWORD (POST)
        // ----------------------------------------------------
        [HttpPost]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null) return RedirectToAction("Login");

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
            if (result.Succeeded)
            {
                TempData["msg"] = "Password has been reset. Please login.";
                return RedirectToAction("Login");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return View(model);
        }
    }

    // --- Helper ViewModels for Password Reset ---
    public class ForgotPasswordViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";
    }

    public class ResetPasswordViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        public string Token { get; set; } = "";

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [DataType(DataType.Password), Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = "";
    }
}