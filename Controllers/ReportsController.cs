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
using System.Text; // Needed for StringBuilder

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

        // ----------------------------------------------------
        // 1. VIEW REPORT (Dashboard)
        // ----------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Index(string reportType, DateTime? startDate, DateTime? endDate)
        {
            var model = await GetReportViewModel(reportType, startDate, endDate);
            return View(model);
        }

        // ----------------------------------------------------
        // 2. DOWNLOAD REPORT (Generates HTML File)
        // ----------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> Download(string reportType, DateTime? startDate, DateTime? endDate)
        {
            var model = await GetReportViewModel(reportType, startDate, endDate);

            var sb = new StringBuilder();
            sb.Append("<html><head><style>");
            sb.Append("body { font-family: sans-serif; padding: 20px; }");
            sb.Append("h1, h3 { color: #2c3e50; }");
            sb.Append("table { width: 100%; border-collapse: collapse; margin-bottom: 20px; }");
            sb.Append("th { background-color: #f2f2f2; text-align: left; padding: 8px; border: 1px solid #ddd; }");
            sb.Append("td { padding: 8px; border: 1px solid #ddd; }");
            sb.Append(".summary { margin-bottom: 30px; padding: 15px; background: #f9f9f9; border: 1px solid #eee; }");
            sb.Append("</style></head><body>");

            sb.Append($"<h1>{model.ReportType} Report</h1>");
            sb.Append($"<p><strong>Generated On:</strong> {DateTime.Now:dd MMM yyyy}<br/>");
            sb.Append($"<strong>Period:</strong> {model.StartDate:dd MMM yyyy} to {model.EndDate:dd MMM yyyy}</p>");

            // Summary
            sb.Append("<div class='summary'><h3>Executive Summary</h3>");
            sb.Append($"<p>Total Cases: <b>{model.TotalCases}</b> | ");
            sb.Append($"New: <b>{model.NewCases}</b> | ");
            sb.Append($"Closed: <b>{model.ClosedCases}</b></p>");
            sb.Append($"<h3>Financials: {model.TotalRevenue:N0} BDT</h3></div>");

            // Case Table
            sb.Append("<h3>Case Details</h3><table>");
            sb.Append("<thead><tr><th>Title</th><th>Type</th><th>Date</th><th>Status</th></tr></thead><tbody>");
            foreach (var c in model.CaseList)
            {
                sb.Append($"<tr><td>{c.CaseTitle}</td><td>{c.CaseType}</td><td>{c.StartDate:dd/MM/yyyy}</td><td>{c.Status}</td></tr>");
            }
            sb.Append("</tbody></table>");

            // Payment Table
            sb.Append("<h3>Financial Transactions</h3><table>");
            sb.Append("<thead><tr><th>Date</th><th>Method</th><th>Amount</th></tr></thead><tbody>");
            foreach (var p in model.PaymentList)
            {
                // Logic to show correct share in download based on role could be added here, currently showing Total/Share based on viewmodel
                decimal displayAmount = User.IsInRole("Admin") ? p.TotalAmount : (User.IsInRole("Lawyer") ? p.LawyerShare : p.TotalAmount);
                sb.Append($"<tr><td>{p.PaymentDate:dd/MM/yyyy}</td><td>{p.PaymentMethod}</td><td>{displayAmount:N2}</td></tr>");
            }
            sb.Append("</tbody></table>");

            sb.Append("</body></html>");

            byte[] fileBytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(fileBytes, "text/html", $"Report_{model.ReportType}_{DateTime.Now:yyyyMMdd}.html");
        }

        // ----------------------------------------------------
        // HELPER: Shared Logic for Filtering
        // ----------------------------------------------------
        private async Task<ReportViewModel> GetReportViewModel(string reportType, DateTime? startDate, DateTime? endDate)
        {
            var user = await _userManager.GetUserAsync(User);
            var userId = user?.Id;
            var roles = await _userManager.GetRolesAsync(user);
            string userRole = roles.FirstOrDefault() ?? "Client";

            DateTime start = startDate ?? DateTime.UtcNow.AddMonths(-1);
            DateTime end = endDate ?? DateTime.UtcNow;

            if (!string.IsNullOrEmpty(reportType))
            {
                var now = DateTime.UtcNow;
                switch (reportType)
                {
                    case "Daily": start = now.Date; end = now.Date.AddDays(1).AddTicks(-1); break;
                    case "Weekly": start = now.AddDays(-7); end = now; break;
                    case "Monthly": start = now.AddMonths(-1); end = now; break;
                    case "Yearly": start = now.AddYears(-1); end = now; break;
                }
            }

            var casesQuery = _db.Cases.Include(c => c.Client).Include(c => c.Lawyer).Where(c => c.StartDate >= start && c.StartDate <= end);
            var paymentsQuery = _db.Payments.Include(p => p.Case).Where(p => p.PaymentDate >= start && p.PaymentDate <= end);
            var hearingsQuery = _db.Hearings.Include(h => h.Case).Where(h => h.HearingDate >= start && h.HearingDate <= end);

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