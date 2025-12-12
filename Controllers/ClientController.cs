using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using LawFirmManagement.Data;
using LawFirmManagement.Models;

namespace LawFirmManagement.Controllers
{
    [Authorize(Roles = "Client")] // STRICTLY for Clients
    public class ClientController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public ClientController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // ----------------------------------------------------
        // 1. CLIENT DASHBOARD (My Cases)
        // ----------------------------------------------------
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            // Fetch cases where ClientId matches the logged-in user
            var myCases = await _db.Cases
                .Include(c => c.Lawyer) // Include Lawyer info so client sees who is assigned
                .Where(c => c.ClientId == userId)
                .OrderByDescending(c => c.Status)
                .ToListAsync();

            return View(myCases);
        }

        // ----------------------------------------------------
        // 2. CASE DETAILS (Read-Only View)
        // ----------------------------------------------------
        public async Task<IActionResult> Details(Guid id)
        {
            var userId = _userManager.GetUserId(User);

            // Security Check: Ensure the Client owns this case
            var caseDetails = await _db.Cases
                .Include(c => c.Lawyer)
                .FirstOrDefaultAsync(c => c.Id == id && c.ClientId == userId);

            if (caseDetails == null) return NotFound("Case not found or access denied.");

            // Load Hearings (Read-Only)
            ViewBag.Hearings = await _db.Hearings
                .Where(h => h.CaseId == id)
                .OrderByDescending(h => h.HearingDate)
                .ToListAsync();

            // Load Documents (Read-Only / Downloadable)
            ViewBag.Documents = await _db.CaseDocuments
                .Where(d => d.CaseId == id)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            return View(caseDetails);
        }
    }
}