using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;
using LawFirmManagement.Data;
using LawFirmManagement.Models;
using LawFirmManagement.Services;
using System;

namespace LawFirmManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _db;
        private readonly IEmailSender _emailSender;

        public AdminController(
            UserManager<IdentityUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext db,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
            _emailSender = emailSender;
        }

        // ---------------------------------------------------------
        // 1. REGISTER ADMIN
        // ---------------------------------------------------------
        [HttpGet]
        [AllowAnonymous]
        public IActionResult RegisterAdmin() => View();

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterAdmin(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError("", "Email and Password are required.");
                return View();
            }

            var user = new IdentityUser { UserName = email, Email = email };
            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                if (!await _roleManager.RoleExistsAsync("Admin"))
                    await _roleManager.CreateAsync(new IdentityRole("Admin"));

                await _userManager.AddToRoleAsync(user, "Admin");
                return RedirectToAction("Users");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View();
        }

        // ---------------------------------------------------------
        // 2. USER LIST
        // ---------------------------------------------------------
        public IActionResult Users()
        {
            var profiles = _db.UserProfiles
                .Include(u => u.User)
                .ToList();
            return View(profiles);
        }

        // ---------------------------------------------------------
        // 3. DELETE USER (UPDATED: SOFT DELETE / DEACTIVATE)
        // ---------------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return RedirectToAction("Users");

            // --- SOFT DELETE IMPLEMENTATION ---
            // Instead of deleting from DB (which causes foreign key errors),
            // we simply lock the account forever.

            // 1. Enable Lockout
            await _userManager.SetLockoutEnabledAsync(user, true);

            // 2. Lock them out until the end of time (Year 9999)
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

            // 3. Update Security Stamp to invalidate current cookies (force logout immediately)
            await _userManager.UpdateSecurityStampAsync(user);

            // Note: The user remains in the database, preserving all history (Cases, Payments).
            // They just cannot log in anymore.

            return RedirectToAction("Users");
        }

        // ---------------------------------------------------------
        // 4. CREATE LAWYER (GET)
        // ---------------------------------------------------------
        [HttpGet]
        public IActionResult CreateLawyer() => View();

        // ---------------------------------------------------------
        // 5. CREATE LAWYER (POST)
        // ---------------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> CreateLawyer(CreateLawyerViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = new IdentityUser
            {
                UserName = model.Email,
                Email = model.Email,
                PhoneNumber = model.Phone
            };

            string autoPassword = "Law" + Guid.NewGuid().ToString("N").Substring(0, 6) + "!";

            var result = await _userManager.CreateAsync(user, autoPassword);

            if (result.Succeeded)
            {
                if (!await _roleManager.RoleExistsAsync("Lawyer"))
                    await _roleManager.CreateAsync(new IdentityRole("Lawyer"));

                await _userManager.AddToRoleAsync(user, "Lawyer");

                var profile = new UserProfile
                {
                    UserId = user.Id,
                    FullName = model.FullName,
                    Role = "Lawyer",
                    Phone = model.Phone,
                    Specialization = model.Specialization,
                    DateOfJoining = model.DateOfJoining
                };

                _db.UserProfiles.Add(profile);
                await _db.SaveChangesAsync();

                string subject = "Welcome to LawFirm as a Lawyer";
                string body = $"<h1>Welcome {model.FullName}</h1><p>Login: {model.Email}</p><p>Password: {autoPassword}</p>";
                await _emailSender.SendEmailAsync(model.Email, subject, body);

                return RedirectToAction("Users");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        // ---------------------------------------------------------
        // 6. LAWYER CASE HISTORY
        // ---------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> LawyerCases(string lawyerId)
        {
            if (string.IsNullOrEmpty(lawyerId)) return NotFound();

            var lawyer = await _userManager.FindByIdAsync(lawyerId);
            if (lawyer == null) return NotFound();

            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == lawyerId);
            ViewBag.LawyerName = profile?.FullName ?? lawyer.Email;

            var history = await _db.Cases
                .Include(c => c.Client)
                .Where(c => c.LawyerId == lawyerId)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            return View(history);
        }

        // ---------------------------------------------------------
        // 7. FINANCIAL DASHBOARD
        // ---------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Payments()
        {
            var payments = await _db.Payments
                .Include(p => p.Case)
                .Include(p => p.Case.Client)
                .Include(p => p.Case.Lawyer)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            ViewBag.TotalRevenue = payments.Sum(p => p.TotalAmount);
            ViewBag.TotalAdminShare = payments.Sum(p => p.AdminShare);
            ViewBag.TotalLawyerShare = payments.Sum(p => p.LawyerShare);

            return View(payments);
        }

        // ---------------------------------------------------------
        // 8. EDIT USER (GET)
        // ---------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> EditUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == id);
            if (profile == null) return NotFound();

            var model = new EditUserViewModel
            {
                UserId = user.Id,
                Email = user.Email,
                Phone = user.PhoneNumber,
                FullName = profile.FullName,
                Role = profile.Role,
                Specialization = profile.Specialization,
                DateOfJoining = profile.DateOfJoining
            };

            return View(model);
        }

        // ---------------------------------------------------------
        // 9. EDIT USER (POST)
        // ---------------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByIdAsync(model.UserId);
            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == model.UserId);

            if (user == null || profile == null) return NotFound();

            user.Email = model.Email;
            user.UserName = model.Email;
            user.PhoneNumber = model.Phone;

            profile.FullName = model.FullName;
            profile.Phone = model.Phone;

            if (model.Role == "Lawyer")
            {
                profile.Specialization = model.Specialization;
                profile.DateOfJoining = model.DateOfJoining;
            }

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                _db.UserProfiles.Update(profile);
                await _db.SaveChangesAsync();
                return RedirectToAction("Users");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }
    }
}