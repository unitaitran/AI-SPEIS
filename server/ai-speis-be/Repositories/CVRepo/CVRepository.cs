using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ai_speis_be.Models;
using ai_speis_be.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace ai_speis_be.Repositories.CVRepo
{
    public class CVRepository : ICVRepository 
    {   
        private readonly ApplicationDbContext _context;
        public CVRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        
        public async Task<IEnumerable<CVFile>> GetAllCVAsync()
        {
            var CVFiles = _context.CVFiles.AsQueryable();
            return await CVFiles.ToListAsync();
        }

        public async Task<CVFile?> GetCVByIdAsync(int id)
        {
            return await _context.CVFiles.Include(c => c.User).FirstOrDefaultAsync(c => c.CVFileId == id);
        }

        public async Task<CVFile?> GetCVByUserIdAsync(int userId)
        {
            return await _context.CVFiles.Include(c => c.User).FirstOrDefaultAsync(c => c.UserId == userId);
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

        public async Task<CVFile> UpdateCVAsync(CVFile cvFile)
        {
            _context.CVFiles.Update(cvFile);
            await _context.SaveChangesAsync();
            return cvFile;
        }
    } 
}