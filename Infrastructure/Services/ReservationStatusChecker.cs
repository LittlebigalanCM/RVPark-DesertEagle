using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services
{
    public class ReservationStatusChecker(IServiceScopeFactory serviceScopeFactory) : BackgroundService
    {
        

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
             

                var scope = serviceScopeFactory.CreateScope();
                var _unitOfWOrk = scope.ServiceProvider.GetRequiredService<UnitOfWork>();


                var reservations = _unitOfWOrk.Reservation.GetAll(
                    r => r.ReservationStatus != SD.CancelledReservation 
                    && r.ReservationStatus != SD.CompleteReservation 
                    && r.ReservationStatus != SD.PendingReservation
                    );

                Console.WriteLine("Checking Reservations" + reservations.Count());
                foreach (var res in reservations)
                {
                    if (res.ReservationStatus != SD.CancelledReservation)
                    {

                        Console.WriteLine("++++++++RES: " + res.ReservationId);
                        Console.WriteLine("++++++++Date: " + DateTime.Now.Date);
                        Console.WriteLine("++++++++start: " + res.StartDate);
                        Console.WriteLine("++++++++end: " + res.EndDate);


                        //add reservation status
                        if (res.StartDate > DateTime.Now.Date)
                        {
                            res.ReservationStatus = SD.UpcomingReservation;
                            Console.WriteLine("++++++++UPCOMING: " + res.ReservationId);

                        }
                        else if (res.StartDate <= DateTime.Now.Date && res.EndDate >= DateTime.Now.Date)
                        {
                            res.ReservationStatus = SD.ActiveReservation;
                            Console.WriteLine("++++++++ACTIVE: " + res.ReservationId);

                        }
                        else if (res.EndDate < DateTime.Now.Date)
                        {
                            res.ReservationStatus = SD.CompleteReservation;
                            Console.WriteLine("++++++++COMPLETE: " + res.ReservationId);

                        }
                        _unitOfWOrk.Reservation.Update(res);
                        _unitOfWOrk.Commit();
                    }
                }
                Console.WriteLine("Current Reservations" + reservations.Count());


                await Task.Delay(TimeSpan.FromSeconds(3600), stoppingToken);

            }
        }
    }
}
