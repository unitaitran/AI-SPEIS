using ai_speis_be.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ai_speis_be.Repositories.SavedQuestionRepo
{
    
    public interface ISavedQuestionRepository
    {
        Task<IEnumerable<SavedQuestion>> GetSavedQuestionsAsync(int userId);
    }
}