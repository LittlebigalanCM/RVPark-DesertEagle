using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RVPark.Pages.Admin.Prices
{
    [Authorize(Roles = SD.AdminRole + "," + SD.StaffRole + "," + SD.CampHostRole)]
    public class IndexModel : PageModel
    {
        private readonly Infrastructure.Data.ApplicationDbContext _context;

        public IndexModel(Infrastructure.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<Price> Price { get;set; } = default!;

        public async Task OnGetAsync()
        {
            Price = (await _context.Price
                .Include(p => p.SiteType)
                .ToListAsync())
                .OrderBy(p => p.EndDate)
                .ToList();
        }
    }
}
