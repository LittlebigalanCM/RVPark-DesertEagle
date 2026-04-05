using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RVPark.Pages.Admin.Transactions
{
    [Authorize(Roles = SD.AdminRole + "," + SD.StaffRole + "," + SD.CampHostRole)]
    public class DetailsModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;

        [BindProperty]
        public Transaction? Transaction { get; set; }
        
        [BindProperty]
        public Reservation? Reservation { get; set; }

        [BindProperty]
        public Check? Check { get; set; }
        public DetailsModel(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public void OnGet(int transactionId)
        {
            Console.WriteLine("_____________________Transaction Details");

            if (transactionId > 0)
            {
                var transaction = _unitOfWork.Transaction.Get(t => t.TransactionId == transactionId, false, "Fee");

                if (transaction != null)
                {
                    Transaction = transaction;
                    Reservation = _unitOfWork.Reservation.Get(r => r.ReservationId == Transaction.ReservationId, false, "UserAccount,Site");
                    if (Transaction.PaymentMethod != null && Transaction.PaymentMethod.Equals(SD.CheckPayment))
                    {
                        Check = _unitOfWork.Check.Get(c => c.TransactionId == Transaction.TransactionId);
                    }
                    else
                    {
                        Check = null;
                    }
                }
                else
                {
                    Transaction = null;
                    Reservation = null;
                    Check = null;
                }
            }
            else
            {
                Transaction = null;
                Reservation = null;
                Check = null;
            }

        }
    }
}
