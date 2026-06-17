using ai_speis_be.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ai_speis_be.Repositories.SavedQuestionRepo
{
    public class SavedQuestionRepository : ISavedQuestionRepository
    { 
        private readonly ApplicationDbContext _context;
        public SavedQuestionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<SavedQuestion>> GetSavedQuestionsAsync(int userId)
        {
            return await _context.SavedQuestion.Include(sq => sq.Question).Where(sq => sq.Question.IsDeleted == false && sq.UserId == userId).ToListAsync();
        }
    }
}