using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using LawFirmManagement.Data;
using LawFirmManagement.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text;

namespace LawFirmManagement.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;

        public ReportsController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // 1. REPORT DASHBOARD
        [HttpGet]
        public async Task<IActionResult> Index(string reportType, DateTime? startDate, DateTime? endDate)
        {
            var model = await GetReportData(reportType, startDate, endDate);
            return View(model);
        }

        // 2. DOWNLOAD REPORT (Generates HTML File)
        [HttpGet]
        public async Task<IActionResult> Download(string reportType, DateTime? startDate, DateTime? endDate)
        {
            var model = await GetReportData(reportType, startDate, endDate);

            // Build HTML String
            var sb = new StringBuilder();
            sb.Append("<html><head><style>");
            sb.Append("body { font-family: 'Segoe UI', sans-serif; padding: 40px; color: #333; }");
            sb.Append("h1 { color: #2c3e50; border-bottom: 2px solid #2c3e50; padding-bottom: 10px; }");
            sb.Append(".summary-box { background: #f8f9fa; padding: 20px; border: 1px solid #ddd; margin-bottom: 30px; }");
            sb.Append("table { width: 100%; border-collapse: collapse; margin-bottom: 30px; font-size: 14px; }");
            sb.Append("th { background-color: #2c3e50; color: white; text-align: left; padding: 10px; }");
            sb.Append("td { padding: 10px; border-bottom: 1px solid #eee; }");
            sb.Append("</style></head><body>");

            sb.Append($"<h1>Law Firm Report: {model.ReportType}</h1>");
            sb.Append($"<p>Generated: {DateTime.Now:dd MMM yyyy HH:mm} | Period: {model.StartDate:dd MMM yyyy} to {model.EndDate:dd MMM yyyy}</p>");

            sb.Append("<div class='summary-box'>");
            sb.Append($"<strong>Total Cases:</strong> {model.TotalCases} &nbsp;|&nbsp; ");
            sb.Append($"<strong>Closed:</strong> {model.ClosedCases} &nbsp;|&nbsp; ");
            sb.Append($"<strong>Revenue:</strong> {model.TotalRevenue:N2} BDT");
            sb.Append("</div>");

            if (model.CaseList.Any())
            {
                sb.Append("<h3>Case History</h3><table><thead><tr><th>Title</th><th>Type</th><th>Start Date</th><th>Status</th></tr></thead><tbody>");
                foreach (var c in model.CaseList)
                {
                    sb.Append($"<tr><td>{c.CaseTitle}</td><td>{c.CaseType}</td><td>{c.StartDate:dd-MMM-yyyy}</td><td>{c.Status}</td></tr>");
                }
                sb.Append("</tbody></table>");
            }

            if (model.PaymentList.Any())
            {
                sb.Append("<h3>Financial Transactions</h3><table><thead><tr><th>Date</th><th>Case</th><th>Method</th><th>Amount</th></tr></thead><tbody>");
                foreach (var p in model.PaymentList)
                {
                    decimal amount = User.IsInRole("Admin") ? p.TotalAmount : (User.IsInRole("Lawyer") ? p.LawyerShare : p.TotalAmount);
                    sb.Append($"<tr><td>{p.PaymentDate:dd-MMM-yyyy}</td><td>{p.Case?.CaseTitle}</td><td>{p.PaymentMethod}</td><td>{amount:N2}</td></tr>");
                }
                sb.Append("</tbody></table>");
            }

            sb.Append("</body></html>");

            // Return as file
            byte[] fileBytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(fileBytes, "text/html", $"Report_{model.ReportType}_{DateTime.Now:yyyyMMdd}.html");
        }

        // HELPER: Fetch Data Logic (Shared by View and Download)
        private async Task<ReportViewModel> GetReportData(string reportType, DateTime? startDate, DateTime? endDate)
        {
            var user = await _userManager.GetUserAsync(User);
            var userId = user?.Id;
            var roles = await _userManager.GetRolesAsync(user);
            string userRole = roles.FirstOrDefault() ?? "Client";

            DateTime start = startDate ?? DateTime.UtcNow.AddMonths(-1);
            DateTime end = endDate ?? DateTime.UtcNow;

            if (!string.IsNullOrEmpty(reportType) && reportType != "Custom")
            {
                var now = DateTime.UtcNow;
                if (reportType == "Daily") { start = now.Date; end = now.Date.AddDays(1).AddTicks(-1); }
                else if (reportType == "Weekly") { start = now.AddDays(-7); end = now; }
                else if (reportType == "Monthly") { start = now.AddMonths(-1); end = now; }
                else if (reportType == "Yearly") { start = now.AddYears(-1); end = now; }
            }

            // Queries
            var casesQuery = _db.Cases.Include(c => c.Client).Include(c => c.Lawyer).Where(c => c.StartDate >= start && c.StartDate <= end);
            var paymentsQuery = _db.Payments.Include(p => p.Case).Where(p => p.PaymentDate >= start && p.PaymentDate <= end);
            var hearingsQuery = _db.Hearings.Include(h => h.Case).Where(h => h.HearingDate >= start && h.HearingDate <= end);

            // Role Filter
            if (userRole == "Lawyer")
            {
                casesQuery = casesQuery.Where(c => c.LawyerId == userId);
                paymentsQuery = paymentsQuery.Where(p => p.Case.LawyerId == userId);
                hearingsQuery = hearingsQuery.Where(h => h.Case.LawyerId == userId);
            }
            else if (userRole == "Client")
            {
                casesQuery = casesQuery.Where(c => c.ClientId == userId);
                paymentsQuery = paymentsQuery.Where(p => p.Case.ClientId == userId);
                hearingsQuery = hearingsQuery.Where(h => h.Case.ClientId == userId);
            }

            var caseList = await casesQuery.OrderByDescending(c => c.StartDate).ToListAsync();
            var paymentList = await paymentsQuery.OrderByDescending(p => p.PaymentDate).ToListAsync();
            var hearingList = await hearingsQuery.OrderByDescending(h => h.HearingDate).ToListAsync();

            decimal totalRevenue = 0;
            if (userRole == "Admin") totalRevenue = paymentList.Sum(p => p.TotalAmount);
            else if (userRole == "Lawyer") totalRevenue = paymentList.Sum(p => p.LawyerShare);
            else totalRevenue = paymentList.Sum(p => p.TotalAmount);

            return new ReportViewModel
            {
                StartDate = start,
                EndDate = end,
                ReportType = reportType ?? "Custom",
                CaseList = caseList,
                PaymentList = paymentList,
                HearingList = hearingList,
                TotalCases = caseList.Count,
                NewCases = caseList.Count(c => c.Status == CaseStatus.Pending),
                ClosedCases = caseList.Count(c => c.Status == CaseStatus.Closed),
                TotalRevenue = totalRevenue
            };
        }
    }
}