using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RVPark.Pages.Admin.MilitaryBranches
{
    [Authorize(Roles = SD.AdminRole + "," + SD.StaffRole + "," + SD.CampHostRole)]
    public class UpsertModel : PageModel
    {
        private readonly UnitOfWork _unitofWork;

        [BindProperty]
        public MilitaryBranch? MilitaryBranch { get; set; }

        public UpsertModel(UnitOfWork unitofWork)
        {
            _unitofWork = unitofWork;
        }

        public void OnGet(int branchId)
        {
            if (branchId == 0)
            {
                MilitaryBranch = new MilitaryBranch();
            }
            else
            {
                MilitaryBranch = _unitofWork.MilitaryBranch.Get(m => m.Id == branchId);
            }

        }

        public IActionResult OnPost()
        {
            if (MilitaryBranch != null)
            {
                if (MilitaryBranch.Id == 0)
                {
                    // Default new branches to active
                    MilitaryBranch.IsActive = true;
                    _unitofWork.MilitaryBranch.Add(MilitaryBranch);
                }
                else
                {
                    var branch = _unitofWork.MilitaryBranch.Get(b => b.Id == MilitaryBranch.Id);
                    branch.BranchName = MilitaryBranch.BranchName;
                    _unitofWork.MilitaryBranch.Update(branch);
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
