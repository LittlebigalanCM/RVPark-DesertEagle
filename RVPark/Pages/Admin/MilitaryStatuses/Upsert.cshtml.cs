using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RVPark.Pages.Admin.MilitaryStatuses
{
    [Authorize(Roles = SD.AdminRole + "," + SD.StaffRole + "," + SD.CampHostRole)]

    public class UpsertModel : PageModel
    {
        private readonly UnitOfWork _unitofWork;

        [BindProperty]
        public MilitaryStatus? MilitaryStatus { get; set; }

        public UpsertModel(UnitOfWork unitofWork)
        {
            _unitofWork = unitofWork;
        }

        public void OnGet(int statusId)
        {
            if (statusId == 0)
            {
                MilitaryStatus = new MilitaryStatus();
            }
            else
            {
                MilitaryStatus = _unitofWork.MilitaryStatus.Get(m => m.Id == statusId);
            }

        }

        public IActionResult OnPost()
        {
            if (MilitaryStatus != null)
            {
                if (MilitaryStatus.Id == 0)
                {

                    _unitofWork.MilitaryStatus.Add(MilitaryStatus);

                }
                else
                {
                    var status = _unitofWork.MilitaryStatus.Get(s => s.Id == MilitaryStatus.Id);
                    status.Status = MilitaryStatus.Status;
                    _unitofWork.MilitaryStatus.Update(status);

                }
                _unitofWork.Commit();

                return RedirectToPage("Index");

            }
            else
            {
                return Page();
            }
        }
    }
}
