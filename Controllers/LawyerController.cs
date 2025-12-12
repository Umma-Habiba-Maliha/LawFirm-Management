using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using LawFirmManagement.Data;
using LawFirmManagement.Models;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace LawFirmManagement.Controllers
{
    [Authorize(Roles = "Lawyer")]
    public class LawyerController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _environment;

        public LawyerController(
            ApplicationDbContext db,
            UserManager<IdentityUser> userManager,
            IWebHostEnvironment environment)
        {
            _db = db;
            _userManager = userManager;
            _environment = environment;
        }

        // ----------------------------------------------------
        // 1. DASHBOARD
        // ----------------------------------------------------
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var myCases = await _db.Cases
                .Include(c => c.Client)
                .Where(c => c.LawyerId == userId)
                .OrderByDescending(c => c.Status)
                .ToListAsync();

            return View(myCases);
        }

        // ----------------------------------------------------
        // 2. ACCEPT CASE (The "Handshake" Action)
        // ----------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> AcceptCase(Guid id)
        {
            var userId = _userManager.GetUserId(User);
            var caseObj = await _db.Cases.FirstOrDefaultAsync(c => c.Id == id && c.LawyerId == userId);

            if (caseObj != null && caseObj.Status == CaseStatus.Pending)
            {
                caseObj.Status = CaseStatus.Active; // 1. Flip to Active
                await _db.SaveChangesAsync();
                TempData["msg"] = "Case Accepted! You can now manage details.";
            }
            return RedirectToAction("Index");
        }

        // ----------------------------------------------------
        // 3. MANAGE CASE (Enforced Access)
        // ----------------------------------------------------
        public async Task<IActionResult> ManageCase(Guid id)
        {
            var userId = _userManager.GetUserId(User);

            var caseDetails = await _db.Cases
                .Include(c => c.Client)
                .FirstOrDefaultAsync(c => c.Id == id && c.LawyerId == userId);

            if (caseDetails == null) return NotFound();

            // --- STRICT WORKFLOW CHECK ---
            // If the case is still Pending (Not Accepted), the lawyer CANNOT view details.
            if (caseDetails.Status == CaseStatus.Pending)
            {
                TempData["error"] = "You must ACCEPT the case request before you can manage it.";
                return RedirectToAction("Index");
            }

            // Load Data
            ViewBag.Hearings = await _db.Hearings.Where(h => h.CaseId == id).OrderBy(h => h.HearingDate).ToListAsync();
            ViewBag.Documents = await _db.CaseDocuments.Where(d => d.CaseId == id).OrderByDescending(d => d.UploadedAt).ToListAsync();

            return View(caseDetails);
        }

        // ----------------------------------------------------
        // 4. UPDATE STATUS (With "Multiple Hearing" Rule)
        // ----------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(Guid caseId, CaseStatus status)
        {
            var caseObj = await _db.Cases.FindAsync(caseId);
            if (caseObj != null)
            {
                // CONSTRAINT: Cannot Close without Multiple Hearings
                if (status == CaseStatus.Closed)
                {
                    int hearingCount = await _db.Hearings.CountAsync(h => h.CaseId == caseId);

                    // "Multiple" means at least 2
                    if (hearingCount < 2)
                    {
                        TempData["error"] = $"Action Denied: You cannot close a case with only {hearingCount} hearing(s). Multiple hearings are required.";
                        return RedirectToAction("ManageCase", new { id = caseId });
                    }

                    caseObj.EndDate = DateTime.UtcNow; // Auto-set date
                }
                else
                {
                    caseObj.EndDate = null; // Clear date if re-opening
                }

                caseObj.Status = status;
                await _db.SaveChangesAsync();
                TempData["msg"] = "Case Status Updated.";
            }
            return RedirectToAction("ManageCase", new { id = caseId });
        }

        // ----------------------------------------------------
        // 5. HELPER ACTIONS (AddHearing / UploadDocument)
        // ----------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> AddHearing(Guid CaseId, DateTime HearingDate, string CourtName, string Notes)
        {
            _db.Hearings.Add(new Hearing { CaseId = CaseId, HearingDate = HearingDate, CourtName = CourtName, Notes = Notes ?? "", ReminderSent = false });
            await _db.SaveChangesAsync();
            return RedirectToAction("ManageCase", new { id = CaseId });
        }

        [HttpPost]
        public async Task<IActionResult> UploadDocument(Guid caseId, IFormFile file)
        {
            if (file != null && file.Length > 0)
            {
                var fileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                var uploadPath = Path.Combine(_environment.WebRootPath, "documents");
                if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                using (var stream = new FileStream(Path.Combine(uploadPath, fileName), FileMode.Create)) { await file.CopyToAsync(stream); }

                _db.CaseDocuments.Add(new CaseDocument { CaseId = caseId, FileName = file.FileName, FilePath = "/documents/" + fileName, UploadedBy = User.Identity?.Name });
                await _db.SaveChangesAsync();
            }
            return RedirectToAction("ManageCase", new { id = caseId });
        }
    }
}