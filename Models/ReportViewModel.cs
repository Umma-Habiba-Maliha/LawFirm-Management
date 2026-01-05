using System;
using System.Collections.Generic;

namespace LawFirmManagement.Models
{
    public class ReportViewModel
    {
        // --- Filter Inputs ---
        public DateTime StartDate { get; set; } = DateTime.UtcNow.AddMonths(-1); // Default to last month
        public DateTime EndDate { get; set; } = DateTime.UtcNow;
        public string ReportType { get; set; } = "Monthly"; // Daily, Weekly, Monthly, Yearly, Custom

        // --- Summary Stats ---
        public int TotalCases { get; set; }
        public int NewCases { get; set; }
        public int ClosedCases { get; set; }
        public decimal TotalRevenue { get; set; }

        // --- Data Lists for Tables ---
        public List<Case> CaseList { get; set; } = new List<Case>();
        public List<Payment> PaymentList { get; set; } = new List<Payment>();
        public List<Hearing> HearingList { get; set; } = new List<Hearing>();
    }
}