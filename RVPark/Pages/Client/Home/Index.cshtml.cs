using ApplicationCore.Models;
using Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RVPark.Pages.Client.Home
{
    public class IndexModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;
        public IndexModel(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IEnumerable<SiteType> SiteTypes { get; set; }
        public Dictionary<int, IEnumerable<Price>> SiteTypePrices { get; set; }

        public void OnGet()
        {
            // Get all site types
            SiteTypes = _unitOfWork.SiteType.GetAll();

            // Get pricing for each site type
            SiteTypePrices = new Dictionary<int, IEnumerable<Price>>();
            foreach (var st in SiteTypes)
            {
                var prices = _unitOfWork.Price.GetAll(
                    p => p.SiteTypeId == st.SiteTypeId
                ).Where(p => p.StartDate <= DateTime.Now.Date.AddMonths(1))
                 .OrderBy(p => p.StartDate);
                SiteTypePrices[st.SiteTypeId] = prices;
            }
        }
    }
}
