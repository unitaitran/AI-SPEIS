using ai_speis_be.Models;

namespace ai_speis_be.Repositories.CodingRepo
{
    public interface ICodingRepository
    {
        /// <summary>
        /// Lấy CodingQuestion kèm theo tất cả TestCases và Templates.
        /// </summary>
        Task<CodingQuestion?> GetCodingQuestionWithTestCasesAsync(
            int questionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lấy danh sách câu hỏi coding theo InterviewSession, kèm Templates và sample TestCases.
        /// </summary>
        Task<List<CodingQuestion>> GetCodingQuestionsBySessionIdAsync(
            int sessionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Tạo submission mới kèm theo các test case results.
        /// </summary>
        Task<CodingSubmission> CreateSubmissionAsync(
            CodingSubmission submission,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lấy submission theo ID, kèm test case results.
        /// </summary>
        Task<CodingSubmission?> GetSubmissionByIdAsync(
            int submissionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Lấy lịch sử submissions của 1 câu hỏi trong 1 session.
        /// </summary>
        Task<List<CodingSubmission>> GetSubmissionsBySessionAndQuestionAsync(
            int sessionId,
            int questionId,
            CancellationToken cancellationToken = default);
    }
}
