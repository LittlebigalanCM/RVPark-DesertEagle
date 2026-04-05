using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RVPark.Pages.Admin.Roles
{
    [Authorize(Roles = SD.AdminRole)]
    public class UpsertModel : PageModel
    {
        private readonly RoleManager<IdentityRole> _roleManager;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public UpsertModel(RoleManager<IdentityRole> roleManager)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
            _roleManager = roleManager;
        }

        [BindProperty]
        public IdentityRole CurrentRole { get; set; }
        [BindProperty]
        public bool IsUpdate { get; set; }

        public async Task OnGetAsync(string? id)
        {
            if (id != null)
            {
#pragma warning disable CS8601 // Possible null reference assignment.
                CurrentRole = await _roleManager.FindByIdAsync(id);
#pragma warning restore CS8601 // Possible null reference assignment.
                IsUpdate = true;
            }
            else
            {
                CurrentRole = new IdentityRole();
                IsUpdate = false;
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {

            if (!IsUpdate)
            {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                CurrentRole.NormalizedName = CurrentRole.Name.ToUpper();
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                await _roleManager.CreateAsync(CurrentRole);
                return RedirectToPage("./Index", new { success = true, message = "Successfully Added" });
            }
            else
            {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                CurrentRole.NormalizedName = CurrentRole.Name.ToUpper();
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                await _roleManager.UpdateAsync(CurrentRole);
                return RedirectToPage("./Index", new { success = true, message = "Update Successful" });
            }

        }
    }
}