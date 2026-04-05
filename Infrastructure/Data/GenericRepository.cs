using ApplicationCore.Interfaces;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Asn1;
using System.Linq.Expressions;

namespace Infrastructure.Data
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        /// <summary>
        /// By using ReadOnly ApplicationDbContext, you can have access to only
        /// querying capabilities of DbContext. UnitOfWork writes
        /// (commits) to the PHYSICAL tables (not internal object).
        /// </summary>
        private readonly ApplicationDbContext _dbContext;

        public GenericRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;

        }

        /// <summary>
        /// Adds the specified entity to the database context and saves the changes.
        /// </summary>
        /// <param name="entity">The entity to add to the database. Cannot be null.</param>
        public void Add(T entity)
        {
            _dbContext.Set<T>().Add(entity);
            _dbContext.SaveChanges();
        }

        /// <summary>
        /// Deletes the specified entity from the database.
        /// </summary>
        /// <param name="entity">The entity to be deleted. Cannot be null.</param>
        public void Delete(T entity)
        {
            _dbContext.Set<T>().Remove(entity);
            _dbContext.SaveChanges();
        }

        /// <summary>
        /// Deletes multiple specified entities from the database.
        /// </summary>
        /// <param name="entities">The entitoes to be deleted. Cannot be null.</param>
        public void Delete(IEnumerable<T> entities)
        {
            _dbContext.Set<T>().RemoveRange(entities);
            _dbContext.SaveChanges();
        }

        /// <summary>
        /// Gets a single entity based on the provided predicate.
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="trackChanges"></param>
        /// <param name="includes"></param>
        /// <returns></returns>
        public virtual T Get(Expression<Func<T, bool>> predicate, bool trackChanges = false, string? includes = null)
        {
            IQueryable<T> queryable = _dbContext.Set<T>();

            if (!string.IsNullOrEmpty(includes)) // If other objects to include (join)
            {
                foreach (var includeProperty in includes.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    queryable = queryable.Include(includeProperty);
                }
            }

            if (trackChanges) // If is false, we do not want EF tracking changes
            {
                queryable = queryable.AsNoTracking();
            }

            return queryable.FirstOrDefault(predicate);
        }


        /// <summary>
        /// Gets a single entity based on the provided predicate asynchronously.
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="trackChanges"></param>
        /// <param name="includes"></param>
        /// <returns></returns>
        public virtual async Task<T> GetAsync(Expression<Func<T, bool>> predicate, bool trackChanges = false, string? includes = null)
        {
            IQueryable<T> queryable = _dbContext.Set<T>();

            if (!string.IsNullOrEmpty(includes)) // If other objects to include (join)
            {
                var includeProperties = includes.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var includeProperty in includeProperties)
                {
                    queryable = queryable.Include(includeProperty);
                }
            }

            if (trackChanges) // If is false, we do not want EF tracking changes
            {
                queryable = queryable.AsNoTracking();
            }

            return await queryable.FirstOrDefaultAsync(predicate);
        }

        /// <summary>
        /// Gets an entity by its unique identifier.
        /// </summary>
        /// <remarks>
        /// The virtual keyword is used to modify a method, property, indexer, or
        /// and allows for it to be overridden in a derived class.
        /// </remarks>
        /// <param name="id"></param>
        /// <returns></returns>
        public virtual T GetById(int? id)
        {
            return _dbContext.Set<T>().Find(id);
        }

        /// <summary>
        /// Gets all specified entities asynchronously.
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="orderBy"></param>
        /// <param name="includes"></param>
        /// <returns></returns>
        public virtual async Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>>? predicate = null, Expression<Func<T, int>>? orderBy = null, string? includes = null)
        {
            IQueryable<T> queryable = _dbContext.Set<T>();

            if (!string.IsNullOrEmpty(includes))
            {
                foreach (var includeProperty in includes.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    queryable = queryable.Include(includeProperty);
                }
            }

            if (predicate != null)
            {
                queryable = queryable.Where(predicate);
            }

            if (orderBy != null)
            {
                queryable = queryable.OrderBy(orderBy);
            }

            return await queryable.ToListAsync();
        }

        /// <summary>
        /// Reloads the specified entity from the database, optionally including related entities.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="includes"></param>
        /// <returns></returns>
        public async Task ReloadAsync(T entity, string? includes = null)
        {
            var entry = _dbContext.Entry(entity);

            if (!string.IsNullOrEmpty(includes))
            {
                foreach (var include in includes.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (include.Contains('.'))
                    {
                        var parts = include.Split('.');
                        var firstLevel = entry.Reference(parts[0]);
                        await firstLevel.LoadAsync();

                        var nestedEntity = firstLevel.CurrentValue;
                        if (nestedEntity != null)
                        {
                            var nestedEntry = _dbContext.Entry(nestedEntity);
                            await nestedEntry.Reference(parts[1]).LoadAsync();
                        }
                    }
                    else
                    {
                        await entry.Reference(include).LoadAsync();
                    }
                }
            }
            else
            {
                await entry.ReloadAsync();
            }
        }

        /// <summary>
        /// Updates the specified entity in the database.
        /// </summary>
        /// <param name="entity"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void Update(T entity)
        {
            var entry = _dbContext.Entry(entity);
            var key = _dbContext.Model.FindEntityType(typeof(T))?.FindPrimaryKey()?.Properties.FirstOrDefault();

            if (key == null)
                throw new InvalidOperationException("No primary key defined for this entity.");

            var keyValue = key.PropertyInfo.GetValue(entity);

            var local = _dbContext.Set<T>().Local
                .FirstOrDefault(e =>
                    key.PropertyInfo.GetValue(e)?.Equals(keyValue) == true);

            if (local != null)
            {
                _dbContext.Entry(local).State = EntityState.Detached;
            }

            _dbContext.Entry(entity).State = EntityState.Modified;
            _dbContext.SaveChanges();
        }

        /// <summary>
        /// Updates multiple specified entities in the database.
        /// </summary>
        /// <param name="entities"></param>
        public void UpdateRange(IEnumerable<T> entities)
        {
            foreach (var entity in entities)
            {
                var entry = _dbContext.Entry(entity);
                entry.State = EntityState.Modified;
            }

            _dbContext.SaveChanges();
        }

        /// <summary>
        /// Gets the first entity that matches the specified filter asynchronously.
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="includeProperties"></param>
        /// <returns></returns>
        public async Task<T> GetFirstOrDefaultAsync(
            Expression<Func<T, bool>> filter = null,
            string includeProperties = null)
        {
            IQueryable<T> query = _dbContext.Set<T>();

            if (filter != null)
                query = query.Where(filter);

            if (!string.IsNullOrEmpty(includeProperties))
            {
                foreach (var includeProp in includeProperties
                             .Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    query = query.Include(includeProp.Trim());
                }
            }

            return await query.FirstOrDefaultAsync();
        }

        /// <summary>
        /// Adds the specified entity to the database asynchronously.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public async Task AddAsync(T entity)
        {
            await _dbContext.Set<T>().AddAsync(entity);
            await _dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// Gets all specified entities.
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="orderBy"></param>
        /// <param name="includes"></param>
        /// <returns></returns>
        public IEnumerable<T> GetAll(Expression<Func<T, bool>>? predicate = null, Expression<Func<T, int>>? orderBy = null, string? includes = null)
        {
            IQueryable<T> queryable = _dbContext.Set<T>();

            if (!string.IsNullOrEmpty(includes))
            {
                foreach (var includeProperty in includes.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    queryable = queryable.Include(includeProperty);
                }
            }

            if (predicate != null)
            {
                queryable = queryable.Where(predicate);
            }

            if (orderBy != null)
            {
                queryable = queryable.OrderBy(orderBy);
            }

            return queryable.ToList();
        }

        /// <summary>
        /// Gets all specified entities as IQueryable. This is for dynamic DataTables queries.
        /// </summary>
        /// <param name="predicate"></param>
        /// <param name="orderBy"></param>
        /// <param name="includes"></param>
        /// <returns></returns>
        public IQueryable<T> GetAllQueryable(Expression<Func<T, bool>>? predicate = null, Expression<Func<T, int>>? orderBy = null, string? includes = null)
        {
            IQueryable<T> queryable = _dbContext.Set<T>().AsNoTracking();

            if (!string.IsNullOrEmpty(includes))
            {
                foreach (var includeProperty in includes.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    queryable = queryable.Include(includeProperty);
                }
            }

            if (predicate != null)
            {
                queryable = queryable.Where(predicate);
            }

            if (orderBy != null)
            {
                queryable = queryable.OrderBy(orderBy);
            }

            return queryable; // ❌ remove ToList() — return IQueryable
        }

    }
}
