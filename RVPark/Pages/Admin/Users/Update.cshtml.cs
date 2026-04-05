using ApplicationCore.Interfaces;
using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RVPark.Pages.Admin.Users
{
    [Authorize(Roles = SD.AdminRole)]
    public class UpdateModel : PageModel
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        private readonly UnitOfWork _unitOfWork;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        private readonly UserManager<IdentityUser> _userManager;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        private readonly RoleManager<IdentityRole> _roleManager;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public UpdateModel(UnitOfWork unitOfWork, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
            _unitOfWork = unitOfWork;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [BindProperty]
        public UserAccount AppUser { get; set; }
        public List<string> UsersRoles { get; set; }
        public List<string> AllRoles { get; set; }
        public List<string> OldRoles { get; set; }
        [BindProperty]
        public string SelectedUserRole { get; set; }

        public IEnumerable<SelectListItem> MilitaryBranchesList { get; set; }
        public IEnumerable<SelectListItem> MilitaryRanksList { get; set; }
        public IEnumerable<SelectListItem> MilitaryStatusesList { get; set; }
        public IEnumerable<SelectListItem> UserRoleList { get; set; }
        [BindProperty]
        public int? SelectedBranchId { get; set; }
        [BindProperty]
        public int? SelectedRankId { get; set; }
        [BindProperty]
        public int SelectedStatusId { get; set; }


        public async Task OnGet(string id)
        {
            AppUser = _unitOfWork.UserAccount.Get(u => u.Id == id);
            var roles = await _userManager.GetRolesAsync(AppUser);
            UsersRoles = roles.ToList();
            OldRoles = roles.ToList();
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
            AllRoles = _roleManager.Roles.Select(r => r.Name).ToList();
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.

            SelectedBranchId = AppUser.BranchId;
            SelectedRankId = AppUser.RankId;
            SelectedStatusId = AppUser.StatusId;
            


            var branches = _unitOfWork.MilitaryBranch.GetAll().ToList();
            var ranks = _unitOfWork.MilitaryRank.GetAll().ToList();
            var statuses = _unitOfWork.MilitaryStatus.GetAll().ToList();
            
            MilitaryBranchesList = branches.Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.BranchName });
            MilitaryRanksList = ranks.Select(r => new SelectListItem { Value = r.Id.ToString(), Text = r.Rank });
            MilitaryStatusesList = statuses.Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Status });
            UserRoleList = _roleManager.Roles
                .OrderBy(r => r.Name)
                .Select(r => new SelectListItem
                {
                    Value = r.Name,
                    Text = r.Name
                });
            SelectedUserRole = UsersRoles.FirstOrDefault();

        }
        public async Task<IActionResult> OnPostAsync()
        {
           
            var user = _unitOfWork.UserAccount.Get(u => u.Id == AppUser.Id);

            user.FirstName = AppUser.FirstName;
            user.LastName = AppUser.LastName;
            user.Email = AppUser.Email;
            user.PhoneNumber = AppUser.PhoneNumber;
            user.BranchId = SelectedBranchId;
            user.RankId = SelectedRankId;
            user.StatusId = SelectedStatusId;
            //user.PasswordHash = !string.IsNullOrEmpty(AppUser.PasswordHash)
            //    ? _userManager.PasswordHasher.HashPassword(user, AppUser.PasswordHash)
            //    : user.PasswordHash;
            
            _unitOfWork.UserAccount.Update(user);
            _unitOfWork.Commit();

            // Get current role(s)
            var oldRoles = await _userManager.GetRolesAsync(user);

            // Remove all old roles
            if (oldRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, oldRoles);
            }

            // Add the newly selected role
            if (!string.IsNullOrEmpty(SelectedUserRole))
            {
                await _userManager.AddToRoleAsync(user, SelectedUserRole);
            }


            var branches = _unitOfWork.MilitaryBranch.GetAll().ToList();
            var ranks = _unitOfWork.MilitaryRank.GetAll().ToList();
            var statuses = _unitOfWork.MilitaryStatus.GetAll().ToList();

            MilitaryBranchesList = branches.Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.BranchName });
            MilitaryRanksList = ranks.Select(r => new SelectListItem { Value = r.Id.ToString(), Text = r.Rank });
            MilitaryStatusesList = statuses.Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Status });

           
            return RedirectToPage("./Index", new { success = true, message = "Update Successful" });
        }
    }
}
