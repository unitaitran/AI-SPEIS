using ai_speis_be.Models;
using ai_speis_be.Models.DTOs;
using ai_speis_be.Models.Enums;
using iText.Kernel.Geom;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ai_speis_be.Repositories.CVRepo
{
    public class CVRepository : ICVRepository 
    {   
        private readonly ApplicationDbContext _context;
        public CVRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        
        public async Task<PagedResult<CVFile>> GetAllCVAsync(JDQueryParameters query, CancellationToken cancellationToken = default)
        {
            var CVFiles = _context.CVFiles.AsQueryable();
            if(!string.IsNullOrEmpty(query.Status) && Enum.TryParse<CVFileStatus>(query.Status, true, out var statusEnum))
            {
                CVFiles = CVFiles.Where(c => c.Status == statusEnum);
            }
            var totalItems =await CVFiles.CountAsync(cancellationToken);
            var orderdCVs = ApplySorting(CVFiles, query.SortBy, query.IsAscending);
            var items = await orderdCVs
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);
            return new PagedResult<CVFile>
            {
                Items = items,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                TotalItems = totalItems
            };


            
        }

        public async Task<CVFile?> GetCVByIdAsync(int id)
        {
            return await _context.CVFiles.Include(c => c.User).FirstOrDefaultAsync(c => c.CVFileId == id && c.Status != CVFileStatus.Archived);
        }

        public async Task<PagedResult<CVFile>> GetCVByUserIdAsync(int userId,JDQueryParameters query, CancellationToken cancellationToken = default)
        {
            var cvFiles = _context.CVFiles.AsNoTracking().Where(c => c.UserId == userId);
            if(!string.IsNullOrEmpty(query.Status) && Enum.TryParse<CVFileStatus>(query.Status, true, out var statusEnum))
            {
                cvFiles = cvFiles.Where(c => c.Status == statusEnum);
            }
            var totalItems = await cvFiles.CountAsync(cancellationToken);
            var orderedItems = ApplySorting(cvFiles, query.SortBy, query.IsAscending);
            var items = await orderedItems
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync(cancellationToken);
            return new PagedResult<CVFile>
            {
                Items = items,  
                TotalItems = totalItems,    
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
            };

         
        }

        public async Task<CVFile> AddCVAsync(CVFile cvFile)
        {
            await _context.CVFiles.AddAsync(cvFile);
            await _context.SaveChangesAsync();
            return cvFile;
        }

        public async Task<bool> DeleteCVAsync(int id)
        {
            var cvFile = await _context.CVFiles.FindAsync(id);
            if (cvFile == null) return false;

            cvFile.Status = CVFileStatus.Archived;
            
            await _context.SaveChangesAsync();
            return true;
        } 

        public async Task<CVFile?> GetActiveCVByUserIdAsync(int userId)
        {
            return await _context.CVFiles
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserId == userId && c.Status != CVFileStatus.Archived);
        }

        public async Task ArchiveAllActiveCVsByUserIdAsync(int userId)
        {
            var activeCVs = await _context.CVFiles
                .Where(c => c.UserId == userId && c.Status != CVFileStatus.Archived)
                .ToListAsync();

            foreach (var cv in activeCVs)
            {
                cv.Status = CVFileStatus.Archived;
                cv.UpdatedAt = DateTime.Now;
            }

            if (activeCVs.Count > 0)
                await _context.SaveChangesAsync();
        }

        public async Task<CVFile> UpdateCVAsync(CVFile cvFile)
        {
            _context.CVFiles.Update(cvFile);
            await _context.SaveChangesAsync();
            return cvFile;
        }
        private static IOrderedQueryable<CVFile> ApplySorting(
            IQueryable<CVFile> query,
            string sortBy,
            bool isAscending)
        {
            var property = (sortBy ?? "UploadedAt").Trim().ToLowerInvariant();
            return (property, isAscending) switch
            {
                
                ("userid", true) => query.OrderBy(c => c.UserId).ThenBy(c => c.CVFileId),
                ("userid", false) => query.OrderByDescending(c => c.UserId).ThenByDescending(c => c.CVFileId),
                // Mặc định sort theo UploadedAt
                (_, true) => query.OrderBy(c => c.UploadedAt).ThenBy(c => c.CVFileId),
                _ => query.OrderByDescending(c => c.UploadedAt).ThenByDescending(c => c.CVFileId)
            };
        }
    } 
   
}