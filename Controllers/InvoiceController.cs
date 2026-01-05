using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using LawFirmManagement.Data;
using LawFirmManagement.Services;
using System.Text;
using System.Threading.Tasks;

namespace LawFirmManagement.Controllers
{
    [Authorize] // Any logged in user
    public class InvoiceController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly InvoiceService _invoiceService;

        public InvoiceController(ApplicationDbContext db, UserManager<IdentityUser> userManager, InvoiceService invoiceService)
        {
            _db = db;
            _userManager = userManager;
            _invoiceService = invoiceService;
        }

        [HttpGet]
        public async Task<IActionResult> Download(int id)
        {
            var userId = _userManager.GetUserId(User);
            var payment = await _db.Payments
                .Include(p => p.Case)
                .Include(p => p.Case.Client)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment == null) return NotFound();

            // SECURITY CHECK: Who is allowed to see this invoice?
            bool isAdmin = User.IsInRole("Admin");
            bool isLawyer = User.IsInRole("Lawyer") && payment.Case.LawyerId == userId;
            bool isClient = User.IsInRole("Client") && payment.Case.ClientId == userId;

            if (!isAdmin && !isLawyer && !isClient)
            {
                return Forbid(); // Access Denied
            }

            // Generate Content based on role (Admin sees all, others see specific view)
            string userRole = isAdmin ? "Admin" : (isLawyer ? "Lawyer" : "Client");
            string htmlContent = _invoiceService.GenerateInvoiceHtml(payment, userRole);

            // Convert to bytes and download
            byte[] fileBytes = Encoding.UTF8.GetBytes(htmlContent);
            string fileName = $"Invoice_{payment.Id}.html"; // Browsers can open/print HTML easily

            return File(fileBytes, "text/html", fileName);
        }
    }
}