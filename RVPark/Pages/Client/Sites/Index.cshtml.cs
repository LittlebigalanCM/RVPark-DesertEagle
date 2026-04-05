using System;
using System.Linq;
using Infrastructure.Data;
using ApplicationCore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RVPark.Pages.Client.Sites
{
    public class IndexModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;
        public IndexModel(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IEnumerable<SiteType> SiteTypes { get; set; }

        public void OnGet()
        {
            SiteTypes = _unitOfWork.SiteType
                .GetAll()
                .ToList();
        }


    }
}
