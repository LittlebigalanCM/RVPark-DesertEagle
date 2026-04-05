using ApplicationCore.Interfaces;
using ApplicationCore.Models;
using Microsoft.AspNetCore.Identity;

namespace Infrastructure.Data
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _dbContext;

        public UnitOfWork(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public IGenericRepository<Transaction> _Transaction;
        public IGenericRepository<Fee> _Fee;
        public IGenericRepository<Photo> _Photo;
        public IGenericRepository<Document> _Document;
        public IGenericRepository<Price> _Price;
        public IGenericRepository<Reservation> _Reservation;
        public IGenericRepository<Site> _Site;
        public IGenericRepository<SiteType> _SiteType;
        public IGenericRepository<UserAccount> _UserAccount;
        public IGenericRepository<MilitaryBranch> _MilitaryBranch;
        public IGenericRepository<MilitaryRank> _MilitaryRank;
        public IGenericRepository<MilitaryStatus> _MilitaryStatus;
        public IGenericRepository<GSPayGrade> _GSPayGrade;
        public IGenericRepository<CustomDynamicField> _CustomDynamicField;
        public IGenericRepository<Check> _Check;


        public IGenericRepository<Transaction> Transaction
        {
            get
            {
                if (_Transaction == null)
                {
                    _Transaction = new GenericRepository<Transaction>(_dbContext);
                }
                return _Transaction;
            }
        }

        public IGenericRepository<Fee> Fee
        {
            get
            {
                if (_Fee == null)
                {
                    _Fee = new GenericRepository<Fee>(_dbContext);
                }
                return _Fee;
            }
        }

        public IGenericRepository<Photo> Photo
        {
            get
            {
                if (_Photo == null)
                {
                    _Photo = new GenericRepository<Photo>(_dbContext);
                }
                return _Photo;
            }
        }

        public IGenericRepository<Document> Document
        {
            get
            {
                if (_Document == null)
                {
                    _Document = new GenericRepository<Document>(_dbContext);
                }
                return _Document;
            }
        }

        public IGenericRepository<Price> Price
        {
            get
            {
                if (_Price == null)
                {
                    _Price = new GenericRepository<Price>(_dbContext);
                }
                return _Price;
            }
        }

        public IGenericRepository<Reservation> Reservation
        {
            get
            {
                if (_Reservation == null)
                {
                    _Reservation = new GenericRepository<Reservation>(_dbContext);
                }
                return _Reservation;
            }
        }

        public IGenericRepository<Site> Site
        {
            get
            {
                if (_Site == null)
                {
                    _Site = new GenericRepository<Site>(_dbContext);
                }
                return _Site;
            }
        }

        public IGenericRepository<SiteType> SiteType
        {
            get
            {
                if (_SiteType == null)
                {
                    _SiteType = new GenericRepository<SiteType>(_dbContext);
                }
                return _SiteType;
            }
        }

        public IGenericRepository<UserAccount> UserAccount
        {
            get
            {
                if (_UserAccount == null)
                {
                    _UserAccount = new GenericRepository<UserAccount>(_dbContext);
                }
                return _UserAccount;
            }
        }


        public IGenericRepository<MilitaryBranch> MilitaryBranch
        {
            get
            {
                if (_MilitaryBranch == null)
                {
                    _MilitaryBranch = new GenericRepository<MilitaryBranch>(_dbContext);
                }
                return _MilitaryBranch;
            }

        }

        public IGenericRepository<MilitaryRank> MilitaryRank
        {
            get
            {
                if (_MilitaryRank == null)
                {
                    _MilitaryRank = new GenericRepository<MilitaryRank>(_dbContext);
                }
                return _MilitaryRank;
            }
        }

        public IGenericRepository<MilitaryStatus> MilitaryStatus
        {
            get
            {
                if (_MilitaryStatus == null)
                {
                    _MilitaryStatus = new GenericRepository<MilitaryStatus>(_dbContext);
                }
                return _MilitaryStatus;
            }
        }

        public IGenericRepository<GSPayGrade> GSPayGrade
        {
            get
            {
                if (_GSPayGrade == null)
                {
                    _GSPayGrade = new GenericRepository<GSPayGrade>(_dbContext);
                }
                return _GSPayGrade;
            }
        }
        public IGenericRepository<CustomDynamicField> CustomDynamicField
        {
            get
            {
                if (_CustomDynamicField == null)
                {
                    _CustomDynamicField = new GenericRepository<CustomDynamicField>(_dbContext);
                }
                return _CustomDynamicField;
            }
        }

        public IGenericRepository<Check> Check
        {
            get
            {
                if (_Check == null)
                {
                    _Check = new GenericRepository<Check>(_dbContext);
                }
                return _Check;
            }
        }

        public int Commit()
        {
            return _dbContext.SaveChanges();
        }

        public async Task<int> CommitAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }
    }
}