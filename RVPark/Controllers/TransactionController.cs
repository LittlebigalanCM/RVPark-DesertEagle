using ApplicationCore.Interfaces;
using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks.Dataflow;

namespace RVPark.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionController : Controller
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly ApplicationDbContext _dbContext;

        public TransactionController(UnitOfWork unitOfWork, ApplicationDbContext dbContext)
        {
            _unitOfWork = unitOfWork;
            _dbContext = dbContext;
        }

        [HttpGet]
        public IActionResult Get(int reservationId)
        {
            var transaction = _unitOfWork.Transaction.GetAll(
                predicate: null,
                includes: "Reservation.UserAccount,Fee"
            );

            if (reservationId > 0)
            {
                transaction = transaction.Where(f => f.ReservationId == reservationId);
            }

            var data = transaction.Select(f => new
            {
                transactionId = f.TransactionId,
                amount = f.Amount,
                reservationId = f.ReservationId,
                fullName = f.Reservation != null && f.Reservation.UserAccount != null
                    ? $"{f.Reservation.UserAccount.FirstName} {f.Reservation.UserAccount.LastName}"
                    : "Unknown",
                description = f.Description, 
                paymentMethod = f.PaymentMethod,
                Fee = f.Fee.Name,
                triggerType = f.TriggerType.ToString(),
                calculationType = f.CalculationType.ToString(),
                transactionDateTime = f.TransactionDateTime.ToString("MM-dd-yyyy HH:mm:ss"),
                previouslyRefunded = f.PreviouslyRefunded ? "Yes" : "No"
            });

            return Json(new { data });
        }


        [HttpGet("UpdateExtraFees")]
        public IActionResult UpdateExtraFees(double amount)
        {            
            
            Console.WriteLine("===============extra: " + ViewData["ExtraFees"]);


            return View(amount);
        }


        [HttpPost("Refund")]
        public IActionResult Refund(int transactionId)
        {
            Console.WriteLine("================== REFUND ==========");

            var transaction = _unitOfWork.Transaction.Get(t => t.TransactionId == transactionId);

            Console.WriteLine("================== REFUND ATTEMPT" + transaction.TransactionId + "==========");


            if (transaction.PreviouslyRefunded == false)
            {
                var refund = new Transaction();

                refund.Amount = transaction.Amount * -1;
                refund.ReservationId = transaction.ReservationId;
           
                refund.TransactionDateTime = DateTime.UtcNow;
                
                refund.FeeId = _unitOfWork.Fee.Get(type => type.Name == SD.RefundName).FeeId;
          
                refund.Description = "REFUND FOR: transactionId#" + transaction.TransactionId;
               
                refund.Reservation = transaction.Reservation;
                refund.PreviouslyRefunded = false;
                refund.PaymentMethod = transaction.PaymentMethod; //idk but yeah, assume refund is in the same format as the payment

                transaction.PreviouslyRefunded = true;
                refund.IsPaid = true;  //Assuming true here woooooo


                _unitOfWork.Transaction.Update(transaction);
                _unitOfWork.Transaction.Add(refund);
                _unitOfWork.Commit();
               

                return Json(new { success = true, message = "Refund/Reverse Refund - Successful" });
            }
            return Json(new { success = false, message = "Refund Unsuccesful: transaction was already refunded." });
        }

        [HttpGet("SearchTransactions")]
        public IActionResult SearchTransactions(int transactionId)
        {
            Console.WriteLine("================== TRANSACTION SEARCH==========");
            // Try to parse as integer for reservation ID search

            var allTransactions = _unitOfWork.Transaction.GetAll(
                predicate: null,
                includes: "Reservation.UserAccount,Fee"
            );

            allTransactions = allTransactions.Where<Transaction>(f => f.TransactionId == transactionId);


            var data = allTransactions.Select(f => new
            {
                transactionId = f.TransactionId,
                amount = f.Amount,
                reservationId = f.ReservationId,
                fullName = f.Reservation != null && f.Reservation.UserAccount != null
                    ? $"{f.Reservation.UserAccount.FirstName} {f.Reservation.UserAccount.LastName}"
                    : "Unknown",
                description = f.Description,
                paymentMethod = f.PaymentMethod,
                Fee = f.Fee.Name,
                triggerType = f.TriggerType.ToString(),
                calculationType = f.CalculationType.ToString(),
                transactionDateTime = f.TransactionDateTime.ToString("MM-dd-yyyy HH:mm:ss"),
                previouslyRefunded = f.PreviouslyRefunded ? "Yes" : "No"
            });

            return Json(new { data });

        }

        [HttpGet("ReservationSearch")]
        public IActionResult ReservationSearch(int reservationId)
        {
            Console.WriteLine("================== RESERVATION SEARCH==========");

            // Try to parse as integer for reservation ID search

            var transactions = _unitOfWork.Transaction.GetAll(
                predicate: null,
                includes: "Reservation.UserAccount,Fee"
            );

            transactions = transactions.Where<Transaction>(f => f.ReservationId == reservationId);



            // Format data for the DataTable
            var data = transactions.Select(f => new
            {
                transactionId = f.TransactionId,
                amount = f.Amount,
                reservationId = f.ReservationId,
                fullName = f.Reservation != null && f.Reservation.UserAccount != null
                    ? $"{f.Reservation.UserAccount.FirstName} {f.Reservation.UserAccount.LastName}"
                    : "Unknown",
                description = f.Description,
                f.PaymentMethod,
                Fee = f.Fee.Name,
                triggerType = f.TriggerType.ToString(),
                calculationType = f.CalculationType.ToString(),
                transactionDateTime = f.TransactionDateTime.ToString("MM-dd-yyyy HH:mm:ss"),
                previouslyRefunded = f.PreviouslyRefunded ? "Yes" : "No"
            });
            return Json(new { data });
        }
        [HttpPost("MarkAsPaid")]
        public IActionResult MarkAsPaid(int transactionId)
        {
            var txn = _unitOfWork.Transaction.Get(t => t.TransactionId == transactionId);
            if (txn == null)
                return NotFound("Transaction not found.");

            txn.IsPaid = true;
            txn.TransactionDateTime = DateTime.UtcNow;

            _dbContext.Attach(txn);
            _dbContext.Entry(txn).Property(t => t.IsPaid).IsModified = true;
            _dbContext.Entry(txn).Property(t => t.TransactionDateTime).IsModified = true;
            _dbContext.SaveChanges();

            return Ok(new { success = true, message = $"Transaction {txn.TransactionId} marked as paid." });
        }


    }
}
