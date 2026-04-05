using ApplicationCore.Enums;
using ApplicationCore.Interfaces;
using ApplicationCore.Models;
using Infrastructure.Data;
using Infrastructure.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Initializes the database with default data and configurations.
/// </summary>
public class DbInitializer : IDbInitializer
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UnitOfWork _unitOfWork;

    public DbInitializer(ApplicationDbContext db, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, UnitOfWork unitOfWork)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
        _unitOfWork = unitOfWork;
    }

    public void Initialize()
    {
        _db.Database.EnsureCreated();

        // Apply pending migrations
        try
        {
            if (_db.Database.GetPendingMigrations().Any())
            {
                _db.Database.Migrate();
            }
        }
        catch (Exception)
        {
            // Migration errors handled silently
        }

        // Seed reference/lookup data
        CreateMilitaryBranches();
        CreateMilitaryRanks();
        CreateMilitaryStatuses();
        CreateGSPayGrades();
        CreateRoles();
        CreateAdminAccount();

        // Seed site configuration
        CreateSiteTypes();
        CreateSites();
        SeedSitePhotos();
        CreatePrices();

        // Seed form configuration
        CreateCustomDynamicFields();
        CreateFees();
    }

    #region Reference/Lookup Data

    private void CreateMilitaryBranches()
    {
        if (!_db.MilitaryBranch.Any())
        {
            var branches = new List<MilitaryBranch>
            {
                new MilitaryBranch { BranchName = "Army",         IsActive = true },
                new MilitaryBranch { BranchName = "Navy",         IsActive = true },
                new MilitaryBranch { BranchName = "Marine Corps", IsActive = true },
                new MilitaryBranch { BranchName = "Coast Guard",  IsActive = true },
                new MilitaryBranch { BranchName = "Air Force",    IsActive = true },
                new MilitaryBranch { BranchName = "Space Force",  IsActive = true },
            };

            _db.MilitaryBranch.AddRange(branches);
            _db.SaveChanges();
        }
    }

    private void CreateMilitaryRanks()
    {
        if (!_db.Set<MilitaryRank>().Any())
        {
            var ranks = new List<MilitaryRank>
            {
                // Enlisted (E1-E9)
                new MilitaryRank { Rank = "E1", IsActive = true },
                new MilitaryRank { Rank = "E2", IsActive = true },
                new MilitaryRank { Rank = "E3", IsActive = true },
                new MilitaryRank { Rank = "E4", IsActive = true },
                new MilitaryRank { Rank = "E5", IsActive = true },
                new MilitaryRank { Rank = "E6", IsActive = true },
                new MilitaryRank { Rank = "E7", IsActive = true },
                new MilitaryRank { Rank = "E8", IsActive = true },
                new MilitaryRank { Rank = "E9", IsActive = true },
                // Officer (O1-O9)
                new MilitaryRank { Rank = "O1", IsActive = true },
                new MilitaryRank { Rank = "O2", IsActive = true },
                new MilitaryRank { Rank = "O3", IsActive = true },
                new MilitaryRank { Rank = "O4", IsActive = true },
                new MilitaryRank { Rank = "O5", IsActive = true },
                new MilitaryRank { Rank = "O6", IsActive = true },
                new MilitaryRank { Rank = "O7", IsActive = true },
                new MilitaryRank { Rank = "O8", IsActive = true },
                new MilitaryRank { Rank = "O9", IsActive = true },
            };

            _db.Set<MilitaryRank>().AddRange(ranks);
            _db.SaveChanges();
        }
    }

    private void CreateMilitaryStatuses()
    {
        if (!_db.Set<MilitaryStatus>().Any())
        {
            var statuses = new List<MilitaryStatus>
            {
                new MilitaryStatus { Status = "Active Duty",    IsActive = true },
                new MilitaryStatus { Status = "Reserves",       IsActive = true },
                new MilitaryStatus { Status = "National Guard", IsActive = true },
                new MilitaryStatus { Status = "Retired",        IsActive = true },
                new MilitaryStatus { Status = "Civilian",       IsActive = true }
            };

            _db.Set<MilitaryStatus>().AddRange(statuses);
            _db.SaveChanges();
        }
    }

    private void CreateGSPayGrades()
    {
        if (!_db.Set<GSPayGrade>().Any())
        {
            var grades = Enumerable.Range(1, 15).Select(i => new GSPayGrade
            {
                Code = $"GS-{i:D2}",
                IsActive = true
            }).ToList();

            _db.Set<GSPayGrade>().AddRange(grades);
            _db.SaveChanges();
        }
    }

    private void CreateRoles()
    {
        _roleManager.CreateAsync(new IdentityRole(SD.AdminRole)).GetAwaiter().GetResult();
        _roleManager.CreateAsync(new IdentityRole(SD.CampHostRole)).GetAwaiter().GetResult();
        _roleManager.CreateAsync(new IdentityRole(SD.StaffRole)).GetAwaiter().GetResult();
        _roleManager.CreateAsync(new IdentityRole(SD.ClientRole)).GetAwaiter().GetResult();
    }

    private void CreateAdminAccount()
    {
        if (!_db.UserAccount.Any())
        {
            var admin = new UserAccount
            {
                UserName = "admin@rvpark.com",
                Email = "admin@rvpark.com",
                FirstName = "Admin",
                LastName = "User",
                PhoneNumber = "555-123-4567",
                StatusId = _unitOfWork.MilitaryStatus.Get(s => s.Status == "Civilian").Id,
                EmailConfirmed = true
            };

            _userManager.CreateAsync(admin, "Admin123*").GetAwaiter().GetResult();
            _userManager.AddToRoleAsync(admin, SD.AdminRole).GetAwaiter().GetResult();
        }
    }

    #endregion

    #region Site Configuration

    private void CreateSiteTypes()
    {
        if (!_db.Set<SiteType>().Any())
        {
            var siteTypes = new List<SiteType>
            {
                new SiteType { Name = "Premium Pull-Through",     IsActive = true }, // Id = 1
                new SiteType { Name = "Premium Back-In",          IsActive = true }, // Id = 2
                new SiteType { Name = "Standard",                 IsActive = true }, // Id = 3
                new SiteType { Name = "Tent/Overflow",            IsActive = true }, // Id = 4
                new SiteType { Name = "Short Term Storage",       IsActive = true }, // Id = 5
                new SiteType { Name = "Wagon Wheel Pull-Through", IsActive = true }, // Id = 6
                new SiteType { Name = "Partial Hookup",           IsActive = true }, // Id = 7
            };

            _db.Set<SiteType>().AddRange(siteTypes);
            _db.SaveChanges();
        }
    }

    private void CreateSites()
    {
        if (!_db.Set<Site>().Any())
        {
            var sites = new List<Site>();

            // Premium Pull Through Sites (101-108) - 8 sites
            for (int i = 101; i <= 108; i++)
            {
                sites.Add(new Site
                {
                    Name = i.ToString(),
                    Description = $"Premium Pull Through site {i} with full hookups (max length 60')",
                    SiteTypeId = 1,
                    TrailerMaxSize = 60,
                    IsLocked = false,
                    IsHandicappedAccessible = false
                });
            }

            // Premium Back In Sites - Multiple ranges
            var backInRanges = new List<(int start, int end)>
            {
                (1, 4), (14, 18), (26, 30), (37, 39), (43, 43), (55, 55), (51, 51), (65, 68), (76, 79), (90, 93)
            };

            foreach (var range in backInRanges)
            {
                for (int i = range.start; i <= range.end; i++)
                {
                    sites.Add(new Site
                    {
                        Name = i.ToString(),
                        Description = $"Premium Back In site {i} with full hookups (max length 60')",
                        SiteTypeId = 2,
                        TrailerMaxSize = 60,
                        IsLocked = false,
                        IsHandicappedAccessible = false
                    });
                }
            }

            // Standard Sites (109-220)
            var handicappedNumbers = new HashSet<int> { 115, 120, 138, 178, 188, 189, 216 };
            for (int i = 109; i <= 220; i++)
            {
                sites.Add(new Site
                {
                    Name = i.ToString(),
                    Description = $"Standard site {i} with basic hookups (max length 45')",
                    SiteTypeId = 3,
                    TrailerMaxSize = 45,
                    IsLocked = false,
                    IsHandicappedAccessible = handicappedNumbers.Contains(i)
                });
            }

            // Tent/Overflow Sites (T1-T5)
            for (int i = 1; i <= 5; i++)
            {
                sites.Add(new Site
                {
                    Name = $"T{i}",
                    Description = $"Tent/Overflow site T{i}",
                    SiteTypeId = 4,
                    TrailerMaxSize = null,
                    IsLocked = false,
                    IsHandicappedAccessible = false
                });
            }

            // Short Term Storage Sites (STS1-STS50)
            for (int i = 1; i <= 50; i++)
            {
                sites.Add(new Site
                {
                    Name = $"STS{i}",
                    Description = $"Short Term Storage site STS{i} (max 60 days)",
                    SiteTypeId = 5,
                    TrailerMaxSize = 45,
                    IsLocked = false,
                    IsHandicappedAccessible = false
                });
            }

            // Wagon Wheel Pull-Through Sites - Multiple ranges
            var wagonWheelRanges = new List<(int start, int end)>
            {
                (5, 13), (19, 25), (31, 36), (40, 42), (44, 49), (52, 54), (56, 64), (69, 75), (80, 89), (94, 100)
            };

            foreach (var range in wagonWheelRanges)
            {
                for (int i = range.start; i <= range.end; i++)
                {
                    sites.Add(new Site
                    {
                        Name = i.ToString(),
                        Description = $"Wagon Wheel Pull-Through site {i} (Walk-Ins only, reservable by Admin staff not online)",
                        SiteTypeId = 6,
                        TrailerMaxSize = 60,
                        IsLocked = false,
                        IsHandicappedAccessible = false
                    });
                }
            }

            // Partial Hookup Sites (221-227)
            for (int i = 221; i <= 227; i++)
            {
                sites.Add(new Site
                {
                    Name = i.ToString(),
                    Description = $"Partial Hookup site {i}",
                    SiteTypeId = 7,
                    TrailerMaxSize = 45,
                    IsLocked = false,
                    IsHandicappedAccessible = false
                });
            }

            _db.Site.AddRange(sites);
            _db.SaveChanges();
        }
    }

    private void SeedSitePhotos()
    {
        if (_db.Set<Photo>().Any()) return;

        const string basePath = "/images/sitePhotos/";

        var rvPhotos = new[] { "rvpic1.jpg", "rvpic2.jpg", "rvpic3.jpg" };
        var tentPhoto = "Tent.png";
        var dryStoragePhoto = "DryStorage.png";

        var tentTypeId = 4;
        var dryStorageTypeId = 5;

        var sites = _db.Set<Site>().ToList();
        var toAdd = new List<Photo>();

        foreach (var site in sites)
        {
            if (_db.Set<Photo>().Any(p => p.SiteId == site.SiteId))
                continue;

            if (site.SiteTypeId == tentTypeId)
            {
                toAdd.Add(new Photo { SiteId = site.SiteId, Name = basePath + tentPhoto });
            }
            else if (site.SiteTypeId == dryStorageTypeId)
            {
                toAdd.Add(new Photo { SiteId = site.SiteId, Name = basePath + dryStoragePhoto });
            }
            else
            {
                foreach (var file in rvPhotos)
                {
                    toAdd.Add(new Photo { SiteId = site.SiteId, Name = basePath + file });
                }
            }
        }

        if (toAdd.Count > 0)
        {
            _db.Set<Photo>().AddRange(toAdd);
            _db.SaveChanges();
        }
    }

    private void CreatePrices()
    {
        if (!_db.Set<Price>().Any())
        {
            var prices = new List<Price>
            {
                new Price { SiteTypeId = 1, PricePerDay = 30, StartDate = new DateTime(2022, 10, 1), EndDate = null }, // Premium Pull-Through
                new Price { SiteTypeId = 2, PricePerDay = 27, StartDate = new DateTime(2022, 10, 1), EndDate = null }, // Premium Back-In
                new Price { SiteTypeId = 3, PricePerDay = 25, StartDate = new DateTime(2022, 10, 1), EndDate = null }, // Standard
                new Price { SiteTypeId = 4, PricePerDay = 9,  StartDate = new DateTime(2022, 10, 1), EndDate = null }, // Tent/Overflow
                new Price { SiteTypeId = 5, PricePerDay = 5,  StartDate = new DateTime(2022, 10, 1), EndDate = null }, // Short Term Storage
                new Price { SiteTypeId = 6, PricePerDay = 30, StartDate = new DateTime(2022, 10, 1), EndDate = null }, // Wagon Wheel Pull-Through
                new Price { SiteTypeId = 7, PricePerDay = 21, StartDate = new DateTime(2022, 10, 1), EndDate = null }  // Partial Hookup
            };

            _db.Set<Price>().AddRange(prices);
            _db.SaveChanges();
        }
    }

    #endregion

    #region Form Configuration

    private void CreateCustomDynamicFields()
    {
        if (!_db.Set<CustomDynamicField>().Any())
        {
            var fields = new List<CustomDynamicField>
            {
                new CustomDynamicField
                {
                    FieldName = "numberofadults",
                    DisplayLabel = "Number of Adults",
                    FieldType = DynamicFieldType.Number,
                    DisplayOrder = 1,
                    DefaultValue = 2,
                    MinValue = 1,
                    MaxValue = 6,
                    Note = "Maximum 6 adults per site.",
                    NoteTrigger = "AtMax",
                    IsEnabled = true,
                    IsDeleted = false,
                    ShowAgreeButtons = false,
                    DisablePayOnDisagree = false
                },
                new CustomDynamicField
                {
                    FieldName = "numberofpets",
                    DisplayLabel = "Number of Pets",
                    FieldType = DynamicFieldType.Number,
                    DisplayOrder = 2,
                    DefaultValue = 0,
                    MinValue = 0,
                    MaxValue = 2,
                    Note = "Pets are welcome with a maximum of 2 pets per site. All pets must be kept on a leash no longer than 6 feet when outside your RV or campsite. Pet owners are responsible for cleaning up after their pets immediately. Pets may not be left unattended outside at any time. Excessive barking or aggressive behavior may result in a request to remove the pet from the premises. Certain breed restrictions may apply - please contact the office for details.",
                    NoteTrigger = "OnFocus",
                    IsEnabled = true,
                    IsDeleted = false,
                    ShowAgreeButtons = true,
                    DisablePayOnDisagree = true,
                    NoteAgreeText = "I Agree",
                    NoteDisagreeText = "I Do Not Agree"
                },
                new CustomDynamicField
                {
                    FieldName = "rulesacknowledgement",
                    DisplayLabel = "Rules and Agreement",
                    FieldType = DynamicFieldType.Agreement,
                    DisplayOrder = 3,
                    Note = @"By making this reservation, I acknowledge and agree to the following terms and conditions:

CHECK-IN/CHECK-OUT: Check-in time is 2:00 PM and check-out time is 11:00 AM. Early check-in or late check-out may be available upon request and subject to availability.

QUIET HOURS: Quiet hours are observed from 10:00 PM to 7:00 AM. Please be respectful of your neighbors and keep noise to a minimum during these hours.

SPEED LIMIT: The campground speed limit is 5 MPH. Please drive slowly and watch for pedestrians and children.

CAMPFIRES: Campfires are permitted only in designated fire rings or grills. Fires must be attended at all times and fully extinguished before leaving your site or going to sleep. During burn bans, no open fires are permitted.

TRASH: Please dispose of all trash in the designated dumpsters. Do not leave trash outside your RV or tent as it attracts wildlife.

PARKING: Each site is allowed a maximum of 2 vehicles. Additional vehicles must be parked in the overflow parking area.

LIABILITY: The campground is not responsible for loss, theft, or damage to personal property. Guests camp at their own risk.

CANCELLATION POLICY: Cancellations made more than 72 hours before arrival will receive a full refund minus a processing fee. Cancellations within 72 hours of arrival are non-refundable.

I agree to abide by all campground rules and policies during my stay.",
                    NoteTrigger = "OnFocus",
                    IsEnabled = true,
                    IsDeleted = false,
                    ShowAgreeButtons = true,
                    DisablePayOnDisagree = true,
                    NoteAgreeText = "I Agree",
                    NoteDisagreeText = "I Decline"
                }
            };

            _db.Set<CustomDynamicField>().AddRange(fields);
            _db.SaveChanges();
        }
    }

    private void CreateFees()
    {
        if (!_db.Set<Fee>().Any())
        {
            var fees = new List<Fee>
            {
                // Manual Fees
                new Fee
                {
                    Name = "Late Fee",
                    DisplayLabel = "Late Fee",
                    TriggerType = TriggerType.Manual,
                    CalculationType = CalculationType.StaticAmount,
                    StaticAmount = 10m,
                    IsEnabled = true
                },
                new Fee
                {
                    Name = "Damage Fee",
                    DisplayLabel = "Damage Fee",
                    TriggerType = TriggerType.Manual,
                    CalculationType = CalculationType.StaticAmount,
                    StaticAmount = 50m,
                    IsEnabled = true
                },
                new Fee
                {
                    Name = "Refund",
                    DisplayLabel = "Refund",
                    TriggerType = TriggerType.Manual,
                    CalculationType = CalculationType.StaticAmount,
                    StaticAmount = 0m,
                    IsEnabled = true
                },

                // Automatic Fees - tied to dynamic fields
                new Fee
                {
                    Name = "Extra Adult Fee",
                    DisplayLabel = "Extra Adult",
                    TriggerType = TriggerType.Automatic,
                    CalculationType = CalculationType.PerUnit,
                    StaticAmount = 1m,
                    IsEnabled = true,
                    TriggerRuleJson = """
                    {
                      "Field": "numberofadults",
                      "FieldType": "Number",
                      "Operator": ">",
                      "Value": 4
                    }
                    """
                },
                new Fee
                {
                    Name = "Extra Pet Fee",
                    DisplayLabel = "Extra Pet",
                    TriggerType = TriggerType.Automatic,
                    CalculationType = CalculationType.PerUnit,
                    StaticAmount = 2m,
                    IsEnabled = true,
                    TriggerRuleJson = """
                    {
                      "Field": "numberofpets",
                      "FieldType": "Number",
                      "Operator": ">",
                      "Value": 1
                    }
                    """
                }
            };

            _db.Set<Fee>().AddRange(fees);
            _db.SaveChanges();
        }
    }

    #endregion
}