namespace Infrastructure.Utilities
{
    public class SD
    {
        //user roles
        public const string AdminRole = "Admin";
        public const string ClientRole = "Client";
        public const string StaffRole = "Staff";
        public const string CampHostRole = "Camp Host";

        //adult, child and pet limits. If limit is exceeded, extra fees are added.
        // This is incorrect, as children and pets do not cause any extra fees, so these probably aren't being referenced at all. -Matt
        //public const int AdultLimit = 4;
        //public const int ChildLimit = 4;
        //public const int PetLimit = 2;

        //names of transaction types
        public const string BaseReservationCostName = "Base Reservation Cost";
        public const string LateFeeName = "Late Fee";
        public const string EarlyCheckoutFeeName = "Early Checkout Fee";
        public const string DamageFeeName = "Damage Fee";

        public const string RefundName = "Refund";
        public const string OtherFeeName = "Other Fee";
        public const string PaymentName = "Payment";

        //Payment methods
        public const string CashPayment = "Cash";
        public const string CheckPayment = "Check";
        public const string CreditCardPayment = "Credit Card";



        //reservation statuses
        public const string ActiveReservation = "Active";
        public const string UpcomingReservation = "Confirmed";
        public const string CompleteReservation = "Completed";
        public const string CancelledReservation = "Cancelled";
        public const string PendingReservation = "Pending"; //Confirmed and needs more info, like uploading PCs orders

        //Document types
        public const string PCSDocument = "PCS Orders";
        public const string DisabilityDocument = "Disability Documentation";
    }

}
