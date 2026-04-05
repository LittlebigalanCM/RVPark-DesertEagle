// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Services;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;

namespace RVPark.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IUserStore<IdentityUser> _userStore;
        private readonly IUserEmailStore<IdentityUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UnitOfWork _unitOfWork;

        public IEnumerable<SelectListItem> MilitaryBranchesList { get; set; }
        public IEnumerable<SelectListItem> MilitaryRanksList { get; set; }
        public IEnumerable<SelectListItem> MilitaryStatusesList { get; set; }
        public IEnumerable<SelectListItem> UserRoleList {  get; set; }


        public RegisterModel(
            UserManager<IdentityUser> userManager,
            IUserStore<IdentityUser> userStore,
            SignInManager<IdentityUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            RoleManager<IdentityRole> roleManager,
            UnitOfWork unitOfWork)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _roleManager = roleManager;
            _unitOfWork = unitOfWork;
        }

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
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

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
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }
            [Required]
            public string FirstName { get; set; }
            [Required]
            public string LastName { get; set; }
            [Required]
            public string PhoneNumber { get; set; }
            public int? SelectedBranchId { get; set; }
            public int? SelectedRankId { get; set; }
            [Required]
            public int SelectedStatusId { get; set; }
            public string SelectedUserRole { get; set; }


            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            var branches = _unitOfWork.MilitaryBranch.GetAll().Where(b => b.IsActive).ToList();
            var ranks = _unitOfWork.MilitaryRank.GetAll().Where(r => r.IsActive).ToList();
            var statuses = _unitOfWork.MilitaryStatus.GetAll().ToList();

            MilitaryBranchesList = branches.Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.BranchName });

            MilitaryRanksList = ranks.Select(r => new SelectListItem { Value = r.Id.ToString(), Text = r.Rank });

            MilitaryStatusesList = statuses.Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Status });

            UserRoleList = new List<SelectListItem>
                {
                    new SelectListItem { Value = SD.AdminRole, Text = SD.AdminRole },
                    new SelectListItem { Value = SD.CampHostRole, Text = SD.CampHostRole },
                    new SelectListItem { Value = SD.ClientRole, Text = SD.ClientRole },
                    new SelectListItem { Value = SD.StaffRole, Text = SD.StaffRole }
                };

            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            var branches = _unitOfWork.MilitaryBranch.GetAll().Where(b => b.IsActive).ToList();
            var ranks = _unitOfWork.MilitaryRank.GetAll().Where(r => r.IsActive).ToList();
            var statuses = _unitOfWork.MilitaryStatus.GetAll().ToList();

            MilitaryBranchesList = branches.Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.BranchName });

            MilitaryRanksList = ranks.Select(r => new SelectListItem { Value = r.Id.ToString(), Text = r.Rank });

            MilitaryStatusesList = statuses.Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Status });

            UserRoleList = new List<SelectListItem>
                {
                    new SelectListItem { Value = SD.AdminRole, Text = SD.AdminRole },
                    new SelectListItem { Value = SD.CampHostRole, Text = SD.CampHostRole },
                    new SelectListItem { Value = SD.ClientRole, Text = SD.ClientRole },
                    new SelectListItem { Value = SD.StaffRole, Text = SD.StaffRole }
                };

            // Retrieve the role from the form
            string role = Input.SelectedUserRole;
            if (string.IsNullOrEmpty(role))
            {
                role = SD.ClientRole; // Make the default login Customer
            }

            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                // Expand identityUser with ApplicationUser properties
                var user = new UserAccount
                {
                    UserName = Input.Email,
                    Email = Input.Email,
                    FirstName = Input.FirstName,
                    LastName = Input.LastName,
                    PhoneNumber = Input.PhoneNumber,
                    BranchId = Input.SelectedBranchId,
                    RankId = Input.SelectedRankId,
                    StatusId = Input.SelectedStatusId,
                };

                var result = await _userManager.CreateAsync(user, Input.Password);
                if (result.Succeeded)
                {
                    // Assign role to the user
                    await AssignUserRoleAsync(user, role);
                    _logger.LogInformation("User created a new account with password.");

                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = user.Id, code, returnUrl },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailConfirmationAsync(Input.Email,HtmlEncoder.Default.Encode(callbackUrl));

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl });
                    }
                    else
                    {
                        if (!User.IsInRole(SD.AdminRole)){
                            await _signInManager.SignInAsync(user, isPersistent: false);
                            return LocalRedirect(returnUrl);
                        }
                        else
                        {
                            TempData["Success"] = "New user created successfully!";
                            return RedirectToPage("/Admin/Users/Index");
                        }
                    }
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }

        private async Task AssignUserRoleAsync(UserAccount user, string role)
        {
            switch (role)
            {
                case SD.CampHostRole:
                    await _userManager.AddToRoleAsync(user, SD.CampHostRole);
                    break;
                case SD.StaffRole:
                    await _userManager.AddToRoleAsync(user, SD.StaffRole);
                    break;
                case SD.AdminRole:
                    await _userManager.AddToRoleAsync(user, SD.AdminRole);
                    break;
                case SD.ClientRole:
                    await _userManager.AddToRoleAsync(user, SD.ClientRole);
                    break;
                default:
                    await _userManager.AddToRoleAsync(user, SD.ClientRole);
                    break;
            }
        }

        private IdentityUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<IdentityUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(IdentityUser)}'. " +
                    $"Ensure that '{nameof(IdentityUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<IdentityUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<IdentityUser>)_userStore;
        }
    }
}