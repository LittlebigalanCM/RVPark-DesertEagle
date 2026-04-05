using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using MimeKit;
namespace Infrastructure.Services
{
    public class EmailSender : IEmailSender
    {
        public EmailSender(IConfiguration _config)
        {

        }
        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var emailToSend = new MimeMessage();
            emailToSend.From.Add(new MailboxAddress("Desert Eagle RV Nellis", "deserteaglervnellis@gmail.com"));
            emailToSend.To.Add(MailboxAddress.Parse(email));
            emailToSend.Subject = subject;
            emailToSend.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = htmlMessage };
            ////send email using a GMAIL ACCOUNT
            using (var emailClient = new MailKit.Net.Smtp.SmtpClient())
            {
                emailClient.Connect("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
                //valid email address, with special application password
                //See https://support.google.com/mail/answer/185833?hl=en for details
                emailClient.Authenticate("deserteaglervnellis@gmail.com", "jxwkbcslxszdezjb");
                emailClient.Send(emailToSend);
                emailClient.Disconnect(true);
            }
            return Task.CompletedTask;
        }

        // This file contains methods for sending specific types of emails. HTML based email templates.
        // Implemented to provide a better user experience and reduce code duplication.
        public async Task SendEmailConfirmationAsync(string email, string link)
        {
            string subject = "Desert Eagle RV Park - Confirm Your Account ";

            string htmlMessage = $@"
                <!DOCTYPE html>
        <html>
        <head>
            <meta charset=""utf-8"" />
            <title>{subject}</title>
        </head>
        <body style=""font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 0; line-height: 1.6;"">
            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                    <td align=""center"" style=""padding: 20px 0;"">
                        <table role=""presentation"" width=""600"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 4px 10px rgba(0,0,0,0.1);"">
                            
                            <tr>
                                <td align=""center"" bgcolor=""#3a8dd6"" style=""padding: 30px 20px; color: #ffffff; font-size: 24px; font-weight: bold; border-radius: 8px 8px 0 0;"">
                                    Desert Eagle RV Park
                                </td>
                            </tr>

                            <tr>
                                <td style=""padding: 30px 40px 30px 40px;"">
                                    <h1 style=""color: #333333; font-size: 22px; margin: 0 0 20px 0;"">
                                        Welcome to Desert Eagle RV Park!
                                    </h1>
                                    <p style=""color: #555555; font-size: 16px; margin: 0 0 20px 0;"">
                                        Thank you for creating an account. Please click the button below to *confirm your email address* and activate your profile:
                                    </p>

                                    <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                                        <tr>
                                            <td align=""center"" bgcolor=""#28a745"" style=""border-radius: 5px;"">
                                                <a href=""{link}"" target=""_blank"" style=""font-size: 16px; font-weight: bold; text-decoration: none; color: #ffffff; padding: 12px 25px; border: 1px solid #28a745; display: inline-block; border-radius: 5px;"">
                                                    CONFIRM MY EMAIL
                                                </a>
                                            </td>
                                        </tr>
                                    </table>
                                    </td>
                            </tr>
                            <tr>
                                <td style=""padding:18px 40px 28px 40px;"">
                                <p style=""margin:12px 0 0 0; font-size:12px; color:#777;"">
                                    If you have questions, call 702-643-3060 or email rvpark@example.com.
                                </p>
                                </td>
                            </tr>

                            <tr>
                                <td align=""center"" bgcolor=""#eeeeee"" style=""padding: 20px 40px; font-size: 12px; color: #777777; border-radius: 0 0 8px 8px;"">
                                    <p style=""margin: 0;"">
                                        Desert Eagle RV Park | Nellis Air Force Base
                                    </p>
                                </td>
                            </tr>

                        </table>
                    </td>
                </tr>
            </table>
        </body>
        </html>";

            await SendEmailAsync(email, subject, htmlMessage);
        }

        public async Task SendTempPasswordAsync(string email, string tempPassword)
        {
            string subject = "Desert Eagle RV Park - Temporary Password";

            string htmlMessage = $@"
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset=""utf-8"" />
            <title>{subject}</title>
        </head>
        <body style=""font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 0; line-height: 1.6;"">
            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                    <td align=""center"" style=""padding: 20px 0;"">
                        <table role=""presentation"" width=""600"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 4px 10px rgba(0,0,0,0.1);"">
                            
                            <tr>
                                <td align=""center"" bgcolor=""#3a8dd6"" style=""padding: 30px 20px; color: #ffffff; font-size: 24px; font-weight: bold; border-radius: 8px 8px 0 0;"">
                                    Desert Eagle RV Park
                                </td>
                            </tr>

                            <tr>
                                <td style=""padding: 30px 40px 30px 40px;"">
                                    <h1 style=""color: #333333; font-size: 22px; margin: 0 0 20px 0;"">
                                        Welcome To Desert Eagle RV Park!
                                    </h1>
                                    <p style=""color: #555555; font-size: 16px; margin: 0 0 15px 0;"">
                                        Your account has been successfully created. Please use the temporary password below to log in:
                                    </p>
                                    
                                    <div style=""background-color: #f9f9f9; border: 1px solid #e0e0e0; padding: 15px; text-align: center; margin-bottom: 20px; border-radius: 4px;"">
                                        <p style=""font-size: 18px; color: #333333; font-weight: bold; margin: 0;"">
                                            Temporary Password: 
                                            <span style=""color: #CC0000; font-family: 'Courier New', monospace;"">{tempPassword}</span>
                                        </p>
                                    </div>
                                    
                                    <p style=""color: #555555; font-size: 16px; margin: 0 0 20px 0;"">
                                        **Important:** Please log in immediately and change your password for security.
                                    </p>
                           <tr>
                                <td style=""padding:18px 40px 28px 40px;"">
                                <p style=""margin:12px 0 0 0; font-size:12px; color:#777;"">
                                    If you have questions, call 702-643-3060 or email rvpark@example.com.
                                </p>
                                </td>
                            </tr>
                            <tr>
                                <td align=""center"" bgcolor=""#eeeeee"" style=""padding: 20px 40px; font-size: 12px; color: #777777; border-radius: 0 0 8px 8px;"">
                                    <p style=""margin: 0;"">
                                        Desert Eagle RV Park | Nellis Air Force Base
                                    </p>
                                </td>
                            </tr>

                        </table>
                    </td>
                </tr>
            </table>
        </body>
        </html>";

            await SendEmailAsync(email, subject, htmlMessage);
        }


        public async Task SendPasswordResetAsync(string email, string link)
        {
            string subject = "Desert Eagle RV Park - Password Reset Request";

            string htmlMessage = $@"
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset=""utf-8"" />
            <title>{subject}</title>
        </head>
        <body style=""font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 0; line-height: 1.6;"">
            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                    <td align=""center"" style=""padding: 20px 0;"">
                        <table role=""presentation"" width=""600"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 4px 10px rgba(0,0,0,0.1);"">
                            
                            <tr>
                                <td align=""center"" bgcolor=""#3a8dd6"" style=""padding: 30px 20px; color: #ffffff; font-size: 24px; font-weight: bold; border-radius: 8px 8px 0 0;"">
                                    Desert Eagle RV Park
                                </td>
                            </tr>

                            <tr>
                                <td style=""padding: 30px 40px 30px 40px;"">
                                    <h1 style=""color: #333333; font-size: 22px; margin: 0 0 20px 0;"">
                                        Password Reset
                                    </h1>
                                    <p style=""color: #555555; font-size: 16px; margin: 0 0 20px 0;"">
                                        You have requested a password reset. Please click the button below to securely set a new password:
                                    </p>

                                    <table role=""presentation"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                                        <tr>
                                            <td align=""center"" bgcolor=""#f0ad4e"" style=""border-radius: 5px;"">
                                                <a href=""{link}"" target=""_blank"" style=""font-size: 16px; font-weight: bold; text-decoration: none; color: #ffffff; padding: 12px 25px; border: 1px solid #f0ad4e; display: inline-block; border-radius: 5px;"">
                                                    RESET PASSWORD
                                                </a>
                                            </td>
                                        </tr>
                                    </table>

                                    <p style=""color: #555555; font-size: 14px; margin: 25px 0 0 0;"">
                                        This link is only valid for a limited time. If you did not request a password reset, you can safely ignore this email.
                                    </p>
                                </td>
                            </tr>
                            <tr>
                                <td style=""padding:18px 40px 28px 40px;"">
                                <p style=""margin:12px 0 0 0; font-size:12px; color:#777;"">
                                    If you have questions, call 702-643-3060 or email rvpark@example.com.
                                </p>
                                </td>
                            </tr>

                            <tr>
                                <td align=""center"" bgcolor=""#eeeeee"" style=""padding: 20px 40px; font-size: 12px; color: #777777; border-radius: 0 0 8px 8px;"">
                                    <p style=""margin: 0;"">
                                        Desert Eagle RV Park | Nellis Air Force Base
                                    </p>
                                </td>
                            </tr>

                        </table>
                    </td>
                </tr>
            </table>
        </body>
        </html>";

            await SendEmailAsync(email, subject, htmlMessage);
        }

        // This section is for the model for reservation receipt details
        public sealed class ReservationReceiptModel
        {
            public int ReservationId { get; init; }
            public string GuestName { get; init; } = "";
            public DateTime StartDate { get; init; }
            public DateTime EndDate { get; init; }
            public int NumberOfNights { get; init; }
            public string SiteName { get; init; } = "";
            public string SiteType { get; init; } = "";
            public string Status { get; init; } = "";
            public decimal TotalPaid { get; init; }
        }

        public async Task SendReservationReceiptAsync(string email, ReservationReceiptModel m)
        {
            string subject = $"Your Reservation Receipt - Desert Eagle RV Park (#{m.ReservationId})";

            string htmlMessage = $@"
            <!DOCTYPE html>
            <html>
            <head>
              <meta charset=""utf-8"" />
              <title>{subject}</title>
            </head>
            <body style=""font-family: Arial, sans-serif; background:#f4f4f4; margin:0; padding:0; line-height:1.6;"">
              <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"">
                <tr>
                  <td align=""center"" style=""padding:20px 0;"">
                    <table role=""presentation"" width=""600"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""border-collapse:collapse; background:#ffffff; border-radius:8px; box-shadow:0 4px 10px rgba(0,0,0,0.1);"">
                      <tr>
                        <td align=""center"" bgcolor=""#3a8dd6"" style=""padding:24px 20px; color:#ffffff; font-size:22px; font-weight:bold; border-radius:8px 8px 0 0;"">
                          Desert Eagle RV Park
                        </td>
                      </tr>

                      <tr>
                        <td style=""padding:28px 40px 12px 40px;"">
                          <h1 style=""margin:0 0 12px 0; font-size:20px; color:#333;"">Thank You For Your Reservation!</h1>
                          <p style=""margin:0; color:#555;"">Reservation Details:</p>
                        </td>
                      </tr>

                      <tr>
                        <td style=""padding:8px 40px 24px 40px;"">
                          <table width=""100%"" cellspacing=""0"" cellpadding=""0"" border=""0"" style=""border-collapse:collapse; font-size:14px; color:#333;"">
                            <tr>
                              <td style=""padding:8px 0; width:42%; color:#666;""><strong>Reservation #</strong></td>
                              <td style=""padding:8px 0;"">#{m.ReservationId}</td>
                            </tr>
                            <tr>
                              <td style=""padding:8px 0; color:#666;""><strong>Guest Name</strong></td>
                              <td style=""padding:8px 0;"">{m.GuestName}</td>
                            </tr>
                             <tr>
                              <td style=""padding:8px 0; color:#666;""><strong>Start Date</strong></td>
                              <td style=""padding:8px 0;"">{m.StartDate:MM/dd/yyyy}</td>
                            </tr>
                            <tr>
                              <td style=""padding:8px 0; color:#666;""><strong>End Date</strong></td>
                              <td style=""padding:8px 0;"">{m.EndDate:MM/dd/yyyy}</td>
                            </tr>
                            <tr>
                              <td style=""padding:8px 0; color:#666;""><strong>Number of Nights</strong></td>
                              <td style=""padding:8px 0;"">{m.NumberOfNights}</td>
                            </tr>
                            <tr>               
                              <td style=""padding:8px 0; color:#666;""><strong>Site Name</strong></td>
                              <td style=""padding:8px 0;"">{m.SiteName}</td>
                            </tr>
                            <tr>
                              <td style=""padding:8px 0; color:#666;""><strong>Site Type</strong></td>
                              <td style=""padding:8px 0;"">{m.SiteType}</td>
                            </tr>
                            <tr>
                              <td style=""padding:8px 0; color:#666;""><strong>Status</strong></td>
                              <td style=""padding:8px 0;"">{m.Status}</td>
                            </tr>
                          </table>
                        </td>
                      </tr>

                      <tr>
                        <td style=""padding:10px 40px 0 40px;"">
                          <hr style=""border:none; border-top:1px solid #eee; margin:0;"" />
                        </td>
                      </tr>

                      <tr>
                        <td style=""padding:18px 40px 28px 40px;"">
                          <p style=""margin:0; font-size:16px; color:#333;""><strong>Total Paid:</strong> ${m.TotalPaid:F2}</p>
                          <p style=""margin:12px 0 0 0; font-size:12px; color:#777;"">
                            If you have questions, call 702-643-3060 or email rvpark@example.com.
                          </p>
                        </td>
                      </tr>

                      <tr>
                        <td align=""center"" bgcolor=""#eeeeee"" style=""padding:18px 40px; font-size:12px; color:#777; border-radius:0 0 8px 8px;"">
                          Desert Eagle RV Park | Nellis Air Force Base<br/>
                          4907 Fam Camp Dr bldg 2889, Las Vegas, NV 89115
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>";

            await SendEmailAsync(email, subject, htmlMessage);
        }

    }// End class
}
