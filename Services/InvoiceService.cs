using System;
using System.Text;
using LawFirmManagement.Models;

namespace LawFirmManagement.Services
{
    public class InvoiceService
    {
        public string GenerateInvoiceHtml(Payment payment, string userRole)
        {
            var sb = new StringBuilder();

            string companyName = "LawFirm Management";
            string companyAddress = "123 Legal Avenue, Dhaka, Bangladesh";

            // UPDATED: Added time to the date format
            string date = DateTime.Now.ToString("dd MMM yyyy h:mm tt");

            string invoiceNo = $"INV-{payment.Id:D6}";

            // DYNAMIC CALCULATION: Get the actual percentage from the Case
            // If for some reason Case is null, default to 10%
            double adminPercent = payment.Case?.AdminSharePercentage ?? 10.0;
            double lawyerPercent = 100.0 - adminPercent;

            sb.Append($@"
                <html>
                <head>
                    <style>
                        body {{ font-family: 'Helvetica', sans-serif; padding: 40px; color: #333; }}
                        .header {{ text-align: center; margin-bottom: 40px; border-bottom: 2px solid #2c3e50; padding-bottom: 20px; }}
                        .company-name {{ font-size: 24px; font-weight: bold; color: #2c3e50; }}
                        .invoice-title {{ font-size: 32px; font-weight: bold; color: #2c3e50; margin-top: 20px; }}
                        .details-table {{ width: 100%; border-collapse: collapse; margin-top: 30px; }}
                        .details-table th {{ background-color: #f8f9fa; padding: 12px; text-align: left; border-bottom: 1px solid #ddd; }}
                        .details-table td {{ padding: 12px; border-bottom: 1px solid #eee; }}
                        .total-row {{ font-weight: bold; font-size: 18px; background-color: #f8f9fa; }}
                        .footer {{ margin-top: 50px; text-align: center; font-size: 12px; color: #777; }}
                        .status-paid {{ color: green; font-weight: bold; border: 2px solid green; padding: 5px 10px; border-radius: 5px; display: inline-block; }}
                    </style>
                </head>
                <body>
                    <div class='header'>
                        <div class='company-name'>{companyName}</div>
                        <div>{companyAddress}</div>
                        <div class='invoice-title'>INVOICE</div>
                        <div>#{invoiceNo}</div>
                        <div>Date: {date}</div>
                    </div>

                    <div style='display: flex; justify-content: space-between;'>
                        <div>
                            <strong>Bill To:</strong><br/>
                            {payment.Case?.Client?.Email ?? "Client"}<br/>
                            Case: {payment.Case?.CaseTitle}
                        </div>
                        <div style='text-align: right;'>
                            <span class='status-paid'>PAID</span>
                        </div>
                    </div>

                    <table class='details-table'>
                        <thead>
                            <tr>
                                <th>Description</th>
                                <th style='text-align: right;'>Amount</th>
                            </tr>
                        </thead>
                        <tbody>
                            <tr>
                                <td>Legal Services - {payment.PaymentType} Payment</td>
                                <td style='text-align: right;'>{payment.TotalAmount:N2} BDT</td>
                            </tr>
            ");

            // Show breakdown only if Admin or Lawyer requests it
            if (userRole == "Admin" || userRole == "Lawyer")
            {
                sb.Append($@"
                            <tr>
                                <!-- Display the exact dynamic percentage -->
                                <td style='color: #777; font-size: 0.9em;'>Admin Share ({adminPercent}%)</td>
                                <td style='text-align: right; color: #777; font-size: 0.9em;'>{payment.AdminShare:N2} BDT</td>
                            </tr>
                            <tr>
                                <!-- Display the calculated lawyer percentage -->
                                <td style='color: #777; font-size: 0.9em;'>Lawyer Share ({lawyerPercent}%)</td>
                                <td style='text-align: right; color: #777; font-size: 0.9em;'>{payment.LawyerShare:N2} BDT</td>
                            </tr>
                ");
            }

            sb.Append($@"
                            <tr class='total-row'>
                                <td>Total Paid</td>
                                <td style='text-align: right;'>{payment.TotalAmount:N2} BDT</td>
                            </tr>
                        </tbody>
                    </table>

                    <div style='margin-top: 30px;'>
                        <strong>Payment Method:</strong> {payment.PaymentMethod}<br/>
                        <strong>Transaction ID:</strong> #TXN-{payment.Id:D5}
                    </div>

                    <div class='footer'>
                        Thank you for your business.<br/>
                        This is a computer-generated invoice and requires no signature.
                    </div>
                </body>
                </html>
            ");

            return sb.ToString();
        }
    }
}