using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;

namespace RVPark.Pages.Client.Reservations
{
    [Authorize] // Require authentication for this page
    public class CreateModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly UserManager<IdentityUser> _userManager;

        public CreateModel(UnitOfWork unitOfWork, UserManager<IdentityUser> userManager)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
        }

        [BindProperty]
        public ApplicationCore.Models.Reservation Reservation { get; set; }

        public SelectListItem UserItem { get; set; }
        public IEnumerable<SelectListItem> SiteList { get; set; }
        public IEnumerable<ApplicationCore.Models.SiteType> SiteTypes { get; set; }
        public Dictionary<int, decimal> SiteTypePrices { get; set; } = new Dictionary<int, decimal>();



        public Dictionary<Site, int> FrequentSites { get; set; } = new Dictionary<Site, int>();
        public List<SelectListItem> FrequentSitesList {  get; set; }

        [BindProperty]
        public bool BookPastSite { get; set; }

        [BindProperty]
        public int? NewSiteId { get; set; }

        [BindProperty]
        public int? PastSiteId { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // Check if user is authenticated - if not, redirect to browse page
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToPage("./Browse");
            }

            Reservation = new ApplicationCore.Models.Reservation
            {
                StartDate = DateTime.UtcNow.Date.AddDays(1),
                EndDate = DateTime.UtcNow.Date.AddDays(8)
            };

            var claimsIdentity = User.Identity as ClaimsIdentity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null)
            {
                UserAccount user = _unitOfWork.UserAccount.Get(u => u.Id == claim.Value);

                if (user == null)
                {
                    // User not found in our system, redirect to browse
                    return RedirectToPage("./Browse");
                }

                UserItem = new SelectListItem
                {
                    Value = user.Id,
                    Text = $"{user.FirstName} {user.LastName} ({user.Email})",
                    Selected = true
                };

                // Get all sites for the dropdown
                var sites = _unitOfWork.Site.GetAll();
                SiteList = sites.Select(s => new SelectListItem
                {
                    Value = s.SiteId.ToString(),
                    Text = !string.IsNullOrEmpty(s.Name) ? s.Name : $"Site #{s.SiteId}"
                });


                //get all previous reservations
                //var prevReservations = _unitOfWork.Reservation.GetAll(null, null, "Site,Site.SiteType").Where(r => r.UserId == user.Id && r.ReservationStatus != SD.ActiveReservation && r.ReservationStatus != SD.UpcomingReservation);


                //create a dictionary of most frequented sites visited 
                //foreach (var reservation in prevReservations)
                //{
                //    if(FrequentSites.ContainsKey(reservation.Site) == true)
                //    {
                //        FrequentSites[reservation.Site] += 1;
                //    }
                //    else
                //    {
                //        FrequentSites[reservation.Site] = 1;
                //    }
                //}

                //get available sites 
                var availableSitesList = _unitOfWork.Site.GetAll();


                //remove any previous visited site that isn't available.
                foreach (var site in FrequentSites.Select(s => s.Key))
                {
                    if(availableSitesList.Contains(site) == false)
                    {
                        FrequentSites.Remove(site);
                    }
                }

                var prevSites = FrequentSites.OrderByDescending(x => x.Value);


                var selectedSites = prevSites.Select(s => new SelectListItem
                {
                    Value = s.Key.SiteId.ToString(),
                    Text = s.Key.SiteType.Name + " : " + s.Key.Name + " - Visited " + s.Value.ToString() + " times",
                }).ToList();

                FrequentSitesList = new List<SelectListItem>();

                FrequentSitesList.AddRange(selectedSites);

                // Get all site types
                SiteTypes = _unitOfWork.SiteType.GetAll();

                // Get pricing for each site type
                foreach (var st in SiteTypes)
                {
                    SiteTypePrices[st.SiteTypeId] = GetCurrentPriceForSiteType(st.SiteTypeId);
                }
            }
            else
            {
                // No valid claim found, redirect to browse
                return RedirectToPage("./Browse");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Ensure user is still authenticated
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToPage("./Browse");
            }

            if (!ModelState.IsValid)
            {
                // Repopulate the form fields
                var claimsIdentity = User.Identity as ClaimsIdentity;
                var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);

                if (claim != null)
                {
                    var user = _unitOfWork.UserAccount.Get(u => u.Id == claim.Value);
                    if (user != null)
                    {
                        UserItem = new SelectListItem
                        {
                            Value = user.Id,
                            Text = $"{user.FirstName} {user.LastName} ({user.Email})",
                            Selected = true
                        };

                        var prevReservations = _unitOfWork.Reservation.GetAll(null, null, "Site,Site.SiteType").Where(r => r.UserId == user.Id && r.ReservationStatus != SD.ActiveReservation && r.ReservationStatus != SD.UpcomingReservation);

                        foreach (var reservation in prevReservations)
                        {
                            if (FrequentSites.ContainsKey(reservation.Site) == true)
                            {
                                FrequentSites[reservation.Site] += 1;
                            }
                            else
                            {
                                FrequentSites[reservation.Site] = 1;
                            }
                        }

                        var prevSites = new List<SelectListItem>();

                        var selectedSites = FrequentSites.OrderByDescending(x => x.Value).Select(s => new SelectListItem
                        {
                            Value = s.Key.SiteId.ToString(),
                            Text = s.Key.SiteType.Name + " : " + s.Key.Name + " - Visited " + s.Value.ToString() + " times",
                        }).ToList();


                        prevSites.AddRange(selectedSites);

                        FrequentSitesList = prevSites;
                    }
                    else
                    {
                        FrequentSitesList = new List<SelectListItem>();
                    }
                }

                var sites = _unitOfWork.Site.GetAll();
                SiteList = sites.Select(s => new SelectListItem
                {
                    Value = s.SiteId.ToString(),
                    Text = s.Name
                });


                // Repopulate site types and pricing
                SiteTypes = _unitOfWork.SiteType.GetAll();
                foreach (var st in SiteTypes)
                {
                    SiteTypePrices[st.SiteTypeId] = GetCurrentPriceForSiteType(st.SiteTypeId);
                }

                return Page();
            }


            //if(BookPastSite == true)
            //{
            //    Reservation.SiteId = PastSiteId ?? default(int);
            //}
            //else
            //{
            //    Reservation.SiteId = NewSiteId ?? default(int);
            //}


            if (Reservation.StartDate == DateTime.Now.Date)
            {
                Reservation.ReservationStatus = SD.ActiveReservation;
            }
            else
            {
                Reservation.ReservationStatus = SD.UpcomingReservation;

            }
            // Validate site selection
            var site = _unitOfWork.Site.Get(s => s.SiteId == Reservation.SiteId);
            if (site == null)
            {
                ModelState.AddModelError(string.Empty, "Selected site does not exist.");
                await OnGetAsync();
                return Page();
            }

            // Get the site type separately
            var siteType = _unitOfWork.SiteType.Get(st => st.SiteTypeId == site.SiteTypeId);
            if (siteType == null)
            {
                ModelState.AddModelError(string.Empty, "Site type not found.");
                await OnGetAsync();
                return Page();
            }

            // Check for overlapping reservations
            var overlappingReservations = _unitOfWork.Reservation.GetAll(
                predicate: r => r.SiteId == Reservation.SiteId &&
                                r.EndDate > Reservation.StartDate &&
                                r.StartDate < Reservation.EndDate &&
                                r.ReservationStatus != SD.CancelledReservation &&
                                r.ReservationStatus != SD.CompleteReservation
            );
            if (overlappingReservations.Any())
            {
                ModelState.AddModelError(string.Empty, "The selected site is already booked for these dates.");
                await OnGetAsync();
                return Page();
            }

            // Check trailer length
            if (Reservation.TrailerLength.HasValue && site.TrailerMaxSize.HasValue)
            {
                if (Reservation.TrailerLength > site.TrailerMaxSize)
                {
                    ModelState.AddModelError(string.Empty, $"Trailer length ({Reservation.TrailerLength} ft) exceeds site maximum ({site.TrailerMaxSize} ft).");
                    await OnGetAsync();
                    return Page();
                }
            }

            // Get the current price for this site type
            var currentPrice = _unitOfWork.Price.GetAll(
                p => p.SiteTypeId == site.SiteTypeId &&
                     p.StartDate <= Reservation.StartDate &&
                     (p.EndDate == null || p.EndDate >= Reservation.EndDate)
            ).OrderByDescending(p => p.StartDate).FirstOrDefault();

            // Default to base rate if no price is found
            decimal dailyRate = currentPrice?.PricePerDay ?? 50.0m;

            // Store the details in TempData
            TempData["ReservationData"] = System.Text.Json.JsonSerializer.Serialize(Reservation);
            TempData["SiteId"] = Reservation.SiteId;
            TempData["SiteName"] = site.Name;
            TempData["SiteType"] = siteType.Name;
            TempData["DailyRate"] = dailyRate.ToString();
            TempData["StartDate"] = Reservation.StartDate.ToString("yyyy-MM-dd");
            TempData["EndDate"] = Reservation.EndDate.ToString("yyyy-MM-dd");
            TempData["TrailerLength"] = Reservation.TrailerLength?.ToString() ?? "N/A";
            TempData["FromGuestFlow"] = "false";

            // Redirect to the Summary page
            return RedirectToPage("./Summary");
        }

        // Helper method to get current price for a site type
        private decimal GetCurrentPriceForSiteType(int siteTypeId)
        {
            var currentPrice = _unitOfWork.Price.GetAll(
                p => p.SiteTypeId == siteTypeId &&
                p.StartDate <= DateTime.Now &&
                (p.EndDate == null || p.EndDate >= DateTime.Now)
            ).OrderByDescending(p => p.StartDate).FirstOrDefault();

            return currentPrice?.PricePerDay ?? 50.0m; // Default fallback rate
        }
    }
}