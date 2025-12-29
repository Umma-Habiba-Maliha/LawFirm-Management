using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using LawFirmManagement.Data;
using LawFirmManagement.Models;
using LawFirmManagement.Services; // Import Service
using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.AspNetCore.Http; // Needed for FormCollection

namespace LawFirmManagement.Controllers
{
    public class ClientController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SSLCommerzService _sslService;

        public ClientController(
            ApplicationDbContext db,
            UserManager<IdentityUser> userManager,
            SSLCommerzService sslService)
        {
            _db = db;
            _userManager = userManager;
            _sslService = sslService;
        }

        // 1. DASHBOARD
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var myCases = await _db.Cases
                .Include(c => c.Lawyer)
                .Where(c => c.ClientId == userId)
                .OrderByDescending(c => c.Status)
                .ToListAsync();

            return View(myCases);
        }

        // 2. CASE DETAILS
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> Details(Guid id)
        {
            var userId = _userManager.GetUserId(User);
            var caseDetails = await _db.Cases
                .Include(c => c.Lawyer)
                .FirstOrDefaultAsync(c => c.Id == id && c.ClientId == userId);

            if (caseDetails == null) return NotFound("Access Denied");

            ViewBag.Hearings = await _db.Hearings.Where(h => h.CaseId == id).OrderByDescending(h => h.HearingDate).ToListAsync();
            ViewBag.Documents = await _db.CaseDocuments.Where(d => d.CaseId == id).OrderByDescending(d => d.UploadedAt).ToListAsync();

            return View(caseDetails);
        }

        // 3. SHOW PAYMENT PAGE
        [Authorize(Roles = "Client")]
        [HttpGet]
        public async Task<IActionResult> Pay(Guid id, string type = "")
        {
            var caseObj = await _db.Cases.FindAsync(id);
            if (caseObj == null || caseObj.PaymentStatus == "FullyPaid")
                return RedirectToAction("Index");

            decimal payableAmount = 0;
            string paymentStage = "";

            if (caseObj.PaymentStatus == "Unpaid")
            {
                if (type == "Full") { payableAmount = caseObj.TotalFee; paymentStage = "Full"; }
                else { payableAmount = caseObj.TotalFee * 0.20m; paymentStage = "Advance"; }
            }
            else if (caseObj.PaymentStatus == "AdvancePaid")
            {
                payableAmount = caseObj.TotalFee * 0.80m;
                paymentStage = "Final";
            }

            ViewBag.PayableAmount = payableAmount;
            ViewBag.PaymentStage = paymentStage;

            return View(caseObj);
        }

        // ----------------------------------------------------
        // 4. PROCESS PAYMENT (Redirects to Gateway)
        // ----------------------------------------------------
        [Authorize(Roles = "Client")]
        [HttpPost]
        public async Task<IActionResult> ProcessPayment(Guid id, string paymentStage)
        {
            var caseObj = await _db.Cases.FindAsync(id);
            if (caseObj == null) return NotFound();

            // Calculate Amount
            decimal amount = 0;
            if (paymentStage == "Full") amount = caseObj.TotalFee;
            else if (paymentStage == "Advance") amount = caseObj.TotalFee * 0.20m;
            else if (paymentStage == "Final") amount = caseObj.TotalFee * 0.80m;

            // Prepare Data for SSLCommerz
            string txnId = "TXN-" + Guid.NewGuid().ToString().Substring(0, 10);
            var user = await _userManager.GetUserAsync(User);
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            // Initiate Payment
            string gatewayUrl = await _sslService.InitiatePayment(
                amount.ToString(),
                txnId,
                User.Identity.Name ?? "Client",
                user.Email,
                user.PhoneNumber,
                baseUrl,
                id.ToString(),  // value_a: Pass CaseId
                paymentStage    // value_b: Pass Stage (Full/Advance/Final)
            );

            if (!string.IsNullOrEmpty(gatewayUrl))
            {
                // Store context temporarily
                TempData["Pending_TxnId"] = txnId;
                TempData["Pending_CaseId"] = id.ToString();
                TempData["Pending_Stage"] = paymentStage;
                TempData["Pending_Amount"] = amount.ToString();

                return Redirect(gatewayUrl); // Go to SSLCommerz
            }

            TempData["error"] = "Gateway Error. Please try again.";
            return RedirectToAction("Index");
        }

        // ----------------------------------------------------
        // 5. CALLBACK: SUCCESS (From SSLCommerz)
        // ----------------------------------------------------
        [HttpPost]
        [AllowAnonymous] // Gateway needs to access this without login cookie sometimes
        public async Task<IActionResult> PaymentSuccess(IFormCollection form)
        {
            string tran_id = form["tran_id"];
            string val_id = form["val_id"];

            // 1. Retrieve Context from SSLCommerz Pass-through parameters
            string caseIdStr = form["value_a"];
            string stage = form["value_b"];
            string amountStr = form["amount"];

            // Fallback to TempData if params are empty (Standard Flow)
            if (string.IsNullOrEmpty(caseIdStr) && TempData["Pending_CaseId"] != null)
            {
                caseIdStr = TempData["Pending_CaseId"].ToString();
                stage = TempData["Pending_Stage"].ToString();
                amountStr = TempData["Pending_Amount"].ToString();
            }

            if (string.IsNullOrEmpty(caseIdStr)) return RedirectToAction("Index");

            Guid caseId = Guid.Parse(caseIdStr);
            decimal paidAmount = decimal.Parse(amountStr);

            var caseObj = await _db.Cases.FindAsync(caseId);
            if (caseObj == null) return RedirectToAction("Index");

            // --- 2. CALCULATE SPLITS (DYNAMIC PERCENTAGE) ---
            decimal adminShare = 0;
            decimal lawyerShare = 0;
            string paymentTypeRecord = "";

            // Get Admin % from the Case (e.g., 2.5, 10.0) and convert to decimal (0.025, 0.10)
            decimal adminRate = (decimal)(caseObj.AdminSharePercentage / 100.0);

            if (stage == "Full")
            {
                // Full Payment: Apply custom % to the whole amount
                adminShare = paidAmount * adminRate;
                lawyerShare = paidAmount - adminShare;

                caseObj.PaymentStatus = "FullyPaid";
                paymentTypeRecord = "Full Payment";
            }
            else if (stage == "Advance")
            {
                // Advance (20%): Lawyer usually keeps advance to start work
                lawyerShare = paidAmount;
                adminShare = 0;

                caseObj.PaymentStatus = "AdvancePaid";
                paymentTypeRecord = "Advance (20%)";
            }
            else if (stage == "Final")
            {
                // Final (80%): Admin takes their % of the TOTAL FEE now
                decimal totalFee = caseObj.TotalFee;
                adminShare = totalFee * adminRate;

                // Lawyer gets the remainder of THIS transaction
                lawyerShare = paidAmount - adminShare;

                caseObj.PaymentStatus = "FullyPaid";
                paymentTypeRecord = "Final Settlement";
            }

            // 3. Save Real Payment Record
            var payment = new Payment
            {
                CaseId = caseId,
                TotalAmount = paidAmount,
                AdminShare = adminShare,
                LawyerShare = lawyerShare,
                PaymentMethod = "SSLCommerz (" + form["card_type"] + ")",
                PaymentType = paymentTypeRecord,
                PaymentDate = DateTime.UtcNow
            };

            _db.Payments.Add(payment);
            await _db.SaveChangesAsync();

            TempData["msg"] = $"Payment Successful! TxnID: {tran_id}";
            return RedirectToAction("Index");
        }

        // 6. FAIL CALLBACK
        [HttpPost]
        [AllowAnonymous]
        public IActionResult PaymentFail()
        {
            TempData["error"] = "Payment Failed.";
            return RedirectToAction("Index");
        }

        // 7. CANCEL CALLBACK
        [HttpPost]
        [AllowAnonymous]
        public IActionResult PaymentCancel()
        {
            TempData["error"] = "Payment Cancelled.";
            return RedirectToAction("Index");
        }

        // 8. PAYMENT HISTORY
        [Authorize(Roles = "Client")]
        [HttpGet]
        public async Task<IActionResult> PayHistory()
        {
            var userId = _userManager.GetUserId(User);

            var payments = await _db.Payments
                .Include(p => p.Case)
                .Include(p => p.Case.Lawyer)
                .Where(p => p.Case.ClientId == userId)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            ViewBag.TotalPaid = payments.Sum(p => p.TotalAmount);
            return View(payments);
        }
    }
}