using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RVPark.Pages.Admin.SiteTypes
{
    [Authorize(Roles = SD.AdminRole)]
    public class CreateModel : PageModel
    {
        private readonly Infrastructure.Data.ApplicationDbContext _context;

        public CreateModel(Infrastructure.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            return Page();
        }

        [BindProperty]
        public ApplicationCore.Models.SiteType SiteType { get; set; } = default!;

        [BindProperty]
        public decimal PricePerDay { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            Console.WriteLine("OnPostAsyncCreate");

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Add SiteType
            _context.SiteType.Add(SiteType);
            await _context.SaveChangesAsync();

            Console.WriteLine("Before Price creation");
            var price = new ApplicationCore.Models.Price
            {
                SiteTypeId = SiteType.SiteTypeId,
                PricePerDay = PricePerDay,
                StartDate = DateTime.Now,
                EndDate = null
            };

            _context.Price.Add(price);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }

    }
}