using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using LawFirmManagement.Data;
using LawFirmManagement.Models;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Linq; // Needed for Sum()
using System.Threading.Tasks;

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
        // 2. ACCEPT CASE (New Action)
        // ----------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> AcceptCase(Guid id)
        {
            var userId = _userManager.GetUserId(User);

            // Find the case assigned to this lawyer
            var caseObj = await _db.Cases.FirstOrDefaultAsync(c => c.Id == id && c.LawyerId == userId);

            // Only accept if it is currently 'Pending'
            if (caseObj != null && caseObj.Status == CaseStatus.Pending)
            {
                caseObj.Status = CaseStatus.Active; // Flip to Active
                await _db.SaveChangesAsync();
                TempData["msg"] = "Case accepted successfully. You can now manage it.";
            }
            else
            {
                TempData["error"] = "Could not accept case.";
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

            if (caseDetails == null) return NotFound("Case not found or access denied.");

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
        // 4. UPDATE STATUS (With Hearing Validation)
        // ----------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(Guid caseId, CaseStatus status)
        {
            var caseObj = await _db.Cases.FindAsync(caseId);
            if (caseObj != null)
            {
                // CONSTRAINT CHECK: If trying to Close...
                if (status == CaseStatus.Closed)
                {
                    // Count how many hearings exist for this case
                    int hearingCount = await _db.Hearings.CountAsync(h => h.CaseId == caseId);

                    // Requirement: "Without multiple hearings (at least 2), case will not close"
                    if (hearingCount < 2)
                    {
                        TempData["error"] = $"Cannot close case! You need multiple hearings. Current: {hearingCount}.";
                        return RedirectToAction("ManageCase", new { id = caseId });
                    }

                    // If check passes, set End Date
                    caseObj.EndDate = DateTime.UtcNow;
                }
                else
                {
                    // If reopening, clear End Date
                    caseObj.EndDate = null;
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

            TempData["msg"] = "Hearing added.";
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
                TempData["msg"] = "Document uploaded.";
            }
            return RedirectToAction("ManageCase", new { id = caseId });
        }

        // ----------------------------------------------------
        // 6. VIEW MY EARNINGS (Financial Dashboard)
        // ----------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Payments()
        {
            var userId = _userManager.GetUserId(User);

            // Fetch payments linked to cases where I am the Lawyer
            // We use .Include to navigate: Payment -> Case -> LawyerId
            var payments = await _db.Payments
                .Include(p => p.Case)
                .Include(p => p.Case.Client)
                .Where(p => p.Case.LawyerId == userId) // Filter: Only this lawyer's cases
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            // Calculate total earnings (Sum of LawyerShare column)
            ViewBag.TotalEarnings = payments.Sum(p => p.LawyerShare);

            return View(payments);
        }
    }
}