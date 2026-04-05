using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RVPark.Pages.Admin.Roles
{
    [Authorize(Roles = SD.AdminRole)]
    public class IndexModel : PageModel
    {
        private readonly RoleManager<IdentityRole> _roleManager;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public IndexModel(RoleManager<IdentityRole> roleManager)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
            _roleManager = roleManager;
        }

        public IEnumerable<IdentityRole> RolesObj { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        public async Task OnGetAsync(bool success = false, string message = null)
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            Success = success;
            Message = message;
            

        }
    }

}