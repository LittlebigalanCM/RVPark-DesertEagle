using ApplicationCore.Dtos;
using ApplicationCore.Models;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace Infrastructure.Utilities
{
    public static class PdfExporter
    {
        public static MemoryStream ExportReservationsToPdf(List<Reservation> reservations, DateTime start, DateTime end)
        {
            var document = new PdfDocument();
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);
            var font = new XFont("Verdana", 10);

            double y = 40;
            gfx.DrawString($"Reservation Report: {start:MM/dd/yyyy} - {end:MM/dd/yyyy}",
                new XFont("Verdana", 14, XFontStyle.Bold),
                XBrushes.Black,
                new XRect(0, y, page.Width, 20),
                XStringFormats.TopCenter);
            y += 30;

            foreach (var r in reservations) 
            {
                TimeSpan duration = r.EndDate - r.StartDate;
                var line = $"{r.UserAccount.FirstName} {r.UserAccount.LastName}, {r.UserAccount.Email}, {r.UserAccount.PhoneNumber}, " +
                           $"{r.Site.Name}, {r.ReservationStatus}, {r.StartDate:MM/dd/yyyy} - {r.EndDate:MM/dd/yyyy}, {duration} days";

                gfx.DrawString(line, font, XBrushes.Black, new XRect(40, y, page.Width - 80, page.Height), XStringFormats.TopLeft);
                y += 20;

                if (y > page.Height - 50)
                {
                    page = document.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    y = 40;
                }
            }

            var stream = new MemoryStream();
            document.Save(stream, false);
            stream.Position = 0;
            return stream;
        }

        public static MemoryStream ExportFinancialSummaryToPdf(DateTime start, DateTime end, decimal collected, decimal anticipated)
        {
            var document = new PdfDocument();
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);
            var titleFont = new XFont("Verdana", 14, XFontStyle.Bold);
            var bodyFont = new XFont("Verdana", 12);

            double y = 40;
            gfx.DrawString("Financial Report", titleFont, XBrushes.Black, new XRect(0, y, page.Width, 20), XStringFormats.TopCenter);
            y += 30;

            gfx.DrawString($"Date Range: {start:MM/dd/yyyy} - {end:MM/dd/yyyy}", bodyFont, XBrushes.Black, new XPoint(40, y));
            y += 30;

            gfx.DrawString($"Collected Revenue: ${collected:F2}", bodyFont, XBrushes.Black, new XPoint(40, y));
            y += 20;

            gfx.DrawString($"Anticipated Revenue: ${anticipated:F2}", bodyFont, XBrushes.Black, new XPoint(40, y));

            var stream = new MemoryStream();
            document.Save(stream, false);
            stream.Position = 0;
            return stream;
        }

        public static MemoryStream ExportReceiptToPdf(Reservation reservation, List<TransactionSummaryDto> transactionSummaries)
        {
            var document = new PdfDocument();
            var page = document.AddPage();
            var gfx = XGraphics.FromPdfPage(page);

            var titleFont = new XFont("Verdana", 14, XFontStyle.Bold);
            var hFont = new XFont("Verdana", 11, XFontStyle.Bold);
            var bodyFont = new XFont("Verdana", 10);
            var boldFont = new XFont("Verdana", 10, XFontStyle.Bold);

            double margin = 40, y = margin, line = 18;

            void NewPage() { page = document.AddPage(); gfx = XGraphics.FromPdfPage(page); y = margin; }
            void Ensure(double needed) { if (y + needed > page.Height - margin) NewPage(); }
            string C(decimal v) => v.ToString("C");

            int Rank(TransactionSummaryDto i)
            {
                var lbl = (i.Label ?? "").ToLowerInvariant();
                var isBase = lbl.Contains("base reservation") || lbl.StartsWith("base ");
                var isCancel = lbl.Contains("cancel");
                if (i.Amount < 0) return 4;
                if (isBase) return 1;
                if (isCancel) return 3;
                return 2;
            }

            gfx.DrawString("Reservation Receipt", titleFont, XBrushes.Black,
                new XRect(0, y, page.Width, line), XStringFormats.TopCenter);
            y += 28;

            Ensure(6 * line);
            gfx.DrawString("Reservation Details", hFont, XBrushes.Black, new XPoint(margin, y)); y += line;
            gfx.DrawString($"Name: {reservation.UserAccount?.FullName}", bodyFont, XBrushes.Black, new XPoint(margin, y)); y += line;
            gfx.DrawString($"Reservation ID: {reservation.ReservationId}", bodyFont, XBrushes.Black, new XPoint(margin, y)); y += line;
            gfx.DrawString($"Site: {reservation.Site?.Name}", bodyFont, XBrushes.Black, new XPoint(margin, y)); y += line;
            gfx.DrawString($"Dates: {reservation.StartDate:MM/dd/yyyy} - {reservation.EndDate:MM/dd/yyyy}", bodyFont, XBrushes.Black, new XPoint(margin, y)); y += line;
            gfx.DrawString($"Trailer Length: {reservation.TrailerLength} ft", bodyFont, XBrushes.Black, new XPoint(margin, y)); y += line;

            y += 10;

            Ensure(line);
            gfx.DrawString("Charges Breakdown", hFont, XBrushes.Black, new XPoint(margin, y)); y += line;

            decimal totalCharges = 0m, totalCredits = 0m;

            foreach (var item in transactionSummaries
                     .OrderBy(Rank)
                     .ThenBy(t => t.TransactionDateTime))
            {
                var amt = item.Amount;
                if (amt >= 0) totalCharges += amt; else totalCredits += -amt;

                Ensure(line);

                // Main label
                gfx.DrawString("• " + item.Label, bodyFont, XBrushes.Black,
                    new XPoint(margin + 20, y));

                // Amount on the right
                var displayAmt = amt < 0 ? $"({Math.Abs(amt):C})" : amt.ToString("C");
                gfx.DrawString(displayAmt, bodyFont, XBrushes.Black,
                    new XPoint(page.Width - margin - 100, y));
                y += line;

                // Breakdown line (from GetBreakdownText)
                var breakdown = item.GetBreakdownText();
                if (!string.IsNullOrWhiteSpace(breakdown))
                {
                    Ensure(line);
                    gfx.DrawString("   - " + breakdown, bodyFont, XBrushes.Gray,
                        new XPoint(margin + 20, y));
                    y += line;
                }

                y += 4;
                if (y > page.Height - margin) NewPage();
            }

            y += 6;
            Ensure(5 * line);
            gfx.DrawLine(XPens.Black, margin, y, page.Width - margin, y);
            y += 8;

            var netCollected = transactionSummaries
                .Where(t => t.Status.Equals("Paid", StringComparison.OrdinalIgnoreCase))
                .Sum(t => t.Amount);

            var balanceDue = totalCharges - netCollected;

            gfx.DrawString("Totals", hFont, XBrushes.Black, new XPoint(margin, y)); y += line;
            gfx.DrawString($"Total Charges: {C(totalCharges)}", bodyFont, XBrushes.Black, new XPoint(margin, y)); y += line;
            gfx.DrawString($"Refunds/Credits: ({C(totalCredits)})", bodyFont, XBrushes.Black, new XPoint(margin, y)); y += line;
            gfx.DrawString($"Balance Due: {C(balanceDue)}", boldFont, XBrushes.DarkRed, new XPoint(margin, y)); y += line;

            var stream = new MemoryStream();
            document.Save(stream, false);
            stream.Position = 0;
            return stream;
        }
    }
}