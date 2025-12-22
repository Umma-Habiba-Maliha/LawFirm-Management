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

        // ----------------------------------------------------
        // 3. SHOW PAYMENT PAGE (Smart Logic)
        // ----------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Pay(Guid id)
        {
            var caseObj = await _db.Cases.FindAsync(id);
            if (caseObj == null || caseObj.PaymentStatus == "FullyPaid")
                return RedirectToAction("Index");

            // Logic: Determine what needs to be paid
            decimal payableAmount = 0;
            string paymentStage = "";

            if (caseObj.PaymentStatus == "Unpaid")
            {
                // Stage 1: Pay 20% Advance
                payableAmount = caseObj.TotalFee * 0.20m;
                paymentStage = "Advance";
            }
            else if (caseObj.PaymentStatus == "AdvancePaid")
            {
                // Stage 2: Pay remaining 80%
                payableAmount = caseObj.TotalFee * 0.80m;
                paymentStage = "Final";
            }

            ViewBag.PayableAmount = payableAmount;
            ViewBag.PaymentStage = paymentStage;

            return View(caseObj);
        }

        // ----------------------------------------------------
        // 4. PROCESS PAYMENT (10/90 Split Logic)
        // ----------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> ProcessPayment(Guid id, string paymentMethod)
        {
            var caseObj = await _db.Cases.FindAsync(id);
            if (caseObj == null) return NotFound();

            decimal amountPaid = 0;
            decimal adminShare = 0;
            decimal lawyerShare = 0;
            string paymentType = "";

            // --- SCENARIO 1: ADVANCE PAYMENT (20%) ---
            if (caseObj.PaymentStatus == "Unpaid")
            {
                amountPaid = caseObj.TotalFee * 0.20m;

                // Logic: Admin gets nothing yet. Lawyer gets full advance.
                // (Admin waits for final settlement)
                adminShare = 0;
                lawyerShare = amountPaid;

                paymentType = "Advance";
                caseObj.PaymentStatus = "AdvancePaid";
            }
            // --- SCENARIO 2: FINAL PAYMENT (80%) ---
            else if (caseObj.PaymentStatus == "AdvancePaid")
            {
                amountPaid = caseObj.TotalFee * 0.80m;

                // Logic: Admin takes 10% of the TOTAL FEE now.
                // Lawyer takes the rest of this payment.
                decimal totalFee = caseObj.TotalFee;
                adminShare = totalFee * 0.10m; // 10% of Total
                lawyerShare = amountPaid - adminShare; // Remaining balance

                paymentType = "Final";
                caseObj.PaymentStatus = "FullyPaid";
            }

            // Create Payment Record
            var payment = new Payment
            {
                CaseId = id,
                TotalAmount = amountPaid,
                AdminShare = adminShare,
                LawyerShare = lawyerShare,
                PaymentMethod = paymentMethod,
                PaymentType = paymentType,
                PaymentDate = DateTime.UtcNow
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();

            TempData["msg"] = $"Payment of ৳{amountPaid} ({paymentType}) successful!";
            return RedirectToAction("Index");
        }
        // ... inside ClientController class ...

        // ----------------------------------------------------
        // 5. VIEW PAYMENT HISTORY
        // ----------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Payments()
        {
            var userId = _userManager.GetUserId(User);

            // Fetch payments linked to cases where I am the Client
            var payments = await _db.Payments
                .Include(p => p.Case)
                .Include(p => p.Case.Lawyer) // Show which lawyer was paid
                .Where(p => p.Case.ClientId == userId) // <--- FILTER BY CLIENT ID
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            // Calculate total paid
            ViewBag.TotalPaid = payments.Sum(p => p.TotalAmount);

            return View(payments);
        }
    }
}