using ApplicationCore.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {

        }

        public DbSet<Transaction> Transaction { get; set; }
        public DbSet<Reservation> Reservation { get; set; }
        public DbSet<Site> Site { get; set; }
        public DbSet<UserAccount> UserAccount { get; set; }
        public DbSet<Price> Price { get; set; }
        public DbSet<Photo> Photo { get; set; }
        public DbSet<Document> Document { get; set; }
        public DbSet<SiteType> SiteType { get; set; }
        public DbSet<Fee> Fee { get; set; }
        public DbSet<MilitaryBranch> MilitaryBranch { get; set; }
        public DbSet<MilitaryRank> MilitaryRank { get; set; }
        public DbSet<MilitaryStatus> MilitaryStatus { get; set; }
        public DbSet<GSPayGrade> GSPayGrades { get; set; }

        public DbSet<CustomDynamicField> CustomDynamicField { get; set; }
        public DbSet<Check> Check { get; set; }

        public object Sites { get; set; }

        /// <summary>
        /// Configures the entity framework model for the context.
        /// </summary>
        /// <param name="modelBuilder">The <see cref="ModelBuilder"/> used to configure the model for the context.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Fee>(entity =>
            {
                entity.HasKey(e => e.FeeId);
                entity.Property(e => e.FeeId).HasColumnType("int").IsRequired();
                entity.Property(e => e.Name).HasColumnType("nvarchar(100)").IsRequired();
                entity.Property(e => e.DisplayLabel).HasColumnType("nvarchar(100)");
                entity.Property(e => e.TriggerType).HasColumnType("int").IsRequired();
                entity.Property(e => e.TriggerRuleJson).HasColumnType("nvarchar(max)");
                entity.Property(e => e.TriggerTemplateType).HasColumnType("nvarchar(100)");
                entity.Property(e => e.CalculationType).HasConversion<int?>().HasColumnType("int");
                entity.Property(e => e.StaticAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Percentage).HasColumnType("decimal(18,2)");
                entity.Property(e => e.IsEnabled).HasColumnType("bit");
            });
        }
    }
}
