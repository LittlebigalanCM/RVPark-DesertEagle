using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RVPark.Pages.Admin.MilitaryBranches
{
    [Authorize(Roles = SD.AdminRole + "," + SD.StaffRole + "," + SD.CampHostRole)]
    public class IndexModel : PageModel
    {

        private readonly UnitOfWork _unitOfWork;

        public IndexModel(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public void OnGet()
        {
            
        }

        public IActionResult OnPostLockBranch(int branchId)
        {
            var branch = _unitOfWork.MilitaryBranch.Get(b => b.Id == branchId);
            if (branch != null)
            {
                branch.IsActive = false;
                _unitOfWork.MilitaryBranch.Update(branch);
                _unitOfWork.Commit();
            }
            return RedirectToPage("./Index");
        }

        public IActionResult OnPostUnlockBranch(int branchId)
        {
            var branch = _unitOfWork.MilitaryBranch.Get(b => b.Id == branchId);
            if (branch != null)
            {
                branch.IsActive = true;
                _unitOfWork.MilitaryBranch.Update(branch);
                _unitOfWork.Commit();
            }
            return RedirectToPage("./Index");
        }
    }
}
