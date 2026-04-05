using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using MailKit.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MimeKit;

namespace RVPark.Pages.Admin.Users
{
    [Authorize(Roles = SD.AdminRole + "," + SD.StaffRole + "," + SD.CampHostRole)]
    public class IndexModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public IndexModel(UnitOfWork unitOfWork, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _unitOfWork = unitOfWork;
            _roleManager = roleManager;
        }

        public IEnumerable<SelectListItem>? RolesList { get; set; }
        public IEnumerable<SelectListItem>? BranchList { get; set; }
        public IEnumerable<SelectListItem>? RankList { get; set; }
        public IEnumerable<SelectListItem>? StatusList { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SelectedRole { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedBranch { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedRank { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedStatus { get; set; }

        public IEnumerable<SelectListItem>? SearchTypeList { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SelectedSearchType { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? UserSearchQuery { get; set; }
       
        [BindProperty(SupportsGet = true)]
        public bool? ResettingSearch { get; set; }

        public IEnumerable<UserAccount>? ApplicationUsers { get; set; }
        public Dictionary<string, List<string>>? UserRoles { get; set; }
        public bool Success { get; set; } = false;
        public string Message { get; set; } = "";

        public async Task OnGetAsync(bool success = false, string message = null)
        {
            InitializeDropdowns();

            Success = success;
            Message = message;
            ResettingSearch = false;

            var users = _unitOfWork.UserAccount.GetAll().ToList();

            UserRoles = new Dictionary<string, List<string>>();

            foreach (var user in users)
            {
                var userRole = await _userManager.GetRolesAsync(user);
                UserRoles.Add(user.Id, userRole.ToList());
            }

            ApplicationUsers = users;
        }

        public async Task<IActionResult> OnGetSearchAsync()
        {
            InitializeDropdowns();

            if (ResettingSearch == true)
            {
                ResettingSearch = false;
                return RedirectToPage("./Index");
            }

            ApplicationUsers = _unitOfWork.UserAccount.GetAll().ToList();
            UserRoles = new Dictionary<string, List<string>>();

            foreach(var user in ApplicationUsers)
            {
                var userRole = await _userManager.GetRolesAsync(user);
                UserRoles.Add(user.Id, userRole.ToList());
            }

            if (!string.IsNullOrWhiteSpace(SelectedRole))
            {
                ApplicationUsers = ApplicationUsers.Where(u => UserRoles.ContainsKey(u.Id) && UserRoles[u.Id].Contains(SelectedRole)).ToList();
            }

            if(SelectedBranch is > 0)
            {
                ApplicationUsers = ApplicationUsers.Where(u => u.BranchId == SelectedBranch).ToList();
            }

            if(SelectedRank is > 0)
            {
                ApplicationUsers = ApplicationUsers.Where(u => u.RankId == SelectedRank).ToList();
            }

            if(SelectedStatus is > 0)
            {
                ApplicationUsers = ApplicationUsers.Where(u => u.StatusId == SelectedStatus).ToList();
            }

            if (!string.IsNullOrWhiteSpace(UserSearchQuery))
            {
                var query = UserSearchQuery.Trim().ToLower();

                ApplicationUsers = SelectedSearchType switch
                {
                    "Name" => ApplicationUsers.Where(u => u.FullName.ToLower().Contains(query)).ToList(),
                    "Email" => ApplicationUsers.Where(u => u.Email != null && u.Email.ToLower().Contains(query)).ToList(),
                    "Phone Number" => ApplicationUsers.Where(u => u.PhoneNumber != null && u.PhoneNumber.ToLower().Contains(query)).ToList(),
                    _ => ApplicationUsers.Where(u => u.FullName.ToLower().Contains(query)
                                    || (u.Email != null && u.Email.ToLower().Contains(query))
                                    || (u.PhoneNumber != null && u.PhoneNumber.ToLower().Contains(query))).ToList()
                    .ToList()
                };
            }

            ResettingSearch = false;
            return Page();
        }


        public async Task<IActionResult> OnPostLockUnlock(string id)
        {
            var user = _unitOfWork.UserAccount.Get(u => u.Id == id);
            if (user.LockoutEnd == null) // unlocked
            {
                user.LockoutEnd = DateTime.UtcNow.AddYears(100);
                user.LockoutEnabled = true;
            }

            else if (user.LockoutEnd > DateTime.UtcNow)//unlock 
            {
                user.LockoutEnd = DateTime.UtcNow;
                user.LockoutEnabled = false;
            }

            else
            {
                user.LockoutEnd = DateTime.UtcNow.AddYears(100);
                user.LockoutEnabled = true;
            }
            _unitOfWork.UserAccount.Update(user);
            await _unitOfWork.CommitAsync();
            return RedirectToPage();
        }

        private void InitializeDropdowns()
        {
            var roles = _roleManager.Roles.ToList();
            var branches = _unitOfWork.MilitaryBranch.GetAll().ToList();
            var ranks = _unitOfWork.MilitaryRank.GetAll().ToList();
            var statuses = _unitOfWork.MilitaryStatus.GetAll().ToList();

            RolesList = roles.Select(r => new SelectListItem { Value = r.Name, Text = r.Name });
            BranchList = branches.Select(b => new SelectListItem { Value = b.Id.ToString(), Text = b.BranchName });
            RankList = ranks.Select(r => new SelectListItem { Value = r.Id.ToString(), Text = r.Rank });
            StatusList = statuses.Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Status });

            SearchTypeList = new[]
            {
                new SelectListItem { Value = "Name", Text = "Name" },
                new SelectListItem("Email", "Email"),
                new SelectListItem("Phone Number", "Phone Number"),
                new SelectListItem("All", "All")
            };
        }
        //This is to fix an error where the text disappeared after searching
        public async Task<IActionResult> OnPostSearchAsync()
        {
            InitializeDropdowns();

            if (ResettingSearch == true)
            {
                ResettingSearch = false;
                return RedirectToPage("./Index");
            }

            ApplicationUsers = _unitOfWork.UserAccount.GetAll().ToList();
            UserRoles = new Dictionary<string, List<string>>();

            foreach (var user in ApplicationUsers)
            {
                var userRole = await _userManager.GetRolesAsync(user);
                UserRoles.Add(user.Id, userRole.ToList());
            }

            if (!string.IsNullOrWhiteSpace(SelectedRole))
            {
                ApplicationUsers = ApplicationUsers.Where(u => UserRoles.ContainsKey(u.Id) && UserRoles[u.Id].Contains(SelectedRole)).ToList();
            }

            if (SelectedBranch is > 0)
            {
                ApplicationUsers = ApplicationUsers.Where(u => u.BranchId == SelectedBranch).ToList();
            }

            if (SelectedRank is > 0)
            {
                ApplicationUsers = ApplicationUsers.Where(u => u.RankId == SelectedRank).ToList();
            }

            if (SelectedStatus is > 0)
            {
                ApplicationUsers = ApplicationUsers.Where(u => u.StatusId == SelectedStatus).ToList();
            }

            if (!string.IsNullOrWhiteSpace(UserSearchQuery))
            {
                var query = UserSearchQuery.Trim().ToLower();

                ApplicationUsers = SelectedSearchType switch
                {
                    "Name" => ApplicationUsers.Where(u => u.FullName.ToLower().Contains(query)).ToList(),
                    "Email" => ApplicationUsers.Where(u => u.Email != null && u.Email.ToLower().Contains(query)).ToList(),
                    "Phone Number" => ApplicationUsers.Where(u => u.PhoneNumber != null && u.PhoneNumber.ToLower().Contains(query)).ToList(),
                    _ => ApplicationUsers.Where(u =>
                            u.FullName.ToLower().Contains(query)
                            || (u.Email != null && u.Email.ToLower().Contains(query))
                            || (u.PhoneNumber != null && u.PhoneNumber.ToLower().Contains(query))
                        ).ToList()
                };
            }

            ResettingSearch = false;
            return Page();
        }
    }
}
