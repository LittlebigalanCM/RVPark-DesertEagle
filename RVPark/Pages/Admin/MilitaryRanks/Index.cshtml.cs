using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RVPark.Pages.Admin.MilitaryRanks
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
            // The page will load data via AJAX
        }

        public IActionResult OnPostLockRank(int rankId)
        {
            var rank = _unitOfWork.MilitaryRank.Get(r => r.Id == rankId);
            if (rank != null)
            {
                rank.IsActive = false;
                _unitOfWork.MilitaryRank.Update(rank);
                _unitOfWork.Commit();
            }

            return RedirectToPage();
        }

        public IActionResult OnPostUnlockRank(int rankId)
        {
            var rank = _unitOfWork.MilitaryRank.Get(r => r.Id == rankId);
            if (rank != null)
            {
                rank.IsActive = true;
                _unitOfWork.MilitaryRank.Update(rank);
                _unitOfWork.Commit();
            }

            return RedirectToPage();
        }
    }
}
