using ApplicationCore.Dtos;
using ApplicationCore.Enums;
using ApplicationCore.Interfaces;
using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Services;
using Infrastructure.Utilities;
using MailKit.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.IdentityModel.Tokens;
using Stripe;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Text.Json;
using static Infrastructure.Services.TransactionService;

namespace RVPark.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReservationController : Controller
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly TransactionService _transactionService;
        private readonly ILogger<ReservationController> _logger;
        private readonly ILoggerFactory _loggerFactory;
        public ReservationController(
            UnitOfWork unitOfWork,
            ILogger<ReservationController> logger,
            ILoggerFactory loggerFactory)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _loggerFactory = loggerFactory;

            _transactionService = new TransactionService(
                unitOfWork.Fee.GetAll().ToList(),
                new TransactionAuditService(),
                unitOfWork,
                loggerFactory.CreateLogger<TransactionService>());
        }

        [HttpGet]
        public IActionResult Get()
        {
            var draw = Convert.ToInt32(Request.Query["draw"]);
            var start = Convert.ToInt32(Request.Query["start"]);
            var length = Convert.ToInt32(Request.Query["length"]);

            var searchTerm = Request.Query["searchTerm"].ToString();
            var statusFilter = Request.Query["statusFilter"].ToString();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole(SD.AdminRole) || User.IsInRole(SD.StaffRole);

            // Base query (EF translation-friendly)
            var query = isAdmin
                ? _unitOfWork.Reservation.GetAllQueryable(
                      predicate: null,
                      includes: "UserAccount,Site,Site.SiteType")
                : _unitOfWork.Reservation.GetAllQueryable(
                      r => r.UserId == userId,
                      includes: "UserAccount,Site,Site.SiteType");

            int recordsTotal = query.Count();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();

                query = query.Where(r =>
                    (r.UserAccount != null &&
                     (r.UserAccount.FirstName + " " + r.UserAccount.LastName)
                         .ToLower().Contains(searchTerm))
                    ||
                    (!string.IsNullOrEmpty(r.Site.Name) &&
                     r.Site.Name.ToLower() == searchTerm)
                    ||
                    r.ReservationId.ToString() == searchTerm
                );
            }


            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                query = query.Where(r => r.ReservationStatus == statusFilter);
            }

            int recordsFiltered = query.Count();

            //sort rules
            var sortOrders = new List<(string ColumnName, bool Asc)>();
            int i = 0;

            while (Request.Query.ContainsKey($"order[{i}][column]"))
            {
                int idx = int.Parse(Request.Query[$"order[{i}][column]"]);
                var colName = Request.Query[$"columns[{idx}][data]"].ToString();
                bool asc = Request.Query[$"order[{i}][dir]"] == "asc";

                sortOrders.Add((colName, asc));
                i++;
            }

            var list = query.AsEnumerable();


            Func<Reservation, object> SortKey(string col) => col switch
            {
                "siteNumber" =>
                    r => int.TryParse(r.Site?.Name, out var num) ? num : r.SiteId,

                "siteName" => r => r.Site?.Name,

                "customerName" =>
                    r => (r.UserAccount != null)
                        ? r.UserAccount.FirstName + " " + r.UserAccount.LastName
                        : "Unknown",

                "startDate" => r => r.StartDate,
                "endDate" => r => r.EndDate,

                "reservationStatus" => r =>
                    r.ReservationStatus == SD.UpcomingReservation ? 1 :
                    r.ReservationStatus == SD.PendingReservation ? 2 :
                    r.ReservationStatus == SD.ActiveReservation ? 3 :
                    r.ReservationStatus == SD.CancelledReservation ? 4 :
                    r.ReservationStatus == SD.CompleteReservation ? 5 : 99,

                _ => r => r.ReservationId,
            };

            IOrderedEnumerable<Reservation>? ordered = null;

            foreach (var (ColumnName, Asc) in sortOrders)
            {
                var key = SortKey(ColumnName);

                if (ordered == null)
                {
                    ordered = Asc
                        ? list.OrderBy(key)
                        : list.OrderByDescending(key);
                }
                else
                {
                    ordered = Asc
                        ? ordered.ThenBy(key)
                        : ordered.ThenByDescending(key);
                }
            }

            if (ordered != null)
                list = ordered;

            var paged = list
                .Skip(start)
                .Take(length)
                .ToList();


            var data = paged.Select(r => new
            {
                reservationId = r.ReservationId,

                customerName = r.UserAccount != null
                    ? $"{r.UserAccount.FirstName} {r.UserAccount.LastName}"
                    : "Unknown",

                siteName = r.Site?.Name ?? $"Site #{r.SiteId}",

                siteNumber = (r.Site != null && int.TryParse(r.Site.Name, out var number))
                    ? number
                    : r.SiteId,

                startDate = r.StartDate.ToString("MM/dd/yyyy"),
                endDate = r.EndDate.ToString("MM/dd/yyyy"),

                reservationStatus = r.ReservationStatus
            });

            return Json(new
            {
                draw,
                recordsTotal,
                recordsFiltered,
                data
            });
        }

        [Authorize(Roles = SD.ClientRole)]
        [HttpGet("CurrentReservations")]
        public async Task<IActionResult> GetCurrentReservations()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var reservations = _unitOfWork.Reservation.GetAll(
                r => r.UserId == userId &&
                    (r.ReservationStatus == SD.UpcomingReservation || r.ReservationStatus == SD.ActiveReservation || r.ReservationStatus == SD.PendingReservation),
                includes: "UserAccount,Site,Site.SiteType"
            );

            var data = reservations.Select(r => new
            {
                r.ReservationId,
                StartDate = r.StartDate.ToString("MM/dd/yyyy"),
                EndDate = r.EndDate.ToString("MM/dd/yyyy"),
                SiteName = !string.IsNullOrEmpty(r.Site?.Name) ? r.Site.Name : $"Site #{r.SiteId}",
                ReservationStatus = r.ReservationStatus
            });

            return Json(new { data });
        }

        [Authorize(Roles = SD.ClientRole)]
        [HttpGet("PastReservations")]
        public IActionResult GetPastReservations()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var reservations = _unitOfWork.Reservation.GetAll(
                r => r.UserId == userId &&
                    (r.ReservationStatus == SD.CompleteReservation || r.ReservationStatus == SD.CancelledReservation),
                includes: "UserAccount,Site,Site.SiteType"
            );

            var data = reservations.Select(r => new
            {
                r.ReservationId,
                StartDate = r.StartDate.ToString("MM/dd/yyyy"),
                EndDate = r.EndDate.ToString("MM/dd/yyyy"),
                SiteName = !string.IsNullOrEmpty(r.Site?.Name) ? r.Site.Name : $"Site #{r.SiteId}",
                ReservationStatus = r.ReservationStatus
            });

            return Json(new { data });
        }


        [HttpGet("Search")]
        public IActionResult Search(string? term, string? filter)
        {
            if (string.IsNullOrEmpty(term) && string.IsNullOrEmpty(filter))
            {
                return BadRequest("Search term or filter is required");
            }

            var results = _unitOfWork.Reservation.GetAll(
                predicate: null,
                includes: "UserAccount,Site,Site.SiteType"
            );

            if (!string.IsNullOrEmpty(term))
            {
                if (int.TryParse(term, out int reservationId))
                {
                    results = results.Where(r => r.ReservationId == reservationId);
                }
                else
                {
                    string searchTerm = term.ToLower();
                    results = results.Where(r =>
                        (r.UserAccount?.FirstName?.ToLower().Contains(searchTerm) ?? false) ||
                        (r.UserAccount?.LastName?.ToLower().Contains(searchTerm) ?? false)
                    );
                }
            }

            if (!string.IsNullOrEmpty(filter))
            {
                results = results.Where(r => r.ReservationStatus == filter);
            }

            var data = results.Select(r => new
            {
                r.ReservationId,
                CustomerName = r.UserAccount != null ? $"{r.UserAccount.FirstName} {r.UserAccount.LastName}" : "Unknown",
                SiteName = r.Site != null && !string.IsNullOrEmpty(r.Site.Name) ? r.Site.Name : $"Site #{r.SiteId}",
                StartDate = r.StartDate.ToString("MM/dd/yyyy"),
                EndDate = r.EndDate.ToString("MM/dd/yyyy"),
                ReservationStatus = r.ReservationStatus
            });

            return Json(new { data });
        }

        [HttpPost("CheckAvailability")]
        public IActionResult CheckAvailability([FromBody] AvailabilityCheckRequest request)
        {
            if (request == null)
            {
                return BadRequest("Invalid request data");
            }

            if (!DateTime.TryParse(request.StartDate, out DateTime startDate) ||
                !DateTime.TryParse(request.EndDate, out DateTime endDate))
            {
                return BadRequest("Invalid date format");
            }

            if (startDate >= endDate)
            {
                return Json(new { available = false, reason = "Check-in date must be before check-out date" });
            }

            var originalReservation = _unitOfWork.Reservation.Get(
                r => r.ReservationId == request.ReservationId,
                includes: "Site"
            );

            if (originalReservation == null)
            {
                return NotFound("Reservation not found");
            }

            var site = _unitOfWork.Site.Get(s => s.SiteId == request.SiteId, includes: "SiteType");
            if (site == null)
            {
                return Json(new { available = false, reason = "Selected site does not exist" });
            }

            var overlappingReservations = _unitOfWork.Reservation.GetAll(
                predicate: r => r.SiteId == request.SiteId &&
                               r.ReservationId != request.ReservationId &&
                               r.EndDate > startDate &&
                               r.StartDate < endDate &&
                               r.ReservationStatus != SD.CancelledReservation &&
                               r.ReservationStatus != SD.CompleteReservation

            );

            if (overlappingReservations.Any())
            {
                return Json(new { available = false, reason = "Selected site is already booked for these dates" });
            }

            if (request.TrailerLength.HasValue && site.TrailerMaxSize.HasValue)
            {
                if ((double)request.TrailerLength.Value > site.TrailerMaxSize.Value)
                {
                    return Json(new { available = false, reason = $"Trailer length ({request.TrailerLength} ft) exceeds site maximum ({site.TrailerMaxSize} ft)" });
                }
            }


            return Json(new { available = true });
        }

        [HttpGet("GetAvailableSites")]
        public IActionResult GetAvailableSites(string startDate, string endDate, string siteTypeIds, string trailerLength = "")
        {
            try
            {
                if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate) || string.IsNullOrEmpty(siteTypeIds))
                {
                    return BadRequest("Start date, end date, and at least one site type are required");
                }

                if (!DateTime.TryParse(startDate, out DateTime parsedStartDate) ||
                    !DateTime.TryParse(endDate, out DateTime parsedEndDate))
                {
                    return BadRequest("Invalid date format");
                }

                if (parsedStartDate >= parsedEndDate)
                {
                    return BadRequest("Check-in date must be before check-out date");
                }

                var siteTypeIdList = siteTypeIds.Split(',')
                    .Select(id => int.TryParse(id, out int result) ? result : -1)
                    .Where(id => id != -1)
                    .ToList();

                decimal? parsedTrailerLength = null;
                if (!string.IsNullOrEmpty(trailerLength) && !string.IsNullOrWhiteSpace(trailerLength))
                {
                    if (decimal.TryParse(trailerLength, out decimal result))
                    {
                        parsedTrailerLength = result;
                    }
                }

                var allSites = _unitOfWork.Site.GetAll(
                    predicate: s => s.SiteTypeId.HasValue && siteTypeIdList.Contains(s.SiteTypeId.Value),
                    includes: "SiteType"
                ).ToList();

                if (parsedTrailerLength.HasValue)
                {
                    allSites = allSites
                        .Where(s => s.TrailerMaxSize == null || (decimal)s.TrailerMaxSize >= parsedTrailerLength.Value)
                        .ToList();

                }

                var overlappingReservations = _unitOfWork.Reservation.GetAll(
                    predicate: r => r.EndDate > parsedStartDate &&
                                    r.StartDate < parsedEndDate &&
                                    r.ReservationStatus != SD.CancelledReservation &&
                                    r.ReservationStatus != SD.CompleteReservation
                );

                var bookedSiteIds = overlappingReservations.Select(r => r.SiteId).ToList();
                var availableSites = allSites.Where(s => !bookedSiteIds.Contains(s.SiteId)).ToList();

                availableSites = availableSites.Where(s => !s.IsLocked).ToList();

                var siteTypePrices = new Dictionary<int, decimal>();
                foreach (var siteTypeId in siteTypeIdList)
                {
                    var currentPrice = _unitOfWork.Price.GetAll(
                        p => p.SiteTypeId == siteTypeId &&
                             p.StartDate <= parsedStartDate &&
                             (p.EndDate == null || p.EndDate >= parsedEndDate)
                    ).OrderByDescending(p => p.StartDate).FirstOrDefault();

                    siteTypePrices[siteTypeId] = currentPrice?.PricePerDay ?? 50.0m;
                }

                var sites = availableSites.Select(s => new
                {
                    siteId = s.SiteId,
                    name = !string.IsNullOrEmpty(s.Name) ? s.Name : $"Site #{s.SiteId}",
                    trailerMaxSize = s.TrailerMaxSize,
                    siteType = s.SiteType?.Name ?? "Unknown",
                    siteTypeId = s.SiteTypeId,
                    isHandicappedAccessible = s.IsHandicappedAccessible,
                    pricePerDay = s.SiteTypeId.HasValue ? siteTypePrices.GetValueOrDefault<int, decimal>(s.SiteTypeId.Value, 50.0m) : 50.0m
                }).ToList();

                return Json(new { sites });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // Caluclate min and max rates for a site type over a date range

        [HttpGet("GetSiteTypeRateRange")]
        public IActionResult GetSiteTypeRateRange(int siteTypeId, string startDate, string endDate)
        {
            if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
            {
                return BadRequest("Start date and end date are required");
            }

            if (!DateTime.TryParse(startDate, out DateTime parsedStartDate) ||
                !DateTime.TryParse(endDate, out DateTime parsedEndDate))
            {
                return BadRequest("Invalid date format");
            }

            if (parsedStartDate >= parsedEndDate)
            {
                return BadRequest("Check-in date must be before check-out date");
            }

            // Get all prices that overlap the date 
            var prices = _unitOfWork.Price.GetAll(
                p => p.SiteTypeId == siteTypeId &&
                     (!(parsedEndDate < p.StartDate) && (p.EndDate == null || !(parsedStartDate > p.EndDate)))
            ).OrderBy(p => p.StartDate).ToList();

            if (!prices.Any())
            {
                // No pricing found for this type/date range
                return Json(new { minRate = 0m, maxRate = 0m });
            }

            // Clip each price to the reservation window
            foreach (var price in prices)
            {
                if (price.StartDate < parsedStartDate) price.StartDate = parsedStartDate;
                if (price.EndDate == null || price.EndDate > parsedEndDate) price.EndDate = parsedEndDate;
            }

            var nightlyRates = new List<decimal>();

            for (var date = parsedStartDate; date < parsedEndDate; date = date.AddDays(1))
            {
                var rateForDate = prices
                    .Where(p => p.StartDate <= date && p.EndDate >= date)
                    .Select(p => p.PricePerDay)
                    .FirstOrDefault();

                if (rateForDate > 0)
                {
                    nightlyRates.Add(rateForDate);
                }
            }

            if (!nightlyRates.Any())
            {
                return Json(new { minRate = 0m, maxRate = 0m });
            }

            var minRate = nightlyRates.Min();
            var maxRate = nightlyRates.Max();

            return Json(new { minRate, maxRate });
        }



        [HttpGet("GetAvailablePreviousSites")]
        public IActionResult GetAvailablePreviousSites(string startDate, string endDate, string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
                {
                    return BadRequest("Start date, end date, and at least one site type are required");
                }

                if (!DateTime.TryParse(startDate, out DateTime parsedStartDate) ||
                    !DateTime.TryParse(endDate, out DateTime parsedEndDate))
                {
                    return BadRequest("Invalid date format");
                }

                if (parsedStartDate >= parsedEndDate)
                {
                    return BadRequest("Check-in date must be before check-out date");
                }

                if (string.IsNullOrEmpty(userId) == true)
                {
                    return BadRequest("User Id is required.");
                }



                var prevReservations = _unitOfWork.Reservation.GetAll(null, null, "Site,Site.SiteType").Where(r => r.UserId == userId && r.ReservationStatus != SD.ActiveReservation && r.ReservationStatus != SD.UpcomingReservation);

                var frequentSites = new Dictionary<Site, int>();

                foreach (var reservation in prevReservations)
                {
                    if (frequentSites.ContainsKey(reservation.Site) == true)
                    {
                        frequentSites[reservation.Site] += 1;
                    }
                    else
                    {
                        frequentSites[reservation.Site] = 1;
                    }
                }



                var overlappingReservations = _unitOfWork.Reservation.GetAll(
                    predicate: r => r.EndDate > parsedStartDate &&
                                    r.StartDate < parsedEndDate &&
                                    r.ReservationStatus != SD.CancelledReservation &&
                                    r.ReservationStatus != SD.CompleteReservation
                );

                var bookedSiteIds = overlappingReservations.Select(r => r.SiteId).ToList();


                foreach (var site in frequentSites.Select(s => s.Key))
                {
                    if (bookedSiteIds.Contains(site.SiteId) == true)
                    {
                        frequentSites.Remove(site);
                    }
                }

                var prevSites = frequentSites.OrderByDescending(x => x.Value);

                var sites = prevSites.Select(s => new
                {
                    siteId = s.Key.SiteId,
                    name = !string.IsNullOrEmpty(s.Key.Name) ? s.Key.Name : $"Site #{s.Key.SiteId}",
                    trailerMaxSize = s.Key.TrailerMaxSize,
                    siteType = s.Key.SiteType?.Name ?? "Unknown",
                    siteTypeId = s.Key.SiteTypeId,
                    pricePerDay = s.Key.SiteTypeId.HasValue ?
                    (decimal)(_unitOfWork.Price.GetAll(
                        p => p.SiteTypeId == s.Key.SiteTypeId &&
                             p.StartDate <= parsedStartDate &&
                             (p.EndDate == null || p.EndDate >= parsedEndDate)
                    ).OrderByDescending(p => p.StartDate).FirstOrDefault()?.PricePerDay ?? 50.0m) : 50.0m,

                    timesVisited = s.Value
                }).ToList();

                return Json(new { sites });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }



        [HttpPost("CalculateFeeChanges")]
        public IActionResult CalculateFeeChanges([FromBody] AvailabilityCheckRequest request)
        {
            if (request == null)
            {
                return BadRequest("Invalid request data");
            }

            if (!DateTime.TryParse(request.StartDate, out DateTime startDate) ||
                !DateTime.TryParse(request.EndDate, out DateTime endDate))
            {
                return BadRequest("Invalid date format");
            }

            if (startDate >= endDate)
            {
                return BadRequest("Check-in date must be before check-out date");
            }

            var originalReservation = _unitOfWork.Reservation.Get(
                r => r.ReservationId == request.ReservationId,
                includes: "Site"
            );

            if (originalReservation == null)
            {
                return NotFound("Reservation not found");
            }

            var site = _unitOfWork.Site.Get(s => s.SiteId == request.SiteId, includes: "SiteType");
            if (site == null)
            {
                return BadRequest("Selected site does not exist");
            }

            //decimal originalCost = _transactionService.CalculateCurrentCost(originalReservation); //this function doesn't work so ignore it <3
            decimal originalCost = _transactionService.CalculateReservationCost(originalReservation); //calculate the original cost based on 

            decimal newCost = _transactionService.CalculateReservationCost(
                request.SiteId,
                startDate,
                endDate); 

            decimal feeDifference = newCost - originalCost;

            return Json(new
            {
                originalTotal = originalCost,
                newTotal = newCost,
                feeChangeAmount = feeDifference
            });
        }

        [HttpPost("CancelReservation")]
        public async Task<IActionResult> CancelReservation([FromBody] CancelReservationRequest request)
        {
            int reservationId = request.ReservationId;
            _logger.LogInformation($"Starting cancellation for reservation {reservationId}");

            var reservation = await _unitOfWork.Reservation.GetFirstOrDefaultAsync(
                r => r.ReservationId == reservationId
            );
            if (reservation != null)
            {
                await _unitOfWork.Reservation.ReloadAsync(reservation, "Site,Site.SiteType");
            }

            if (reservation == null)
                return NotFound("Reservation not found.");

            reservation.ReservationStatus = SD.CancelledReservation;
            reservation.DynamicData ??= new();
            reservation.DynamicData["HoursBefore"] = (reservation.StartDate - DateTime.UtcNow).TotalHours;

            _unitOfWork.Reservation.Update(reservation);
            await _unitOfWork.CommitAsync();
            _logger.LogInformation($"Marked reservation {reservationId} as canceled.");

            var existingTransactions = await _unitOfWork.Transaction.GetAllAsync(t => t.ReservationId == reservationId);
            var totalPaid = existingTransactions.Where(t => t.IsPaid).Sum(t => t.Amount);

            var Fees = await _unitOfWork.Fee.GetAllAsync();
            var txService = new TransactionService(
                Fees.ToList(),
                new TransactionAuditService(),
                _unitOfWork,
                _loggerFactory.CreateLogger<TransactionService>()
            );

            int nights = (reservation.EndDate - reservation.StartDate).Days;
            decimal baseAmount = existingTransactions.FirstOrDefault(t => t.Description?.ToLower().Contains("base") == true)?.Amount ?? 0;

            Transaction? cancellationFeeTx = null;
            try
            {
                cancellationFeeTx = await txService.GetBestCancellationTransactionAsync(reservation, baseAmount, nights);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error evaluating cancellation fee for reservation {reservationId}");
                return StatusCode(500, $"Error evaluating cancellation fee: {ex.Message}");
            }

            decimal cancellationAmount = 0m;
            var originalPaymentMethod = SD.CreditCardPayment;

            if (cancellationFeeTx != null)
            {
                _logger.LogInformation($"Cancellation fee found: {cancellationFeeTx.Amount:C}");
                cancellationAmount = cancellationFeeTx.Amount;
                cancellationFeeTx.IsPaid = false;

                // Prefer the original reservation's payment method (most recent paid tx),
                // fall back to the existing SD.CreditCardPayment default if none found.
                originalPaymentMethod = existingTransactions
                    .Where(t => t.IsPaid && !string.IsNullOrEmpty(t.PaymentMethod))
                    .OrderByDescending(t => t.TransactionDateTime)
                    .Select(t => t.PaymentMethod)
                    .FirstOrDefault();

                cancellationFeeTx.PaymentMethod = originalPaymentMethod ?? SD.CreditCardPayment;

                if (cancellationFeeTx.TransactionDateTime == default) cancellationFeeTx.TransactionDateTime = DateTime.UtcNow;

                // Avoid sending navigation properties with the new entity to prevent EF from attempting to insert/update them.
                cancellationFeeTx.Reservation = null;
                cancellationFeeTx.Fee = null;

                try
                {
                    await _unitOfWork.Transaction.AddAsync(cancellationFeeTx);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to add cancellation fee transaction. Tx: ReservationId={ReservationId}, FeeId={FeeId}, Amount={Amount}, PaymentMethod={PaymentMethod}",
                                            cancellationFeeTx.ReservationId, cancellationFeeTx.FeeId, cancellationFeeTx.Amount, cancellationFeeTx.PaymentMethod);
                    return StatusCode(500, $"Failed to create cancellation fee transaction: {ex.Message}");
                }
            }
            else
            {
                _logger.LogWarning($"No cancellation fee found for reservation {reservationId}");
            }

            decimal refundAmount = totalPaid - cancellationAmount;

            if (refundAmount >= 0)
            {
                _logger.LogInformation($"Refund amount: {refundAmount:C}");
                if (cancellationFeeTx != null)
                {
                    cancellationFeeTx.IsPaid = true;
                    _unitOfWork.Transaction.Update(cancellationFeeTx);
                }

                if (refundAmount > 0)
                {
                    var refundType = Fees.FirstOrDefault(t =>
                                            t.Name.ToLower().Contains("refund") || t.DisplayLabel.ToLower().Contains("refund"));

                    // ... later, when creating the refund transaction, replace the incorrect assignment:
                    var refundTx = new Transaction
                    {
                        ReservationId = reservationId,
                        Amount = -refundAmount,
                        Description = "Refund after cancellation",
                        TransactionDateTime = DateTime.UtcNow,
                        FeeId = refundType?.FeeId ?? 0,
                        TriggerType = TriggerType.Manual,
                        IsPaid = false,
                        // Prefer the original reservation's payment method (most recent paid tx),
                        // fall back to SD.CreditCardPayment if none exists.
                        PaymentMethod = originalPaymentMethod ?? SD.CreditCardPayment
                    };

                    try
                    {
                        await _unitOfWork.Transaction.AddAsync(refundTx);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to add refund transaction for reservation {reservationId}");
                        return StatusCode(500, $"Failed to create refund transaction: {ex.Message}");
                    }
                }
            }
            else
            {
                _logger.LogInformation($"No refund applicable. Negative balance: {refundAmount:C}");
                refundAmount = 0;
            }

            await _unitOfWork.CommitAsync();

            return Ok(new
            {
                message = "Reservation canceled. Refund calculated.",
                amountPaid = totalPaid,
                cancellationFee = cancellationAmount,
                refundAmount = refundAmount,
                balanceDue = cancellationAmount - totalPaid
            });
        }


        [HttpGet("GetSiteDetails")]
        public IActionResult GetSiteDetails(int siteId, string? referenceDate = null)
        {
            if (siteId <= 0)
            {
                return BadRequest("Invalid site ID");
            }

            // parse referenceDate if provided, otherwise fall back to now
            DateTime priceReference;
            if (!string.IsNullOrWhiteSpace(referenceDate) && DateTime.TryParse(referenceDate, out var parsed))
            {
                priceReference = parsed;
            }
            else
            {
                priceReference = DateTime.Now;
            }

            var site = _unitOfWork.Site.Get(
                s => s.SiteId == siteId,
                includes: "SiteType"
            );

            if (site == null)
            {
                return NotFound("Site not found");
            }

            var sitePhoto = _unitOfWork.Photo.GetAll(p => p.SiteId == siteId).FirstOrDefault();

            decimal pricePerDay = 50.0m;
            if (site.SiteTypeId.HasValue)
            {
                var currentPrice = _unitOfWork.Price.GetAll(
                    p => p.SiteTypeId == site.SiteTypeId.Value &&
                         p.StartDate <= priceReference &&
                         (p.EndDate == null || p.EndDate >= priceReference)
                ).OrderByDescending(p => p.StartDate).FirstOrDefault();

                pricePerDay = currentPrice?.PricePerDay ?? 50.0m;
            }

            return Json(new
            {
                siteId = site.SiteId,
                name = site.Name ?? $"Site #{site.SiteId}",
                description = site.Description ?? "No description available",
                trailerMaxSize = site.TrailerMaxSize,
                siteTypeId = site.SiteTypeId,
                siteTypeName = site.SiteType?.Name ?? "Unknown",
                pricePerDay = pricePerDay,
                sitePhoto = sitePhoto?.Name,
                isHandicappedAccessible = site.IsHandicappedAccessible
            });
        }

        [HttpPost("CheckAvailabilityForNew")]
        public IActionResult CheckAvailabilityForNew([FromBody] NewReservationCheckRequest request)
        {
            if (request == null)
            {
                return BadRequest("Invalid request data");
            }

            if (!DateTime.TryParse(request.StartDate, out DateTime startDate) ||
                !DateTime.TryParse(request.EndDate, out DateTime endDate))
            {
                return BadRequest("Invalid date format");
            }

            if (startDate >= endDate)
            {
                return Json(new { available = false, reason = "Check-in date must be before check-out date" });
            }

            var site = _unitOfWork.Site.Get(s => s.SiteId == request.SiteId, includes: "SiteType");
            if (site == null)
            {
                return Json(new { available = false, reason = "Selected site does not exist" });
            }

            var overlappingReservations = _unitOfWork.Reservation.GetAll(
                predicate: r => r.SiteId == request.SiteId &&
                                r.EndDate > startDate &&
                                r.StartDate < endDate &&
                                r.ReservationStatus != SD.CancelledReservation &&
                                r.ReservationStatus != SD.CompleteReservation
            );

            if (overlappingReservations.Any())
            {
                return Json(new { available = false, reason = "Selected site is already booked for these dates" });
            }

            if (request.TrailerLength.HasValue && site.TrailerMaxSize.HasValue)
            {
                if ((double)request.TrailerLength.Value > site.TrailerMaxSize.Value)
                {
                    return Json(new { available = false, reason = $"Trailer length ({request.TrailerLength} ft) exceeds site maximum ({site.TrailerMaxSize} ft)" });
                }
            }

            return Json(new { available = true });
        }


        [HttpPost("CreatePaymentIntent")]
        public IActionResult OnPostCreatePaymentIntent()
        {
            var amount = 5000; // e.g., $50.00
            var options = new PaymentIntentCreateOptions
            {
                Amount = amount,
                Currency = "usd",
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true
                }
            };

            var service = new PaymentIntentService();
            var intent = service.Create(options);

            return new JsonResult(new { clientSecret = intent.ClientSecret });
        }


        public class NewReservationCheckRequest
        {
            public int SiteId { get; set; }
            public string StartDate { get; set; }
            public string EndDate { get; set; }
            public decimal? TrailerLength { get; set; }
        }

        public class AvailabilityCheckRequest
        {
            public int ReservationId { get; set; }
            public int SiteId { get; set; }
            public string StartDate { get; set; }
            public string EndDate { get; set; }
            public decimal? TrailerLength { get; set; }
        }

        [HttpPost("ConfirmPCSOrders")]
        public async Task<IActionResult> ConfirmPCSOrders([FromBody] ConfirmPCSOrdersRequest request)
        {
            int reservationId = request.ReservationId;
            //int documentId = request.DocumentId;
            _logger.LogInformation($"Starting PCS Orders confirmation for reservation {reservationId}");
            Console.WriteLine($"Starting PCS Orders confirmation for reservation {reservationId}");
            //get the reservation
            var reservation = await _unitOfWork.Reservation.GetFirstOrDefaultAsync(
                r => r.ReservationId == reservationId
            );
            if (reservation != null)
            {
                await _unitOfWork.Reservation.ReloadAsync(reservation, "Site,Site.SiteType");
            }

            if (reservation == null)
                return NotFound("Reservation not found.");

            //check if it does have a PCS requirement
            if (reservation.RequiresPCS.HasValue && reservation.RequiresPCS == true)
            {
                //if so, get the document
                var document = await _unitOfWork.Document.GetFirstOrDefaultAsync(
                    d => d.ReservationId == reservation.ReservationId && d.DocType == SD.PCSDocument
                );
                if (document == null)
                {
                    _logger.LogInformation($"Document not found for reservation {reservationId}");
                    Console.WriteLine($"Document not found for reservation {reservationId}");
                    return NotFound("Document not found.");
                }

                //mark document as approved
                document.IsApproved = true;

                _unitOfWork.Document.Update(document);
                await _unitOfWork.CommitAsync();
                _logger.LogInformation($"Marked PCS orders document {document.Id} as approved for reservation {reservationId}.");

                //Change reservation status if all documents are approved
                //if there is a disability document, check if it's approved too
                if (reservation.RequiresDisability.HasValue && reservation.RequiresDisability == true)
                {
                    var disabilityDoc = await _unitOfWork.Document.GetFirstOrDefaultAsync(
                        d => d.ReservationId == reservationId && d.DocType == SD.DisabilityDocument
                    );
                    if (disabilityDoc != null && disabilityDoc.IsApproved == true)
                    {
                        //both documents are approved, mark as upcoming!
                        _logger.LogInformation($"All documents for reservation {reservationId} approved. Marking as confirmed");
                        reservation.ReservationStatus = SD.UpcomingReservation;
                    }
                    //otherwise, no status change. There's still documentation that requires approval
                    else { 
                        _logger.LogInformation($"Reservation {reservationId} still has disability documents needing to be approved. will stay pending");
                    }
                }
                else //no other documentation needing approval, mark as upcoming
                {
                    _logger.LogInformation($"All documents for reservation {reservationId} approved. Marking as confirmed");
                    reservation.ReservationStatus = SD.UpcomingReservation;
                }

                _unitOfWork.Reservation.Update(reservation);
                await _unitOfWork.CommitAsync();
                _logger.LogInformation($"Reservation {reservationId} updated in database.");


            }
            else 
            {
                _logger.LogInformation($"No PCS orders for reservation {reservationId} to confirm");
                return NotFound("Reservation does not have any PCS orders to confirm");

            }

            return Ok(new
            {
                message = "PCS Orders confirmed."
            });
        }

        [HttpPost("ConfirmDisability")]
        public async Task<IActionResult> ConfirmDisability([FromBody] ConfirmDisabilityRequest request)
        {
            int reservationId = request.ReservationId;
            _logger.LogInformation($"Starting Disability confirmation for reservation {reservationId}");
            Console.WriteLine($"Starting Disability confirmation for reservation {reservationId}");
            //get the reservation
            var reservation = await _unitOfWork.Reservation.GetFirstOrDefaultAsync(
                r => r.ReservationId == reservationId
            );
            if (reservation != null)
            {
                await _unitOfWork.Reservation.ReloadAsync(reservation, "Site,Site.SiteType");
            }

            if (reservation == null)
                return NotFound("Reservation not found.");

            //check if it does have a Disability requirement
            if (reservation.RequiresDisability.HasValue && reservation.RequiresDisability == true)
            {
                //if so, get the document
                var document = await _unitOfWork.Document.GetFirstOrDefaultAsync(
                    d => d.ReservationId == reservation.ReservationId && d.DocType == SD.DisabilityDocument
                );
                if (document == null)
                {
                    _logger.LogInformation($"Document not found for reservation {reservationId}");
                    Console.WriteLine($"Document not found for reservation {reservationId}");
                    return NotFound("Document not found.");
                }

                //mark document as approved
                document.IsApproved = true;

                _unitOfWork.Document.Update(document);
                await _unitOfWork.CommitAsync();
                _logger.LogInformation($"Marked disability document {document.Id} as approved for reservation {reservationId}.");

                //Change reservation status if all documents are approved
                //if there is a PCS document, check if it's approved too
                if (reservation.RequiresPCS.HasValue && reservation.RequiresPCS == true)
                {
                    var PCSDoc = await _unitOfWork.Document.GetFirstOrDefaultAsync(
                        d => d.ReservationId == reservationId && d.DocType == SD.PCSDocument
                    );
                    if (PCSDoc != null && PCSDoc.IsApproved == true)
                    {
                        //both documents are approved, mark as upcoming!
                        _logger.LogInformation($"All documents for reservation {reservationId} approved. Marking as confirmed");
                        reservation.ReservationStatus = SD.UpcomingReservation;
                    }
                    //otherwise, no status change. There's still documentation that requires approval
                    else { 
                        _logger.LogInformation($"Reservation {reservationId} still has PCS documents needing to be approved. will stay pending");
                    }
                }
                else //no other documentation needing approval, mark as upcoming
                {
                    _logger.LogInformation($"All documents for reservation {reservationId} approved. Marking as confirmed");
                    reservation.ReservationStatus = SD.UpcomingReservation;
                }

                _unitOfWork.Reservation.Update(reservation);
                await _unitOfWork.CommitAsync();
                _logger.LogInformation($"Reservation {reservationId} updated in database.");


            }
            else 
            {
                _logger.LogInformation($"No PCS orders for reservation {reservationId} to confirm");
                return NotFound("Reservation does not have any PCS orders to confirm");

            }

            return Ok(new
            {
                message = "Disability Documents confirmed."
            });
        }

        [HttpPost("RevertPCSOrders")]
        public async Task<IActionResult> RevertPCSOrders([FromBody] JsonElement payload)
        {
            try
            {
                int reservationId = payload.GetProperty("reservationId").GetInt32();

                var reservation = await _unitOfWork.Reservation.GetFirstOrDefaultAsync(
                    r => r.ReservationId == reservationId,
                    includeProperties: "Site,Site.SiteType"
                );

                if (reservation == null)
                    return NotFound(new { message = "Reservation not found." });

                var document = await _unitOfWork.Document.GetFirstOrDefaultAsync(
                    d => d.ReservationId == reservation.ReservationId && d.DocType == SD.PCSDocument
                );

                if (document == null)
                    return NotFound(new { message = "PCS Orders document not found." });

                document.IsApproved = false;
                _unitOfWork.Document.Update(document);

                reservation.ReservationStatus = SD.PendingReservation;
                _unitOfWork.Reservation.Update(reservation);

                await _unitOfWork.CommitAsync();

                return Ok(new { message = "PCS Orders reverted successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Error reverting PCS Orders: {ex.Message}" });
            }
        }

        [HttpPost("RevertDisability")]
        public async Task<IActionResult> RevertDisability([FromBody] JsonElement payload)
        {
            try
            {
                int reservationId = payload.GetProperty("reservationId").GetInt32();

                var reservation = await _unitOfWork.Reservation.GetFirstOrDefaultAsync(
                    r => r.ReservationId == reservationId,
                    includeProperties: "Site,Site.SiteType"
                );

                if (reservation == null)
                    return NotFound(new { message = "Reservation not found." });

                var document = await _unitOfWork.Document.GetFirstOrDefaultAsync(
                    d => d.ReservationId == reservation.ReservationId && d.DocType == SD.DisabilityDocument
                );

                if (document == null)
                    return NotFound(new { message = "Disability document not found." });

                document.IsApproved = false;
                _unitOfWork.Document.Update(document);

                reservation.ReservationStatus = SD.PendingReservation;
                _unitOfWork.Reservation.Update(reservation);

                await _unitOfWork.CommitAsync();

                return Ok(new { message = "Disability documentation reverted successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Error reverting Disability documents: {ex.Message}" });
            }
        }


    }//end controller class
}