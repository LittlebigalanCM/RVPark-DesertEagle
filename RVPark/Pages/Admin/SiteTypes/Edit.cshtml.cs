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

namespace RVPark.Pages.Admin.SiteTypes
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
        public ApplicationCore.Models.SiteType SiteType { get; set; } = default!;

        [BindProperty]
        public decimal PricePerDay { get; set; }

        public decimal OriginalPricePerDay { get; set; }

        [BindProperty]
        public double? TrailerMaxSize { get; set; }


        [BindProperty]
        public ApplicationCore.Models.Price CurrentPrice { get; set; }

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var sitetype = await _context.SiteType.FirstOrDefaultAsync(m => m.SiteTypeId == id);
            if (sitetype == null)
            {
                return NotFound();
            }

            SiteType = sitetype;

            var lengths = await _context.Site
    .Where(s => s.SiteTypeId == SiteType.SiteTypeId && s.TrailerMaxSize.HasValue)
    .Select(s => s.TrailerMaxSize!.Value)
    .ToListAsync();

            if (lengths.Any())
            {
                // assume all same; use Max just in case
                TrailerMaxSize = lengths.Max();
            }
            else
            {
                TrailerMaxSize = null;
            }

            // Add logging to debug
            Console.WriteLine($"Looking for price for SiteType ID: {id}");

            // Get the current active price - make sure this query returns data
            CurrentPrice = await _context.Price
                .Where(p => p.SiteTypeId == id &&
                p.StartDate <= DateTime.Now &&
                (p.EndDate == null || p.EndDate >= DateTime.Now))
                .OrderByDescending(p => p.StartDate)
                .FirstOrDefaultAsync();

            // Log whether we found a price
            if (CurrentPrice != null)
            {
                Console.WriteLine($"Found price: {CurrentPrice.PricePerDay}");
                PricePerDay = CurrentPrice.PricePerDay;
            }
            else
            {
                Console.WriteLine("No price found for this site type");
                PricePerDay = 0;
            }

            return Page();
        }


        public async Task<IActionResult> OnPostAsync()
        {
            Console.WriteLine("Before Price Edit");

            try
            {
                // 1. First, update the SiteType alone
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    Console.WriteLine("Updated SiteType");

                    try
                    {
                        _context.Attach(SiteType).State = EntityState.Modified;
                        await _context.SaveChangesAsync();

                        // 2. Update all Sites for this SiteType with the new trailer length
                        var sites = await _context.Site
                            .Where(s => s.SiteTypeId == SiteType.SiteTypeId)
                            .ToListAsync();

                        if (TrailerMaxSize.HasValue)
                        {
                            foreach (var site in sites)
                            {
                                site.TrailerMaxSize = TrailerMaxSize.Value;
                            }
                        }
                        else
                        {
                            // If you want blank to mean "no trailers allowed", set them to null
                            foreach (var site in sites)
                            {
                                site.TrailerMaxSize = null;
                            }
                        }

                        await _context.SaveChangesAsync();

                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }

            

                /*
                // 2. Check if price has changed
                Console.WriteLine("Before PriceChange");

                bool priceChanged = false;
                if (CurrentPrice != null)
                {
                    priceChanged = CurrentPrice.PricePerDay != PricePerDay;
                }
                else
                {
                    // If no current price, create one if PricePerDay > 0
                    priceChanged = PricePerDay > 0;
                }

                if (priceChanged)
                {
                    Console.WriteLine("PriceChange == true");

                    // 3. If there's an existing price, mark it as ended in a separate transaction
                    if (CurrentPrice != null)
                    {
                        using (var transaction = await _context.Database.BeginTransactionAsync())
                        {
                            try
                            {
                                // Reload the entity from the database to ensure it's properly tracked
                                CurrentPrice = await _context.Price.FindAsync(CurrentPrice.PriceId);
                                if (CurrentPrice != null)
                                {
                                    CurrentPrice.EndDate = DateTime.Now;
                                    await _context.SaveChangesAsync();
                                    transaction.Commit();
                                }
                            }
                            catch
                            {
                                transaction.Rollback();
                                throw;
                            }
                        }
                    }

                    // 4. Create a new price record in a separate transaction
                    using (var transaction = await _context.Database.BeginTransactionAsync())
                    {
                        try
                        {
                            // Reload the SiteType to ensure it exists and is properly tracked
                            var freshSiteType = await _context.SiteType.FindAsync(SiteType.SiteTypeId);

                            if (freshSiteType != null)
                            {
                                var newPrice = new ApplicationCore.Models.Price
                                {
                                    SiteTypeId = freshSiteType.SiteTypeId,
                                    PricePerDay = PricePerDay,
                                    StartDate = DateTime.Now,
                                    EndDate = null
                                };

                                _context.Price.Add(newPrice);
                                await _context.SaveChangesAsync();
                                transaction.Commit();
                            }
                            else
                            {
                                throw new Exception("Could not find SiteType with ID: " + SiteType.SiteTypeId);
                            }
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
                */
                return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                // Log the error for debugging
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.InnerException?.Message);

                ModelState.AddModelError("", "An error occurred while saving changes: " + ex.Message);
                return Page();
            }
        }

        private bool SiteTypeExists(int id)
        {
            return _context.SiteType.Any(e => e.SiteTypeId == id);
        }
    }
}