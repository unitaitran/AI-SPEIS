using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Repositories.JDRepo
{
    public class JDRepository : IJDRepository
    {
        private readonly ApplicationDbContext _context;
        public JDRepository (ApplicationDbContext context)
        {
            _context = context; 
        }
        public async Task<JDFile> AddJDAsync(JDFile jdFile)
        {
            await _context.JDFiles.AddAsync(jdFile);
            await _context.SaveChangesAsync();  
            return jdFile;
        }

        public async Task<JDFile?> DeleteJDAsync(int id)
        {
            var jdFile =await _context.JDFiles.FindAsync(id);
            if (jdFile != null)
            {
                _context.JDFiles.Remove(jdFile);
                await _context.SaveChangesAsync();
                return jdFile;
            }
            return null;
        }
             

        public async Task<PagedResult<JDFile>> GetAllJDAsync(JDQueryParameters query, CancellationToken cancellationToken = default)
        {
            var jdFiles = _context.JDFiles.AsNoTracking();

            if(!string.IsNullOrEmpty(query.Status) && Enum.TryParse<JDFileStatus>(query.Status, true, out var statusEnum))
            {
                jdFiles = jdFiles.Where(j => j.Status == statusEnum);
            }
            var totalItems = await jdFiles.CountAsync(cancellationToken);
            var orderedItems = ApplySorting(jdFiles, query.SortBy, query.IsAscending);
            var items = await orderedItems
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);
            return new PagedResult<JDFile>
            {
                Items = items,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                TotalItems = totalItems,
            };
        }

        public async Task<JDFile?> GetJDByIdAsync(int id)
        {
            return await _context.JDFiles.Include(j => j.User).FirstOrDefaultAsync(j => j.JDFileId == id);
        }

        public async Task<PagedResult<JDFile>> GetJDByUserIdAsync(int userId, JDQueryParameters query, CancellationToken cancellationToken = default)
        {
            var jdFiles = _context.JDFiles.AsNoTracking().Where(j => j.UserId == userId);
            if(!string.IsNullOrEmpty(query.Status) && Enum.TryParse<JDFileStatus>(query.Status, true, out var statusEnum))
            {
                jdFiles = jdFiles.Where(j => j.Status == statusEnum);
            }
            
            var totalItems = await jdFiles.CountAsync(cancellationToken);
            var orderedItems = ApplySorting(jdFiles, query.SortBy, query.IsAscending);
            var items = await orderedItems
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);
            return new PagedResult<JDFile>
            {
                Items = items,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                TotalItems = totalItems,
            };
        }

        public async Task<JDFile> UpdateJDAsync(JDFile jdFile)
        {
            _context.JDFiles.Update(jdFile);
            await _context.SaveChangesAsync();
            return jdFile;
        }
        private static IOrderedQueryable<JDFile> ApplySorting(
           IQueryable<JDFile> query,
           string sortBy,
           bool isAscending)
        {
            var property = (sortBy ?? "UploadedAt").Trim().ToLowerInvariant();
            return (property, isAscending) switch
            {

                ("userid", true) => query.OrderBy(j => j.UserId).ThenBy(j => j.JDFileId),
                ("userid", false) => query.OrderByDescending(j => j.UserId).ThenByDescending(j => j.JDFileId),
                // Mặc định sort theo UploadedAt
                (_, true) => query.OrderBy(j => j.UploadedAt).ThenBy(j => j.JDFileId),
                _ => query.OrderByDescending(j => j.UploadedAt).ThenByDescending(j => j.JDFileId),
            };
        }
    }
}
