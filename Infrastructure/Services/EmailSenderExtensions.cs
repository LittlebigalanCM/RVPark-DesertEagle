using Microsoft.AspNetCore.Identity.UI.Services;
using System.Threading.Tasks;
using static Infrastructure.Services.EmailSender;

namespace Infrastructure.Services
{
    // This file contains extension methods for the IEmailSender interface to provide additional email sending functionalities.
    // Implemented to reduce code duplication and enhance maintainability.
    public static class EmailSenderExtensions
    {
        public static Task SendEmailConfirmationAsync(
            this IEmailSender emailSender,
            string email,
            string link)
        {
            if (emailSender is EmailSender concreteSender)
            {
                return concreteSender.SendEmailConfirmationAsync(email, link);
            }
            return Task.CompletedTask;
        }

        public static Task SendTempPasswordAsync(
            this IEmailSender emailSender,
            string email,
            string tempPassword)
        {
            if (emailSender is EmailSender concreteSender)
            {
                return concreteSender.SendTempPasswordAsync(email, tempPassword);
            }

            return Task.CompletedTask;
        }

        public static Task SendPasswordResetAsync(this IEmailSender emailSender, string email, string link)
        {
            if (emailSender is EmailSender concreteSender)
            {
                return concreteSender.SendPasswordResetAsync(email, link);
            }

            return Task.CompletedTask;
        }

        public static Task SendReservationReceiptAsync(
            this IEmailSender emailSender,
            string email,
            ReservationReceiptModel model)
        {
            if (emailSender is EmailSender concreteSender)
            {
                return concreteSender.SendReservationReceiptAsync(email, model);
            }
            return Task.CompletedTask;
        }

    }
}