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

        public async Task<SavedQuestion?> SaveQuestionAsync(int userId, int questionId)
        {
            var question = await _context.Questions.FirstOrDefaultAsync(q => q.QuestionId == questionId && q.IsDeleted == false);
            if (question == null)
            {
                return null;
            }
            // check if user has saved the question before
            var existingSave = await _context.SavedQuestion.FirstOrDefaultAsync(sq => sq.UserId == userId && sq.QuestionId == questionId);
            //if has saved => return the question
            if (existingSave != null)
            {
                existingSave.Question = question;
                return existingSave;
            }
            var savedQuestion = new SavedQuestion
            {
                QuestionId = questionId,
                UserId = userId,
                SavedAt = DateTime.UtcNow,
                Question = question
            };
            await _context.SavedQuestion.AddAsync(savedQuestion);
            await _context.SaveChangesAsync();
            return savedQuestion;
        }

        public async Task<bool> UnsaveQuestionAsync(int userId, int questionId)
        {
            var existingSave = await _context.SavedQuestion.FirstOrDefaultAsync(sq => sq.UserId == userId && sq.QuestionId == questionId);
            if(existingSave == null)
            {
                return false;
            }
            _context.SavedQuestion.Remove(existingSave);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}