using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ai_speis_be.BehaviouralInterviews.Selection
{
    public interface IBehaviouralQuestionSelectionService
    {
         Task<BehaviouralQuestionSelectionResult> SelectAsync(
            BehaviouralSelectionContext context,
            CancellationToken cancellationToken = default);
    }
}