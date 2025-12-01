using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using LawFirmManagement.Data;
using LawFirmManagement.Models;

namespace LawFirmManagement.Controllers
{
    [Authorize(Roles = "Lawyer")] // Only Lawyers can see this
    public class LawyerController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public LawyerController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // ----------------------------------------------------
        // 1. DASHBOARD - LIST ASSIGNED CASES
        // ----------------------------------------------------
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            // Fetch cases where LawyerId matches the logged-in user
            var myCases = await _db.Cases
                .Include(c => c.Client)
                .Where(c => c.LawyerId == userId)
                .OrderByDescending(c => c.Status) // Show Active/Pending first
                .ToListAsync();

            return View(myCases);
        }

        // ----------------------------------------------------
        // 2. MANAGE CASE (View Details + Hearings)
        // ----------------------------------------------------
        public async Task<IActionResult> ManageCase(Guid id)
        {
            var userId = _userManager.GetUserId(User);

            // Security: Ensure the lawyer owns this case
            var caseDetails = await _db.Cases
                .Include(c => c.Client)
                .FirstOrDefaultAsync(c => c.Id == id && c.LawyerId == userId);

            if (caseDetails == null) return NotFound("Case not found or access denied.");

            // Load existing hearings for this case
            ViewBag.Hearings = await _db.Hearings
                .Where(h => h.CaseId == id)
                .OrderBy(h => h.HearingDate)
                .ToListAsync();

            return View(caseDetails);
        }

        // ----------------------------------------------------
        // 3. SCHEDULE HEARING (POST)
        // ----------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> AddHearing(Guid CaseId, DateTime HearingDate, string CourtName, string Notes)
        {
            var hearing = new Hearing
            {
                CaseId = CaseId,
                HearingDate = HearingDate,
                CourtName = CourtName,
                Notes = Notes ?? "",
                ReminderSent = false
            };

            _db.Hearings.Add(hearing);
            await _db.SaveChangesAsync();

            TempData["msg"] = "Hearing scheduled successfully.";
            return RedirectToAction("ManageCase", new { id = CaseId });
        }

        // ----------------------------------------------------
        // 4. UPDATE CASE STATUS (Active/Closed)
        // ----------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(Guid caseId, CaseStatus status)
        {
            var caseObj = await _db.Cases.FindAsync(caseId);
            if (caseObj != null)
            {
                caseObj.Status = status;
                await _db.SaveChangesAsync();
            }
            return RedirectToAction("ManageCase", new { id = caseId });
        }
    }
}