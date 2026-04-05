using ApplicationCore.Models;
using ApplicationCore.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;
using System.IO;
using Infrastructure.Utilities;
using Infrastructure.Data;
using ApplicationCore.Dtos;

namespace RVPark.Pages.Client.Reservations
{
    public class ReservationHistoryModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;

        public ReservationHistoryModel(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public List<ReservationDto> PastReservations { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl
        {
            get; set;
        }
        public void OnGet()
        {
            var userId = User.Identity?.Name;
            PastReservations = _unitOfWork.Reservation
                .GetAll(r => r.UserAccount.UserName == userId &&
                             (r.ReservationStatus == SD.CompleteReservation || r.ReservationStatus == SD.CancelledReservation),
                        includes: "Site,UserAccount")
                .Select(r =>
                {
                    var transactions = _unitOfWork.Transaction
                        .GetAll(t => t.ReservationId == r.ReservationId)
                        .ToList();

                    var totalPaid = transactions.Where(t => t.Amount > 0).Sum(t => t.Amount);
                    var totalRefunded = transactions.Where(t => t.Amount < 0).Sum(t => t.Amount);
                    var cancellationFee = transactions
                        .Where(t => t.Description != null && t.Description.Contains("Cancellation Fee"))
                        .Sum(t => t.Amount);

                    var balance = cancellationFee > totalPaid ? cancellationFee - totalPaid : 0;

                    var unpaidTransaction = transactions
                        .FirstOrDefault(t => t.Description != null &&
                            t.Description.Contains("Cancellation Fee") &&
                            t.Amount > 0 &&
                        !t.IsPaid);

                    return new ReservationDto
                    {
                        ReservationId = r.ReservationId,
                        StartDate = r.StartDate,
                        EndDate = r.EndDate,
                        SiteName = r.Site?.Name ?? "N/A",
                        Status = r.ReservationStatus ?? "Unknown",
                        TransactionId = unpaidTransaction?.TransactionId,
                        TotalPaid = totalPaid,
                        ExtraFees = cancellationFee > totalPaid ? cancellationFee - totalPaid : 0
                    };

                })
                .ToList();
        }

    }
}