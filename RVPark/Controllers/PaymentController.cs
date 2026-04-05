using System;
using System.Linq;
using ApplicationCore.Dtos;
using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace RVPark.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        // Provides access to repositories and database commit operations
        private readonly UnitOfWork _unitOfWork;

        // Handles fee calculations and triggered transaction logic
        private readonly TransactionService _transactionService;

        public PaymentController(UnitOfWork unitOfWork, TransactionService transactionService)
        {
            _unitOfWork = unitOfWork;
            _transactionService = transactionService;
        }

        /// <summary>
        /// Marks a single transaction as paid.
        /// </summary>
        [HttpPost("MarkAsPaid")]
        public IActionResult MarkAsPaid([FromBody] int transactionId)
        {
            // Look up the transaction by ID
            var txn = _unitOfWork.Transaction.Get(t => t.TransactionId == transactionId);
            if (txn == null)
                return NotFound(new { success = false, message = "Transaction not found." });

            // Only update if not already marked as paid
            if (!txn.IsPaid)
            {
                txn.IsPaid = true;
                txn.TransactionDateTime = DateTime.UtcNow; // Timestamp the payment
                _unitOfWork.Transaction.Update(txn);
                _unitOfWork.Commit();
            }

            return Ok(new { success = true, message = $"Transaction {transactionId} marked as paid." });
        }

        /// <summary>
        /// Marks multiple transactions as paid in a batch operation.
        /// </summary>
        [HttpPost("MarkAsPaidBatch")]
        public IActionResult MarkAsPaidBatch([FromBody] int[] transactionIds)
        {
            // Validate request payload
            if (transactionIds == null || transactionIds.Length == 0)
                return BadRequest(new { success = false, message = "No transactions provided." });

            // Fetch all matching transactions
            var txns = _unitOfWork.Transaction
                .GetAll(t => transactionIds.Contains(t.TransactionId))
                .ToList();

            if (txns.Count == 0)
                return NotFound(new { success = false, message = "No matching transactions found." });

            // Mark each transaction as paid if not already
            foreach (var txn in txns)
            {
                if (txn.IsPaid) continue;

                txn.IsPaid = true;
                txn.TransactionDateTime = DateTime.UtcNow;
                _unitOfWork.Transaction.Update(txn);
            }

            // Commit all updates in one transaction
            _unitOfWork.Commit();

            return Ok(new { success = true, count = txns.Count, message = "Transactions marked as paid." });
        }

        /// <summary>
        /// Generates a summary of a reservation's payments and triggered fees.
        /// </summary>
        [HttpGet("ReceiptSummary")]
        public IActionResult ReceiptSummary(int reservationId)
        {
            // Fetch reservation
            var reservation = _unitOfWork.Reservation.Get(r => r.ReservationId == reservationId);
            if (reservation == null)
                return NotFound("Reservation not found.");

            // Load related data
            reservation.Site = _unitOfWork.Site.Get(s => s.SiteId == reservation.SiteId);
            reservation.UserAccount = _unitOfWork.UserAccount.Get(u => u.Id == reservation.UserId);

            // Calculate base lodging cost before fees
            var baseAmount = _transactionService.CalculateReservationCost(
                reservation.SiteId,
                reservation.StartDate,
                reservation.EndDate);

            // Duration in days for triggered fee calculations
            var days = (reservation.EndDate - reservation.StartDate).Days;

            // Generate fee summary including triggered rules
            var summary = _transactionService.ApplyTriggeredTransactionsAsync(
                reservation,
                baseAmount,
                days);

            // Load paid transactions tied to this reservation
            var transactions = _unitOfWork.Transaction
                .GetAll(t => t.ReservationId == reservationId && t.IsPaid, includes: "Fee")
                .OrderByDescending(t => t.TransactionDateTime)
                .ToList();

            // Construct response model
            return Ok(new
            {
                reservationId,
                guest = reservation.UserAccount?.FullName,
                site = reservation.Site?.Name,
                start = reservation.StartDate.ToShortDateString(),
                end = reservation.EndDate.ToShortDateString(),

                transactions = transactions.Select(t => new
                {
                    t.TransactionId,
                    t.Description,
                    Fee = t.Fee?.Name,
                    t.Amount,
                    t.IsPaid,
                    t.TransactionDateTime,
                    t.TriggerRuleSnapshotJson
                }),

                summary = summary
            });
        }

        /// <summary>
        /// Retrieves full details for a single transaction including fee, reservation, and trigger data.
        /// </summary>
        [HttpGet("TransactionDetails")]
        public IActionResult TransactionDetails(int transactionId)
        {
            // Load transaction with related Fee and Reservation
            var transaction = _unitOfWork.Transaction
                .Get(t => t.TransactionId == transactionId, includes: "Fee,Reservation");

            if (transaction == null)
                return NotFound("Transaction not found.");

            var reservation = transaction.Reservation;

            return Ok(new
            {
                transaction.TransactionId,
                transaction.ReservationId,
                Amount = transaction.Amount,
                transaction.Description,
                transaction.IsPaid,
                transaction.TransactionDateTime,
                Fee = transaction.Fee?.Name,
                TriggerType = transaction.TriggerType.ToString(),
                CalculationType = transaction.CalculationType.ToString(),
                TriggerRuleSnapshotJson = transaction.TriggerRuleSnapshotJson,
                GuestName = reservation?.UserAccount?.FullName,
                SiteName = reservation?.Site?.Name,
                Dates = $"{reservation?.StartDate:MM/dd/yyyy} - {reservation?.EndDate:MM/dd/yyyy}"
            });
        }
    }
}
