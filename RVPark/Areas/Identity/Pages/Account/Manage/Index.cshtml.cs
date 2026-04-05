// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using ApplicationCore.Models;

namespace RVPark.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UnitOfWork _unitOfWork;

        public IEnumerable<SelectListItem> MilitaryBranchesList { get; set; }
        public IEnumerable<SelectListItem> MilitaryRanksList { get; set; }
        public IEnumerable<SelectListItem> MilitaryStatusesList { get; set; }


        public IndexModel(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            UnitOfWork unitOfWork)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _unitOfWork = unitOfWork;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string StatusMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [Display(Name = "First Name")]            
            public string FirstName { get; set; }
            [Required]
            [Display(Name = "Last Name")]
            public string LastName { get; set; }
            [Phone]
            [Display(Name = "Phone number")]
            public string PhoneNumber { get; set; }
            //[Required]
            public int? SelectedBranchId { get; set; }
            //[Required]
            public int? SelectedRankId { get; set; }
            [Required]
            public int SelectedStatusId { get; set; }
            //public int? SelectedGSPayId { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var branches = _unitOfWork.MilitaryBranch.GetAll().ToList();
            var ranks = _unitOfWork.MilitaryRank.GetAll().ToList();
            var statuses = _unitOfWork.MilitaryStatus.GetAll().ToList();

            //Retrieves and populates the lists for dropdowns
            MilitaryBranchesList = branches.Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.BranchName });
            MilitaryRanksList = ranks.Select(r => new SelectListItem { Value = r.Id.ToString(), Text = r.Rank });
            MilitaryStatusesList = statuses.Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Status });

            // Get the current user as UserAccount
            var user = await _userManager.GetUserAsync(User) as UserAccount;
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Populate the Username and Input properties
            Username = user.UserName;
            Input = new InputModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                SelectedBranchId = user.BranchId,
                SelectedRankId = user.RankId,
                SelectedStatusId = user.StatusId,
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var branches = _unitOfWork.MilitaryBranch.GetAll().ToList();
            var ranks = _unitOfWork.MilitaryRank.GetAll().ToList();
            var statuses = _unitOfWork.MilitaryStatus.GetAll().ToList();

            MilitaryBranchesList = branches.Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.BranchName });
            MilitaryRanksList = ranks.Select(r => new SelectListItem { Value = r.Id.ToString(), Text = r.Rank });
            MilitaryStatusesList = statuses.Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Status });

            var user = await _userManager.GetUserAsync(User) as UserAccount;
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var civilian = statuses.First(s => s.Status == "Civilian");

            bool isCivilian = Input.SelectedStatusId == civilian.Id;

            if (!ModelState.IsValid) return Page();

            // Update user properties from Input
            user.FirstName = Input.FirstName;
            user.LastName = Input.LastName;
            user.PhoneNumber = Input.PhoneNumber;
            user.BranchId = Input.SelectedBranchId;
            user.RankId = Input.SelectedRankId;
            user.StatusId = Input.SelectedStatusId;

            // Save changes to the database
            _unitOfWork.UserAccount.Update(user);
            _unitOfWork.Commit();

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Your profile has been updated";
            return RedirectToPage();
        }
    }
}