using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using LawFirmManagement.Data;
using LawFirmManagement.Models;
using LawFirmManagement.Services;
using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting; // Needed for IWebHostEnvironment
using System.IO;                    // Needed for Path

namespace LawFirmManagement.Controllers
{
    public class ClientController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SSLCommerzService _sslService;
        private readonly IWebHostEnvironment _environment; // Defined here
        private readonly NotificationService _notificationService;

        public ClientController(
            ApplicationDbContext db,
            UserManager<IdentityUser> userManager,
            SSLCommerzService sslService,
            IWebHostEnvironment environment,
            NotificationService notificationService)
        {
            _db = db;
            _userManager = userManager;
            _sslService = sslService;
            _environment = environment; // <--- FIX: THIS WAS MISSING!
            _notificationService = notificationService;
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

        // 3. SHOW PAYMENT PAGE (Updated to 50% Logic)
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
                if (type == "Full")
                {
                    payableAmount = caseObj.TotalFee;
                    paymentStage = "Full";
                }
                else
                {
                    payableAmount = caseObj.TotalFee * 0.50m; // 50% Advance
                    paymentStage = "Advance";
                }
            }
            else if (caseObj.PaymentStatus == "AdvancePaid")
            {
                payableAmount = caseObj.TotalFee * 0.50m; // Remaining 50%
                paymentStage = "Final";
            }

            ViewBag.PayableAmount = payableAmount;
            ViewBag.PaymentStage = paymentStage;

            return View(caseObj);
        }

        // 4. PROCESS PAYMENT (Updated to 50% Logic)
        [Authorize(Roles = "Client")]
        [HttpPost]
        public async Task<IActionResult> ProcessPayment(Guid id, string paymentStage)
        {
            var caseObj = await _db.Cases.FindAsync(id);
            if (caseObj == null) return NotFound();

            decimal amount = 0;
            if (paymentStage == "Full") amount = caseObj.TotalFee;
            else if (paymentStage == "Advance") amount = caseObj.TotalFee * 0.50m; // 50%
            else if (paymentStage == "Final") amount = caseObj.TotalFee * 0.50m;   // 50%

            string txnId = "TXN-" + Guid.NewGuid().ToString().Substring(0, 10);
            var user = await _userManager.GetUserAsync(User);
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            string gatewayUrl = await _sslService.InitiatePayment(
                amount.ToString(),
                txnId,
                User.Identity.Name ?? "Client",
                user.Email,
                user.PhoneNumber,
                baseUrl,
                id.ToString(),
                paymentStage
            );

            if (!string.IsNullOrEmpty(gatewayUrl))
            {
                TempData["Pending_TxnId"] = txnId;
                TempData["Pending_CaseId"] = id.ToString();
                TempData["Pending_Stage"] = paymentStage;
                TempData["Pending_Amount"] = amount.ToString();
                return Redirect(gatewayUrl);
            }

            TempData["error"] = "Gateway Error. Please try again.";
            return RedirectToAction("Index");
        }

        // 5. PAYMENT SUCCESS
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> PaymentSuccess(IFormCollection form)
        {
            string tran_id = form["tran_id"];
            string caseIdStr = form["value_a"];
            string stage = form["value_b"];
            string amountStr = form["amount"];

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

            decimal adminShare = 0;
            decimal lawyerShare = 0;
            string paymentTypeRecord = "";

            decimal adminRate = (decimal)(caseObj.AdminSharePercentage / 100.0);

            if (stage == "Full")
            {
                adminShare = paidAmount * adminRate;
                lawyerShare = paidAmount - adminShare;
                caseObj.PaymentStatus = "FullyPaid";
                paymentTypeRecord = "Full Payment";
            }
            else if (stage == "Advance")
            {
                lawyerShare = paidAmount;
                adminShare = 0;
                caseObj.PaymentStatus = "AdvancePaid";
                paymentTypeRecord = "Advance (50%)";
            }
            else if (stage == "Final")
            {
                decimal totalFee = caseObj.TotalFee;
                adminShare = totalFee * adminRate;
                lawyerShare = paidAmount - adminShare;
                caseObj.PaymentStatus = "FullyPaid";
                paymentTypeRecord = "Final Settlement";
            }

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

            // Notifications
            await _notificationService.CreateForAdminsAsync("Payment Received", $"Client paid ৳{paidAmount} for Case: {caseObj.CaseTitle}.");
            if (!string.IsNullOrEmpty(caseObj.LawyerId))
            {
                await _notificationService.NotifyUserAsync(caseObj.LawyerId, "Payment Update", $"Payment of ৳{paidAmount} received for Case: {caseObj.CaseTitle}.");
            }

            TempData["msg"] = $"Payment Successful! TxnID: {tran_id}";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult PaymentFail()
        {
            TempData["error"] = "Payment Failed.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult PaymentCancel()
        {
            TempData["error"] = "Payment Cancelled.";
            return RedirectToAction("Index");
        }

        // 6. DOWNLOAD DOCUMENT (Fixed)
        [Authorize(Roles = "Client")]
        public IActionResult DownloadDocument(int id)
        {
            var doc = _db.CaseDocuments.Find(id);
            if (doc == null) return NotFound("Document record not found.");

            // Now _environment is properly initialized!
            var webRoot = _environment.WebRootPath;
            var relativePath = doc.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var filePath = Path.Combine(webRoot, relativePath);

            if (!System.IO.File.Exists(filePath)) return NotFound("File not found on server.");
            return PhysicalFile(filePath, "application/octet-stream", doc.FileName);
        }

        // 7. PAYMENT HISTORY
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