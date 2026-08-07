const COPY = {
  vi: {
    statuses: { Pending: 'Chờ bắt đầu', Active: 'Đang diễn ra', Completed: 'Hoàn thành', Cancelled: 'Đã hủy', Expired: 'Đã hết hạn' },
    rounds: { Technical: 'Kỹ thuật', Behavior: 'Hành vi', Code: 'Lập trình' },
    history: {
      eyebrow: 'Luyện tập', title: 'Lịch sử phỏng vấn', subtitle: 'Xem lại các phiên phỏng vấn và câu trả lời đã được đánh giá.',
      total: 'Tổng phiên', completed: 'Đã hoàn thành', active: 'Đang xử lý', search: 'Tìm theo đợt, vòng hoặc trạng thái',
      status: 'Trạng thái', round: 'Vòng phỏng vấn', all: 'Tất cả', reset: 'Reset', time: 'Thời gian', campaign: 'Đợt phỏng vấn',
      answers: 'Câu trả lời', language: 'Ngôn ngữ', actions: 'Hành động', loading: 'Đang tải lịch sử phỏng vấn…',
      loadTitle: 'Chưa thể tải lịch sử', emptyTitle: 'Chưa có phiên phỏng vấn', emptyDescription: 'Khi bạn bắt đầu luyện tập, các phiên và kết quả sẽ xuất hiện ở đây.',
      noResultsTitle: 'Không tìm thấy kết quả', noResultsDescription: 'Hãy thay đổi hoặc xóa bộ lọc để xem thêm phiên phỏng vấn.', clearFilters: 'Xóa bộ lọc', retry: 'Thử lại',
      viewDetail: 'Xem chi tiết', review: 'Review câu trả lời', detailAria: 'Xem chi tiết đợt phỏng vấn {{id}}', reviewAria: 'Review câu trả lời vòng {{round}}',
      detailEyebrow: 'CHI TIẾT ĐỢT PHỎNG VẤN', detailTitle: 'Đợt phỏng vấn #{{id}}', close: 'Đóng', closeAria: 'Đóng chi tiết', started: 'Bắt đầu', finished: 'Hoàn thành',
      duration: 'Thời lượng cấu hình', minutes: '{{count}} phút', totalScore: 'Điểm tổng', sessionRounds: 'Các vòng phỏng vấn', loadingResult: 'Đang tải kết quả…', scoreError: 'Không thể tải điểm tổng quan của đợt phỏng vấn này.', answerCount: '{{completed}}/{{total}} câu trả lời',
      unauthorized: 'Bạn không có quyền xem lịch sử phỏng vấn.', loadError: 'Không thể tải lịch sử phỏng vấn. Vui lòng thử lại.',
      showing: 'Hiển thị', of: 'trên tổng số', sessionsUnit: 'phiên phỏng vấn', pageSize: 'Số lượng mỗi trang:',
      firstPage: 'Trang đầu', lastPage: 'Trang cuối', previousPage: 'Trang trước', nextPage: 'Trang tiếp',
    },
    review: {
      technical: 'Kỹ thuật', behavioral: 'Hành vi', fallbackRound: 'Phỏng vấn', title: 'Review câu trả lời', eyebrow: 'REVIEW TỪNG CÂU TRẢ LỜI', back: 'Lịch sử phỏng vấn',
      loading: 'Đang tải câu trả lời và đánh giá…', loadTitle: 'Chưa thể mở review', retry: 'Thử lại', backHistory: 'Về lịch sử',
      missingId: 'Không tìm thấy mã phiên phỏng vấn.', forbidden: 'Bạn không có quyền xem phiên phỏng vấn này.', notFound: 'Không tìm thấy phiên phỏng vấn hoặc kết quả review.', timeout: 'Yêu cầu mất quá nhiều thời gian. Vui lòng thử lại.', unsupported: 'Vòng phỏng vấn này chưa hỗ trợ review từng câu trả lời.', loadError: 'Không thể tải review câu trả lời. Vui lòng thử lại.',
      emptyTitle: 'Chưa có câu trả lời để review', emptyDescription: 'Phiên này chưa có dữ liệu câu trả lời hoặc transcript phù hợp để hiển thị.', questionList: 'Danh sách câu hỏi', waitingEvaluation: 'Đang chờ đánh giá',
      mainQuestion: 'CÂU HỎI CHÍNH', question: 'CÂU HỎI', missingQuestion: 'Câu hỏi không có nội dung', skill: 'Kỹ năng: {{skill}}', transcript: 'Transcript câu trả lời', missingTranscript: 'Không có transcript cho câu trả lời này.', aiFeedback: 'Nhận xét AI', rubric: 'Tiêu chí đánh giá', strengths: 'Điểm mạnh', improvements: 'Điểm cần cải thiện', practiceTips: 'Gợi ý luyện tập', followUps: 'Câu hỏi làm rõ / theo dõi', missingFollowUpTranscript: 'Không có transcript.', previous: 'Câu trước', next: 'Câu tiếp',
    },
  },
  en: {
    statuses: { Pending: 'Pending', Active: 'In progress', Completed: 'Completed', Cancelled: 'Cancelled', Expired: 'Expired' },
    rounds: { Technical: 'Technical', Behavior: 'Behavioral', Code: 'Coding' },
    history: {
      eyebrow: 'Practice', title: 'Interview history', subtitle: 'Review completed interview sessions and evaluated answers.',
      total: 'Total sessions', completed: 'Completed', active: 'In progress', search: 'Search by campaign, round, or status',
      status: 'Status', round: 'Interview round', all: 'All', reset: 'Reset', time: 'Time', campaign: 'Interview campaign',
      answers: 'Answers', language: 'Language', actions: 'Actions', loading: 'Loading interview history…',
      loadTitle: 'Unable to load history', emptyTitle: 'No interview sessions yet', emptyDescription: 'Your sessions and results will appear here once you start practicing.',
      noResultsTitle: 'No matching results', noResultsDescription: 'Change or clear the filters to see more interview sessions.', clearFilters: 'Clear filters', retry: 'Retry',
      viewDetail: 'View details', review: 'Review answers', detailAria: 'View details for interview campaign {{id}}', reviewAria: 'Review answers for the {{round}} round',
      detailEyebrow: 'INTERVIEW CAMPAIGN DETAILS', detailTitle: 'Interview campaign #{{id}}', close: 'Close', closeAria: 'Close details', started: 'Started', finished: 'Completed',
      duration: 'Configured duration', minutes: '{{count}} minutes', totalScore: 'Overall score', sessionRounds: 'Interview rounds', loadingResult: 'Loading result…', scoreError: 'Unable to load the overall score for this interview campaign.', answerCount: '{{completed}}/{{total}} answers',
      unauthorized: 'You do not have permission to view interview history.', loadError: 'Unable to load interview history. Please try again.',
      showing: 'Showing', of: 'of', sessionsUnit: 'interview sessions', pageSize: 'Items per page:',
      firstPage: 'First page', lastPage: 'Last page', previousPage: 'Previous page', nextPage: 'Next page',
    },
    review: {
      technical: 'Technical', behavioral: 'Behavioral', fallbackRound: 'Interview', title: 'Answer review', eyebrow: 'REVIEW EACH ANSWER', back: 'Interview history',
      loading: 'Loading answers and evaluation…', loadTitle: 'Unable to open review', retry: 'Retry', backHistory: 'Back to history',
      missingId: 'The interview session ID is missing.', forbidden: 'You do not have permission to view this interview session.', notFound: 'The interview session or review result was not found.', timeout: 'The request took too long. Please try again.', unsupported: 'This interview round does not support answer review yet.', loadError: 'Unable to load answer review. Please try again.',
      emptyTitle: 'No answers to review', emptyDescription: 'This session has no answer or transcript data available for review.', questionList: 'Question list', waitingEvaluation: 'Evaluation pending',
      mainQuestion: 'MAIN QUESTION', question: 'QUESTION', missingQuestion: 'Question content is unavailable', skill: 'Skill: {{skill}}', transcript: 'Answer transcript', missingTranscript: 'No transcript is available for this answer.', aiFeedback: 'AI feedback', rubric: 'Evaluation criteria', strengths: 'Strengths', improvements: 'Areas to improve', practiceTips: 'Practice suggestions', followUps: 'Clarifying / follow-up questions', missingFollowUpTranscript: 'No transcript is available.', previous: 'Previous', next: 'Next',
    },
  },
};

export const getInterviewHistoryCopy = (language) => (
  String(language || '').toLowerCase().startsWith('en') ? COPY.en : COPY.vi
);

