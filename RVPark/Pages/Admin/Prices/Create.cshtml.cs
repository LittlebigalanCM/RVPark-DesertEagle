using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace RVPark.Pages.Admin.Prices
{
    [Authorize(Roles = SD.AdminRole)]
    public class CreateModel : PageModel
    {
        private readonly Infrastructure.Data.ApplicationDbContext _context;
        private readonly UnitOfWork _unitOfWork;
        
        public SiteType SiteType { get; set; }
        
        public Price LatestPrice { get; set; }

        public CreateModel(Infrastructure.Data.ApplicationDbContext context, UnitOfWork unitOfWork)
        {
            _context = context;
            _unitOfWork = unitOfWork;
        }

        public IActionResult OnGet(int id)
        {
            SiteType = _unitOfWork.SiteType.GetById(id);

            //Find latest price
            LatestPrice = _unitOfWork.Price.GetAll(
                p => p.SiteTypeId == SiteType.SiteTypeId && p.EndDate == null
            ).FirstOrDefault();

            return Page();
        }

        [BindProperty]
        public Price Price { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            //Re-Populate Latest Price
            LatestPrice = _unitOfWork.Price.GetAll(
                p => p.SiteTypeId == Price.SiteTypeId && p.EndDate == null
            ).FirstOrDefault();

            //If User provided start date is before that latest price start date
            if (LatestPrice != null && LatestPrice.StartDate > Price.StartDate)
            {
                return BadRequest();
            }

            //Makes sure there is a latest price
            if(LatestPrice != null)
            {
                //Updates latest price end date to today
                LatestPrice.EndDate = Price.StartDate.AddDays(-1);
                _unitOfWork.Price.Update(LatestPrice);
                _unitOfWork.Commit();
            }

            _context.Price.Add(Price);
            await _context.SaveChangesAsync();

            return RedirectToPage("../SiteTypes/Index");
        }
    }
}
