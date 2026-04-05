using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RVPark.Pages.Admin.Fees
{
    [Authorize(Roles = SD.AdminRole + "," + SD.StaffRole + "," + SD.CampHostRole)]
    public class IndexModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
