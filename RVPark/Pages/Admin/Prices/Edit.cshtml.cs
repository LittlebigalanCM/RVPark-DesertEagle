using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RVPark.Pages.Admin.Prices
{
    [Authorize(Roles = SD.AdminRole)]
    public class EditModel : PageModel
    {
        private readonly Infrastructure.Data.ApplicationDbContext _context;

        public EditModel(Infrastructure.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Price Price { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var price =  await _context.Price.FirstOrDefaultAsync(m => m.PriceId == id);
            if (price == null)
            {
                return NotFound();
            }
            Price = price;
           ViewData["SelectedSiteTypeId"] = new SelectList(_context.SiteType, "SelectedSiteTypeId", "Name");
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more information, see https://aka.ms/RazorPagesCRUD.
      public async Task<IActionResult> OnPostAsync()
{
    // First validate the basic model
    if (!ModelState.IsValid)
    {
        ViewData["SelectedSiteTypeId"] = new SelectList(_context.SiteType, "SelectedSiteTypeId", "Name");
        return Page();
    }

    
            if (Price.StartDate < DateTime.Today)
            {
                    
                ModelState.AddModelError("Price.StartDate",
                    "Date must be current or in the future");
                ViewData["SelectedSiteTypeId"] = new SelectList(_context.SiteType, "SelectedSiteTypeId", "Name");
                return Page();
            }
            

    // Continue with saving if validation passes
    _context.Attach(Price).State = EntityState.Modified;
    try
    {
        await _context.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        if (!PriceExists(Price.PriceId))
        {
            return NotFound();
        }
        else
        {
            throw;
        }
    }
    return RedirectToPage("../SiteTypes/Index");
}
        private bool PriceExists(int id)
        {
            return _context.Price.Any(e => e.PriceId == id);
        }
    }
}
