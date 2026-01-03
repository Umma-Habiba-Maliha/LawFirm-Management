using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using LawFirmManagement.Data;
using LawFirmManagement.Models;
using LawFirmManagement.Services; // 1. Added Namespace
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace LawFirmManagement.Controllers
{
    [Authorize(Roles = "Lawyer")]
    public class LawyerController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _environment;
        private readonly NotificationService _notificationService; // 2. Add Field

        public LawyerController(
            ApplicationDbContext db,
            UserManager<IdentityUser> userManager,
            IWebHostEnvironment environment,
            NotificationService notificationService) // 3. Inject Service
        {
            _db = db;
            _userManager = userManager;
            _environment = environment;
            _notificationService = notificationService;
        }

        // 1. DASHBOARD
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

        // 2. ACCEPT CASE
        [HttpPost]
        public async Task<IActionResult> AcceptCase(Guid id)
        {
            var userId = _userManager.GetUserId(User);
            var caseObj = await _db.Cases.FirstOrDefaultAsync(c => c.Id == id && c.LawyerId == userId);

            if (caseObj != null && caseObj.Status == CaseStatus.Pending)
            {
                caseObj.Status = CaseStatus.Active;
                await _db.SaveChangesAsync();

                // --- NOTIFY CLIENT ---
                await _notificationService.NotifyUserAsync(
                    caseObj.ClientId,
                    "Case Accepted",
                    $"Your case <strong>{caseObj.CaseTitle}</strong> is now Active. Lawyer: {User.Identity.Name}"
                );

                TempData["msg"] = "Case accepted successfully.";
            }
            else
            {
                TempData["error"] = "Could not accept case.";
            }

            return RedirectToAction("Index");
        }

        // 3. MANAGE CASE
        public async Task<IActionResult> ManageCase(Guid id)
        {
            var userId = _userManager.GetUserId(User);
            var caseDetails = await _db.Cases.Include(c => c.Client).FirstOrDefaultAsync(c => c.Id == id && c.LawyerId == userId);

            if (caseDetails == null) return NotFound();
            if (caseDetails.Status == CaseStatus.Pending)
            {
                TempData["error"] = "You must ACCEPT the case first.";
                return RedirectToAction("Index");
            }

            ViewBag.Hearings = await _db.Hearings.Where(h => h.CaseId == id).OrderBy(h => h.HearingDate).ToListAsync();
            ViewBag.Documents = await _db.CaseDocuments.Where(d => d.CaseId == id).OrderByDescending(d => d.UploadedAt).ToListAsync();

            return View(caseDetails);
        }

        // 4. UPDATE STATUS
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(Guid caseId, CaseStatus status)
        {
            var caseObj = await _db.Cases.FindAsync(caseId);
            if (caseObj != null)
            {
                if (status == CaseStatus.Closed)
                {
                    int hearingCount = await _db.Hearings.CountAsync(h => h.CaseId == caseId);
                    if (hearingCount < 1)
                    {
                        TempData["error"] = $"Cannot close! Needs at least 1 hearings. Current: {hearingCount}.";
                        return RedirectToAction("ManageCase", new { id = caseId });
                    }
                    caseObj.EndDate = DateTime.UtcNow;
                }
                else
                {
                    caseObj.EndDate = null;
                }

                caseObj.Status = status;
                await _db.SaveChangesAsync();

                // --- NOTIFY CLIENT ---
                await _notificationService.NotifyUserAsync(
                    caseObj.ClientId,
                    "Status Update",
                    $"Case <strong>{caseObj.CaseTitle}</strong> status changed to: <strong>{status}</strong>."
                );

                TempData["msg"] = "Status Updated.";
            }
            return RedirectToAction("ManageCase", new { id = caseId });
        }

        // 5. ADD HEARING
        [HttpPost]
        public async Task<IActionResult> AddHearing(Guid CaseId, DateTime HearingDate, string CourtName, string Notes)
        {
            var caseObj = await _db.Cases.FindAsync(CaseId);
            if (caseObj != null && caseObj.Status == CaseStatus.Closed)
            {
                TempData["error"] = "Cannot schedule hearing. Case is CLOSED.";
                return RedirectToAction("ManageCase", new { id = CaseId });
            }

            _db.Hearings.Add(new Hearing { CaseId = CaseId, HearingDate = HearingDate, CourtName = CourtName, Notes = Notes ?? "", ReminderSent = false });
            await _db.SaveChangesAsync();

            // --- NOTIFY CLIENT ---
            if (caseObj != null)
            {
                await _notificationService.NotifyUserAsync(
                    caseObj.ClientId,
                    "Hearing Scheduled",
                    $"New hearing for <strong>{caseObj.CaseTitle}</strong> on {HearingDate.ToShortDateString()} at {CourtName}."
                );
            }

            TempData["msg"] = "Hearing added.";
            return RedirectToAction("ManageCase", new { id = CaseId });
        }

        // 6. UPLOAD DOCUMENT
        [HttpPost]
        public async Task<IActionResult> UploadDocument(Guid caseId, IFormFile file)
        {
            var caseObj = await _db.Cases.FindAsync(caseId);
            if (caseObj != null && caseObj.Status == CaseStatus.Closed)
            {
                TempData["error"] = "Cannot upload documents. Case is CLOSED.";
                return RedirectToAction("ManageCase", new { id = caseId });
            }

            if (file != null && file.Length > 0)
            {
                var fileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                var uploadPath = Path.Combine(_environment.WebRootPath, "documents");
                if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                using (var stream = new FileStream(Path.Combine(uploadPath, fileName), FileMode.Create)) { await file.CopyToAsync(stream); }

                _db.CaseDocuments.Add(new CaseDocument { CaseId = caseId, FileName = file.FileName, FilePath = "/documents/" + fileName, UploadedBy = User.Identity?.Name });
                await _db.SaveChangesAsync();

                // --- NOTIFY CLIENT ---
                if (caseObj != null)
                {
                    await _notificationService.NotifyUserAsync(
                        caseObj.ClientId,
                        "Document Uploaded",
                        $"A new document <strong>{file.FileName}</strong> has been uploaded by your lawyer."
                    );
                }

                TempData["msg"] = "Document uploaded.";
            }
            return RedirectToAction("ManageCase", new { id = caseId });
        }

        // 7. PAYMENTS
        [HttpGet]
        public async Task<IActionResult> Payments()
        {
            var userId = _userManager.GetUserId(User);
            var payments = await _db.Payments.Include(p => p.Case).Include(p => p.Case.Client).Where(p => p.Case.LawyerId == userId).OrderByDescending(p => p.PaymentDate).ToListAsync();
            ViewBag.TotalEarnings = payments.Sum(p => p.LawyerShare);
            return View(payments);
        }
    }
}