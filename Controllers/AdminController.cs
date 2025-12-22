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
        // 3. DELETE USER (Fixed to handle Constraints)
        // ---------------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return RedirectToAction("Users");

            // A. Remove Related Cases
            var userCases = _db.Cases.Where(c => c.ClientId == id || c.LawyerId == id).ToList();
            if (userCases.Any())
            {
                _db.Cases.RemoveRange(userCases);
                await _db.SaveChangesAsync();
            }

            // B. Remove Profile
            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == id);
            if (profile != null)
            {
                _db.UserProfiles.Remove(profile);
                await _db.SaveChangesAsync();
            }

            // C. Remove Pending Request
            var pending = await _db.PendingUsers.FirstOrDefaultAsync(p => p.Email == user.Email);
            if (pending != null)
            {
                _db.PendingUsers.Remove(pending);
                await _db.SaveChangesAsync();
            }

            // D. Remove User
            await _userManager.DeleteAsync(user);

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

            // Auto-generate password
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

                // Send Email
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
        // 7. FINANCIAL DASHBOARD (THE MISSING PART)
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

            // Calculate Totals
            ViewBag.TotalRevenue = payments.Sum(p => p.TotalAmount);
            ViewBag.TotalAdminShare = payments.Sum(p => p.AdminShare);
            ViewBag.TotalLawyerShare = payments.Sum(p => p.LawyerShare);

            return View(payments);
        }
    }
}