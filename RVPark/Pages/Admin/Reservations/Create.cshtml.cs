using ApplicationCore.Interfaces;
using ApplicationCore.Models;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Infrastructure.Utilities;
using NuGet.Packaging;
using System.ComponentModel.DataAnnotations;
using Microsoft.CodeAnalysis.Operations;
using static Org.BouncyCastle.Asn1.Cmp.Challenge;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Infrastructure.Services;
using System.Text.Json;

namespace RVPark.Pages.Admin.Reservations
{
    [Authorize(Roles = SD.AdminRole + "," + SD.StaffRole + "," + SD.CampHostRole)]
    public class CreateModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly IEmailSender _emailSender;

        public CreateModel(UnitOfWork unitOfWork, UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager, IEmailSender emailSender)
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
        }

        [BindProperty]
        public ApplicationCore.Models.Reservation Reservation { get; set; }

        [BindProperty]
        public int NumberOfVehicles { get; set; }

        [BindProperty]
        public string? VehiclePlates { get; set; }

        [BindProperty]
        public string? VehiclePlate1 { get; set; }

        [BindProperty]
        public string? VehiclePlate2 { get; set; }

        [BindProperty]
        public string? VehiclePlate3 { get; set; }

        [BindProperty]
        public string? FirstName { get; set; }

        [BindProperty]
        public string? LastName { get; set; }

        [BindProperty]
        public string? Email { get; set; }

        [BindProperty]
        public bool IsEmailTaken { get; set; }

        [BindProperty]
        public string? PhoneNumber { get; set; }

        [BindProperty]
        public bool IsWalkinReservation { get; set; }

        [BindProperty]
        public bool SendingTempPassword { get; set; }

        public bool RequiresPCS { get; set; }

        [BindProperty]
        [StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string? Password { get; set; }

        [BindProperty]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password")]
        public string? ConfirmPassword { get; set; }

        [BindProperty]
        [DataType(DataType.Password)]
        public string? TempPassword { get; set; }

        public IEnumerable<SelectListItem> UserList { get; set; }
        public IEnumerable<SelectListItem> SiteList { get; set; }
        public IEnumerable<ApplicationCore.Models.SiteType> SiteTypes { get; set; }

        public IEnumerable<SelectListItem> MilitaryBranchesList { get; set; }
        public IEnumerable<SelectListItem> MilitaryRanksList { get; set; }
        public IEnumerable<SelectListItem> MilitaryStatusesList { get; set; }

        [BindProperty]
        public int? SelectedBranchId { get; set; } = 0;
        [BindProperty]
        public int? SelectedRankId { get; set; } = 0;
        [BindProperty]
        public int? SelectedStatusId { get; set; } = 0;

        public Dictionary<int, IEnumerable<Price>> SiteTypePrices { get; set; }
        public async Task OnGetAsync()
        {
            // Initialize default reservation values
            Reservation = new ApplicationCore.Models.Reservation
            {
                StartDate = DateTime.UtcNow.Date.AddDays(1),
                EndDate = DateTime.UtcNow.Date.AddDays(8),
                ReservationStatus = SD.UpcomingReservation
            };

            // Load military branches and statuses
            var branches = _unitOfWork.MilitaryBranch.GetAll().Where(b => b.IsActive).ToList();
            var statuses = _unitOfWork.MilitaryStatus.GetAll().ToList();

            MilitaryBranchesList = branches.Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.BranchName });
            MilitaryRanksList = new List<SelectListItem>(); // Empty until branch is selected
            MilitaryStatusesList = statuses.Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Status });

            // Get all unlocked users
            var users = _unitOfWork.UserAccount.GetAll()
              .Where(u => u.LockoutEnd == null || u.LockoutEnd <= DateTime.UtcNow);

            var clients = new List<UserAccount>();

            // Filter users that belong to the client role
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                if (roles.Contains(SD.ClientRole))
                {
                    clients.Add(u);
                }
            }

            // Build user dropdown list, starting with walk-in option
            var selected = new List<SelectListItem>();
            selected.Add(new SelectListItem { Value = "New Customer", Text = "New Customer - Walk In" });

            var selectedClients = clients.Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = $"{u.FirstName} {u.LastName} ({u.Email})"
            });

            selected.AddRange(selectedClients);
            UserList = selected;

            // Build Site dropdown
            var sites = _unitOfWork.Site.GetAll();
            SiteList = sites.Select(s => new SelectListItem
            {
                Value = s.SiteId.ToString(),
                Text = !string.IsNullOrEmpty(s.Name) ? s.Name : $"Site #{s.SiteId}"
            });

            // Load site types
            SiteTypes = _unitOfWork.SiteType.GetAll();

            // Load full price history for each site type
            SiteTypePrices = new Dictionary<int, IEnumerable<Price>>();
            foreach (var st in SiteTypes)
            {
                var prices = _unitOfWork.Price.GetAll(
                    p => p.SiteTypeId == st.SiteTypeId
                ).OrderByDescending(p => p.StartDate);
                SiteTypePrices[st.SiteTypeId] = prices;
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // If walk-in, verify email uniqueness
            if (IsWalkinReservation == true)
            {
                var emailCheck = _unitOfWork.UserAccount.Get(u => u.Email == Email);
                if (emailCheck != null)
                {
                    IsEmailTaken = true;
                    ModelState.AddModelError(string.Empty, "The email is already taken.");
                }
            }

            // If validation fails, re-load dropdown data and redisplay page
            if (!ModelState.IsValid || IsEmailTaken)
            {
                var branches = _unitOfWork.MilitaryBranch.GetAll().Where(b => b.IsActive).ToList();
                var statuses = _unitOfWork.MilitaryStatus.GetAll().ToList();

                MilitaryBranchesList = branches.Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.BranchName });
                MilitaryRanksList = new List<SelectListItem>();
                MilitaryStatusesList = statuses.Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Status });

                // Reload client list
                var users = _unitOfWork.UserAccount.GetAll();
                var clients = new List<UserAccount>();
                foreach (var u in users)
                {
                    var roles = await _userManager.GetRolesAsync(u);
                    if (roles.Contains(SD.ClientRole))
                    {
                        clients.Add(u);
                    }
                }

                var selected = new List<SelectListItem> { new SelectListItem { Value = "New Customer", Text = "New Customer - Walk In" } };
                var selectedClients = clients.Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = $"{u.FirstName} {u.LastName} ({u.Email})"
                });
                selected.AddRange(selectedClients);
                UserList = selected;

                // Reload site list
                var sites = _unitOfWork.Site.GetAll();
                SiteList = sites.Select(s => new SelectListItem
                {
                    Value = s.SiteId.ToString(),
                    Text = s.Name
                });

                // Reload site types & pricing
                SiteTypes = _unitOfWork.SiteType.GetAll();

                SiteTypePrices = new Dictionary<int, IEnumerable<Price>>();
                foreach (var st in SiteTypes)
                {
                    var prices = _unitOfWork.Price.GetAll(
                        p => p.SiteTypeId == st.SiteTypeId
                    ).OrderByDescending(p => p.StartDate);
                    SiteTypePrices[st.SiteTypeId] = prices;
                }

                return Page();
            }

            // Create new walk-in user if needed
            string userId = null;
            if (IsWalkinReservation)
            {
                var user = new UserAccount
                {
                    UserName = Email,
                    Email = Email,
                    FirstName = FirstName,
                    LastName = LastName,
                    PhoneNumber = PhoneNumber,
                    BranchId = SelectedBranchId ?? null,
                    RankId = SelectedRankId ?? null,
                    StatusId = SelectedStatusId ?? 0
                };

                // Optionally send temp password
                if (SendingTempPassword == true)
                {
                    if (TempPassword != null)
                    {
                        Password = TempPassword;
                        await _emailSender.SendTempPasswordAsync(Email, TempPassword);
                    }
                }

                // Create user in Identity
                var createResult = await _userManager.CreateAsync(user, Password);
                if (!createResult.Succeeded)
                {
                    foreach (var error in createResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    await OnGetAsync();
                    return Page();
                }

                // Assign Client role
                var roleResult = await _userManager.AddToRoleAsync(user, SD.ClientRole);
                if (!roleResult.Succeeded)
                {
                    foreach (var error in roleResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    await OnGetAsync();
                    return Page();
                }

                userId = user.Id;
            }
            else
            {
                // Existing user selected from dropdown
                userId = Reservation.UserId;
                var user = _unitOfWork.UserAccount.Get(u => u.Id == Reservation.UserId);
                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "Selected user does not exist.");
                    await OnGetAsync();
                    return Page();
                }
            }

            // Assign user to reservation
            Reservation.UserId = userId;

            // Build multiline vehicle plate string
            var plates = new List<string>();
            if (!string.IsNullOrWhiteSpace(VehiclePlate1)) plates.Add(VehiclePlate1.Trim());
            if (!string.IsNullOrWhiteSpace(VehiclePlate2)) plates.Add(VehiclePlate2.Trim());
            if (!string.IsNullOrWhiteSpace(VehiclePlate3)) plates.Add(VehiclePlate3.Trim());
            VehiclePlates = string.Join(Environment.NewLine, plates);

            Reservation.NumberOfVehicles = NumberOfVehicles;
            Reservation.VehiclePlates = VehiclePlates;

            // Validate site and site type
            var site = _unitOfWork.Site.Get(s => s.SiteId == Reservation.SiteId);
            if (site == null)
            {
                ModelState.AddModelError(string.Empty, "Selected site does not exist.");
                await OnGetAsync();
                return Page();
            }

            var siteType = _unitOfWork.SiteType.Get(st => st.SiteTypeId == site.SiteTypeId);
            if (siteType == null)
            {
                ModelState.AddModelError(string.Empty, "Site type not found.");
                await OnGetAsync();
                return Page();
            }

            // Check date overlap with existing reservations
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

            // Validate trailer size against site limit
            if (Reservation.TrailerLength.HasValue && site.TrailerMaxSize.HasValue)
            {
                if (Reservation.TrailerLength > site.TrailerMaxSize)
                {
                    ModelState.AddModelError(string.Empty, $"Trailer length ({Reservation.TrailerLength} ft) exceeds site maximum ({site.TrailerMaxSize} ft).");
                    await OnGetAsync();
                    return Page();
                }
            }

            // Determine reservation status based on dates
            if (Reservation.StartDate > DateTime.Now)
            {
                Reservation.ReservationStatus = SD.UpcomingReservation;
            }
            else if (Reservation.StartDate <= DateTime.Now && Reservation.EndDate > DateTime.Now)
            {
                Reservation.ReservationStatus = SD.ActiveReservation;
            }
            else
            {
                Reservation.ReservationStatus = SD.CompleteReservation;
            }

            // Get selected site type again (used for display)
            var selectedSiteType = _unitOfWork.SiteType.Get(st => st.SiteTypeId == site.SiteTypeId);

            // Determine correct price based on date range
            var currentPrice = _unitOfWork.Price.GetAll(
            p => p.SiteTypeId == site.SiteTypeId &&
                     p.StartDate <= Reservation.StartDate &&
                     (p.EndDate == null || p.EndDate >= Reservation.EndDate)
            ).OrderByDescending(p => p.StartDate).FirstOrDefault();

            // PCS requirement: stays longer than 180 days
            if (Reservation.StartDate.AddDays(180) < Reservation.EndDate)
            {
                RequiresPCS = true;
            }
            else
            {
                RequiresPCS = false;
            }

            // Disability requirement based on site attributes
            if (site.IsHandicappedAccessible)
            {
                Reservation.RequiresDisability = true;
            }
            else
            {
                Reservation.RequiresDisability = false;
            }

            // Final fallback daily rate if no pricing found
            decimal dailyRate = currentPrice?.PricePerDay ?? 50.0m;

            // Store details in TempData for summary page
            TempData["ReservationData"] = System.Text.Json.JsonSerializer.Serialize(Reservation);
            TempData["SiteId"] = Reservation.SiteId;
            TempData["SiteName"] = site.Name;
            TempData["SiteType"] = selectedSiteType.Name;
            TempData["DailyRate"] = dailyRate.ToString();
            TempData["StartDate"] = Reservation.StartDate.ToString("yyyy-MM-dd");
            TempData["EndDate"] = Reservation.EndDate.ToString("yyyy-MM-dd");
            TempData["RequiresPCS"] = (Reservation.EndDate - Reservation.StartDate).TotalDays > 180 ? "true" : "false";
            TempData["TrailerLength"] = Reservation.TrailerLength?.ToString() ?? "N/A";
            TempData["RequiresDisability"] = Reservation.RequiresDisability;

            TempData["AdminCreated"] = "true";
            return RedirectToPage("/client/Reservations/Summary");
        }
    }
}
