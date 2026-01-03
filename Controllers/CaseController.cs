using LawFirmManagement.Data;
using LawFirmManagement.Models;
using LawFirmManagement.Services; // 1. Added Namespace
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
        private readonly NotificationService _notificationService; // 2. Add Field

        public CaseController(
            ApplicationDbContext db,
            UserManager<IdentityUser> userManager,
            NotificationService notificationService) // 3. Inject Service
        {
            _db = db;
            _userManager = userManager;
            _notificationService = notificationService;
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
            if (!ModelState.IsValid)
            {
                await LoadDropdowns(model);
                return View(model);
            }

            // Logic 1: Specialization Match
            var lawyerProfile = await _db.UserProfiles.FirstOrDefaultAsync(u => u.UserId == model.LawyerId);
            if (lawyerProfile == null)
            {
                ModelState.AddModelError("LawyerId", "Selected lawyer profile not found.");
                await LoadDropdowns(model);
                return View(model);
            }

            if (!string.Equals(lawyerProfile.Specialization, model.CaseType, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("LawyerId", $"Mismatch! This case is '{model.CaseType}', but Lawyer specializes in '{lawyerProfile.Specialization}'.");
                await LoadDropdowns(model);
                return View(model);
            }

            // Logic 2: Workload Limit
            int activeCases = await _db.Cases.CountAsync(c => c.LawyerId == model.LawyerId && c.Status != CaseStatus.Closed);
            if (activeCases >= 5)
            {
                ModelState.AddModelError("LawyerId", $"Overloaded! Lawyer already has {activeCases} active cases. Max limit is 5.");
                await LoadDropdowns(model);
                return View(model);
            }

            // Logic 3: Fixed Fee & Admin Share
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
                TotalFee = fixedFee,
                PaymentStatus = "Unpaid",
                AdminSharePercentage = model.AdminSharePercentage
            };

            _db.Cases.Add(newCase);
            await _db.SaveChangesAsync();

            // --- 4. SEND NOTIFICATION TO LAWYER (NEW) ---
            await _notificationService.NotifyUserAsync(
                model.LawyerId,
                "New Case Assigned",
                $"You have been assigned to case: <strong>{model.CaseTitle}</strong> ({model.CaseType}). Please accept it."
            );

            return RedirectToAction("Index");
        }

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

        private async Task LoadDropdowns(CreateCaseViewModel model)
        {
            var clientUsers = await _userManager.GetUsersInRoleAsync("Client");
            model.ClientList = clientUsers.Select(u => new SelectListItem { Value = u.Id, Text = u.Email });

            var lawyers = await _db.UserProfiles
                .Where(p => p.Role == "Lawyer")
                .Include(p => p.User)
                .ToListAsync();

            model.LawyerList = lawyers.Select(p =>
            {
                int currentLoad = _db.Cases.Count(c => c.LawyerId == p.UserId && c.Status != CaseStatus.Closed);
                string statusText = currentLoad >= 5 ? "[FULL]" : $"[{currentLoad}/5]";
                return new SelectListItem
                {
                    Value = p.UserId,
                    Text = $"{p.FullName} ({p.Specialization}) - {statusText}"
                };
            });
        }
    }
}