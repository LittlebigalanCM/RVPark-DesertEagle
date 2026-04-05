using ApplicationCore.Interfaces;
using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MimeKit;
using System.Collections.Generic;
using System.Linq;

namespace RVPark.Pages.Admin.Sites
{
    [Authorize(Roles = SD.AdminRole + "," + SD.StaffRole + "," + SD.CampHostRole)]
    public class IndexModel : PageModel
    {
        private readonly UnitOfWork _unitOfWork;

        public IndexModel(UnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [BindProperty]
        public IList<Site> Site { get; set; } = default!;

        [BindProperty(SupportsGet = true)]
        public string SiteSearchQuery { get; set; }

        public IEnumerable<SelectListItem> SiteTypeList { get; set; }
        public IEnumerable<SelectListItem> SearchTypeList { get; set; }

        [BindProperty]
        public int SelectedSiteTypeId { get; set; }

        [BindProperty]
        public string SelectedSearchType { get; set; }

        [BindProperty]
        public bool ResettingSearch {  get; set; }



        public void OnGet()
        {
            Console.WriteLine("=================BASIC GET");
           
            Site = _unitOfWork.Site.GetAll(
                    predicate: null,
                    includes: "SiteType"
            ).ToList();
            var siteTypes = _unitOfWork.SiteType.GetAll();

            SiteTypeList = siteTypes.Select(st => new SelectListItem { Value = st.SiteTypeId.ToString(), Text = st.Name });

            var searchTypes = new List<string>();

            searchTypes.Add("Name");
            searchTypes.Add("Description");
            searchTypes.Add("All");
            SearchTypeList = searchTypes.Select(s => new SelectListItem { Value = s, Text = s });
            ResettingSearch = false;
        }

        public void OnPostSearch()
        {
            Console.WriteLine("=================SEARCH BY NAME: " + SelectedSearchType);
            Site = _unitOfWork.Site.GetAll(s => true, null, "SiteType").ToList();

            if(ResettingSearch == true)
            {
                var site_Types = _unitOfWork.SiteType.GetAll();

                SiteTypeList = site_Types.Select(st => new SelectListItem { Value = st.SiteTypeId.ToString(), Text = st.Name });

                var search_Types = new List<string>();

                search_Types.Add("Name");
                search_Types.Add("Description");
                search_Types.Add("All");
                SearchTypeList = search_Types.Select(s => new SelectListItem { Value = s, Text = s });
                ResettingSearch = false;
                RedirectToPage("Index");   
            }
            
            if (SiteSearchQuery != null)
            {
                if (SelectedSearchType != null && SelectedSearchType != "All")
                {

                    if (SelectedSearchType == "Name")
                    {
                        Site = Site.Where(s => s.Name.ToUpper().Contains(SiteSearchQuery.ToUpper())).ToList();
                    }
                    else if (SelectedSearchType == "Description")
                    {
                        Site = Site.Where(s => s.Description.ToUpper().Contains(SiteSearchQuery.ToUpper())).ToList();
                    }
                                      
                }
                else
                {
                    //search by both name and description
                    Site = Site.Where(s => s.Name.ToUpper().Contains(SiteSearchQuery.ToUpper()) ||
                                           s.Description.ToUpper().Contains(SiteSearchQuery.ToUpper())).ToList();
                }
            }
            
            
            if (SelectedSiteTypeId != 0)
            {
                Site = Site.Where(s => s.SiteTypeId == SelectedSiteTypeId).ToList();
            }


            var siteTypes = _unitOfWork.SiteType.GetAll();

            SiteTypeList = siteTypes.Select(st => new SelectListItem { Value = st.SiteTypeId.ToString(), Text = st.Name });

            var searchTypes = new List<string>();

            searchTypes.Add("Name");
            searchTypes.Add("Description");
            searchTypes.Add("All");
            SearchTypeList = searchTypes.Select(s => new SelectListItem { Value = s, Text = s });
        }

        public IActionResult OnPostLockSite(int siteId)
        {
            Console.WriteLine("===========Lock================");
            var site = _unitOfWork.Site.Get(s => s.SiteId == siteId);
            if (site != null)
            {
                site.IsLocked = true;
                _unitOfWork.Site.Update(site);
                _unitOfWork.Commit();
            }

            var siteTypes = _unitOfWork.SiteType.GetAll();

            SiteTypeList = siteTypes.Select(st => new SelectListItem { Value = st.SiteTypeId.ToString(), Text = st.Name });

            return RedirectToPage("./Index");   
        }

        public IActionResult OnPostUnlockSite(int siteId)
        {
            Console.WriteLine("===========Lock================");
            var site = _unitOfWork.Site.Get(s => s.SiteId == siteId);
            if (site != null)
            {
                site.IsLocked = false;
                _unitOfWork.Site.Update(site);
                _unitOfWork.Commit();
            }

            var siteTypes = _unitOfWork.SiteType.GetAll();

            SiteTypeList = siteTypes.Select(st => new SelectListItem { Value = st.SiteTypeId.ToString(), Text = st.Name });

            return RedirectToPage("./Index");
        }
    }
}

