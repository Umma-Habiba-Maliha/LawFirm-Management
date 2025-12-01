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
        // 1. REGISTER ADMIN (GET)
        // ---------------------------------------------------------
        [HttpGet]
        [AllowAnonymous]
        public IActionResult RegisterAdmin() => View();

        // ---------------------------------------------------------
        // 2. REGISTER ADMIN (POST)
        // ---------------------------------------------------------
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
        // 3. USER LIST
        // ---------------------------------------------------------
        public IActionResult Users()
        {
            var profiles = _db.UserProfiles
                .Include(u => u.User)
                .ToList();
            return View(profiles);
        }

        // ---------------------------------------------------------
        // 4. DELETE USER
        // ---------------------------------------------------------
        // 4. DELETE USER (Fixed to handle Database Constraints)
        // ---------------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> DeleteUser(string id)
        {
            // 1. Find the User
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return RedirectToAction("Users");

            // 2. DELETE RELATED CASES (Fixes the SQL Conflict Error)
            // We must remove any cases where this user is the Client OR the Lawyer
            var userCases = _db.Cases
                .Where(c => c.ClientId == id || c.LawyerId == id)
                .ToList();

            if (userCases.Any())
            {
                _db.Cases.RemoveRange(userCases);
                await _db.SaveChangesAsync(); // Save this change first
            }

            // 3. DELETE USER PROFILE (Clean up the profile table)
            var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == id);
            if (profile != null)
            {
                _db.UserProfiles.Remove(profile);
                await _db.SaveChangesAsync();
            }

            // 4. DELETE PENDING REQUEST (If they are still in the waiting list)
            var pending = await _db.PendingUsers.FirstOrDefaultAsync(p => p.Email == user.Email);
            if (pending != null)
            {
                _db.PendingUsers.Remove(pending);
                await _db.SaveChangesAsync();
            }

            // 5. FINALLY, DELETE THE LOGIN ACCOUNT
            await _userManager.DeleteAsync(user);

            return RedirectToAction("Users");
        }

        // ---------------------------------------------------------
        // 5. CREATE LAWYER (GET)
        // ---------------------------------------------------------
        [HttpGet]
        public IActionResult CreateLawyer() => View();

        // ---------------------------------------------------------
        // 6. CREATE LAWYER (POST) - UPDATED FOR AUTO PASSWORD
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

            // --- 1. AUTO-GENERATE PASSWORD ---
            // Creates a password like "Law8f3a2b1!"
            string autoPassword = "Law" + Guid.NewGuid().ToString("N").Substring(0, 6) + "!";

            // --- 2. CREATE USER WITH AUTO PASSWORD ---
            var result = await _userManager.CreateAsync(user, autoPassword);

            if (result.Succeeded)
            {
                // Ensure Role Exists
                if (!await _roleManager.RoleExistsAsync("Lawyer"))
                    await _roleManager.CreateAsync(new IdentityRole("Lawyer"));

                await _userManager.AddToRoleAsync(user, "Lawyer");

                // Create Profile
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

                // --- 3. SEND EMAIL WITH PASSWORD ---
                string subject = "Welcome to LawFirm as a Lawyer - Account Credentials";
                string body = $@"
                    <div style='font-family: Arial; padding: 20px; border: 1px solid #ddd; background-color: #f9f9f9;'>
                        <h2 style='color: #2c3e50;'>Welcome, {model.FullName}!</h2>
                        <p>You have been registered as a Lawyer.</p>
                        <hr />
                        <p><strong>Your Temporary Credentials:</strong></p>
                        <ul>
                            <li><b>Email:</b> {model.Email}</li>
                            <li><b>Password:</b> <span style='background: #eee; padding: 3px 8px; border-radius: 4px; font-weight: bold;'>{autoPassword}</span></li>
                        </ul>
                        <p style='color: red;'>Please log in and change your password immediately.</p>
                        <br/>
                        <a href='https://localhost:7208/Account/Login' style='background-color: #007bff; color: white; padding: 10px 15px; text-decoration: none; border-radius: 5px;'>Login Now</a>
                    </div>
                ";

                await _emailSender.SendEmailAsync(model.Email, subject, body);

                return RedirectToAction("Users");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }
    }
}