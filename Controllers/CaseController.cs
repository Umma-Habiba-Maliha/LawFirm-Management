using LawFirmManagement.Data;
using LawFirmManagement.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LawFirmManagement.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CaseController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public CaseController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // 1. LIST CASES
        public async Task<IActionResult> Index()
        {
            var cases = await _db.Cases
                .Include(c => c.Client)
                .Include(c => c.Lawyer)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            return View(cases);
        }

        // 2. CREATE (GET)
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new CreateCaseViewModel();
            await LoadDropdowns(model);
            return View(model);
        }

        // 3. CREATE (POST)
        [HttpPost]
        public async Task<IActionResult> Create(CreateCaseViewModel model)
        {
            // 1. Basic Form Validation
            if (!ModelState.IsValid)
            {
                await LoadDropdowns(model);
                return View(model);
            }

            // ---------------------------------------------------------
            // LOGIC 1: SPECIALIZATION MATCH
            // ---------------------------------------------------------
            var lawyerProfile = await _db.UserProfiles
                .FirstOrDefaultAsync(u => u.UserId == model.LawyerId);

            if (lawyerProfile == null)
            {
                ModelState.AddModelError("LawyerId", "Selected lawyer profile not found.");
                await LoadDropdowns(model);
                return View(model);
            }

            // Compare Case Type vs Lawyer Specialization (Ignore case sensitivity)
            if (!string.Equals(lawyerProfile.Specialization, model.CaseType, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("LawyerId",
                    $"Mismatch! This case is '{model.CaseType}', but Lawyer {lawyerProfile.FullName} specializes in '{lawyerProfile.Specialization}'.");

                await LoadDropdowns(model);
                return View(model);
            }

            // ---------------------------------------------------------
            // LOGIC 2: WORKLOAD LIMIT (Max 5 Active)
            // ---------------------------------------------------------
            int activeCases = await _db.Cases
                .CountAsync(c => c.LawyerId == model.LawyerId && c.Status != CaseStatus.Closed);

            if (activeCases >= 5)
            {
                ModelState.AddModelError("LawyerId",
                    $"Overloaded! Lawyer {lawyerProfile.FullName} already has {activeCases} active cases. Max limit is 5.");

                await LoadDropdowns(model);
                return View(model);
            }

            // ---------------------------------------------------------
            // LOGIC 3: FIXED FEE ENFORCEMENT
            // ---------------------------------------------------------
            // Ensure the fee stored matches the official rate card
            decimal fixedFee = GetFixedFee(model.CaseType);

            var newCase = new Case
            {
                CaseTitle = model.CaseTitle,
                CaseType = model.CaseType,
                Description = model.Description,
                ClientId = model.ClientId,
                LawyerId = model.LawyerId,
                Status = CaseStatus.Pending,
                StartDate = DateTime.UtcNow,

                // Save values
                TotalFee = fixedFee,
                PaymentStatus = "Unpaid"
            };

            _db.Cases.Add(newCase);
            await _db.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // Helper: Get Fixed Fees
        private decimal GetFixedFee(string caseType)
        {
            return caseType switch
            {
                "Civil" => 50000m,
                "Criminal" => 80000m,
                "Family" => 30000m,
                "Corporate" => 120000m,
                "Property" => 60000m,
                _ => 0m
            };
        }

        // Helper: Load Dropdowns with Smart Info
        private async Task LoadDropdowns(CreateCaseViewModel model)
        {
            // Clients
            var clientUsers = await _userManager.GetUsersInRoleAsync("Client");
            model.ClientList = clientUsers.Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = u.Email
            });

            // Lawyers - Add Specialization and Workload to the dropdown text
            var lawyers = await _db.UserProfiles
                .Where(p => p.Role == "Lawyer")
                .Include(p => p.User)
                .ToListAsync();

            model.LawyerList = lawyers.Select(p =>
            {
                // Count active cases for this lawyer
                int currentLoad = _db.Cases.Count(c => c.LawyerId == p.UserId && c.Status != CaseStatus.Closed);

                return new SelectListItem
                {
                    Value = p.UserId,
                    // Text format: "John Doe (Criminal) [3/5]"
                    Text = $"{p.FullName} ({p.Specialization}) [{currentLoad}/5]"
                };
            });
        }
    }
}