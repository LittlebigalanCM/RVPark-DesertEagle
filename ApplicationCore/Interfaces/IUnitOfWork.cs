using ApplicationCore.Models;

namespace ApplicationCore.Interfaces
{
    /// <summary>
    /// Represents a unit of work that encapsulates a set of operations to be performed on a data source.
    /// </summary>
    public interface IUnitOfWork
    {
        public IGenericRepository<Transaction> Transaction { get; }
        public IGenericRepository<Reservation> Reservation { get; }
        public IGenericRepository<Site> Site { get; }
        public IGenericRepository<UserAccount> UserAccount { get; }
        public IGenericRepository<Price> Price { get; }
        public IGenericRepository<Photo> Photo { get; }
        public IGenericRepository<Document> Document { get; }
        public IGenericRepository<SiteType> SiteType { get; }
        public IGenericRepository<Fee> Fee { get; }
        public IGenericRepository<MilitaryBranch> MilitaryBranch { get; }
        public IGenericRepository<MilitaryRank> MilitaryRank { get; }
        public IGenericRepository<MilitaryStatus> MilitaryStatus { get; }
        public IGenericRepository<GSPayGrade> GSPayGrade { get; }

        public IGenericRepository<CustomDynamicField> CustomDynamicField { get; }
        public IGenericRepository<Check> Check { get; }



        //save changes to data source
        int Commit();

        Task<int> CommitAsync();
    }
}