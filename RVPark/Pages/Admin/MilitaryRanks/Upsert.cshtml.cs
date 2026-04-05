using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RVPark.Pages.Admin.MilitaryRanks
{
    [Authorize(Roles = SD.AdminRole + "," + SD.StaffRole + "," + SD.CampHostRole)]
    public class UpsertModel : PageModel
    {
        private readonly UnitOfWork _unitofWork;

        [BindProperty]
        public MilitaryRank? MilitaryRank { get; set; }

        public UpsertModel(UnitOfWork unitofWork)
        {
            _unitofWork = unitofWork;
        }

        public void OnGet(int rankId)
        {
            if (rankId == 0)
            {
                MilitaryRank = new MilitaryRank();
            }
            else
            {
                MilitaryRank = _unitofWork.MilitaryRank.Get(m => m.Id == rankId);
            }
        }

        public IActionResult OnPost()
        {
            if (MilitaryRank != null)
            {
                if (MilitaryRank.Id == 0)
                {
                    // New ranks are active by default
                    MilitaryRank.IsActive = true;
                    _unitofWork.MilitaryRank.Add(MilitaryRank);
                }
                else
                {
                    var rank = _unitofWork.MilitaryRank.Get(r => r.Id == MilitaryRank.Id);
                    
                    // Only update the name, preserve the IsActive state
                    rank.Rank = MilitaryRank.Rank;
                    _unitofWork.MilitaryRank.Update(rank);
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
