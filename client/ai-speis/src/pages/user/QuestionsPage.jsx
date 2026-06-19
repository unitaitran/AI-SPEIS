import React, { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Search,
  ChevronDown,
  Bookmark,
  Loader2,
  HelpCircle,
  X,
  ChevronLeft,
  ChevronRight,
  Sparkles
} from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';
import { ENDPOINTS } from '../../config/api';

function QuestionsPage() {
  const { t, i18n } = useTranslation('dashboard');
  const isVi = i18n.language.startsWith('vi');

  // Helper for Session Expiration
  const handleSessionExpired = useCallback(() => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    alert(isVi ? 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại!' : 'Session has expired. Please login again!');
    window.location.href = '/#login';
  }, [isVi]);

  // API states
  const [questions, setQuestions] = useState([]);
  const [savedQuestionIds, setSavedQuestionIds] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isSavedLoading, setIsSavedLoading] = useState(false);

  // Search & Filter states
  const [searchInputValue, setSearchInputValue] = useState('');
  const [selectedRoles, setSelectedRoles] = useState([]);
  const [selectedDifficulties, setSelectedDifficulties] = useState([]);
  const [showSavedOnly, setShowSavedOnly] = useState(false);

  const [sortOption] = useState('popular'); // popular, newest

  // Pagination
  const [currentPage, setCurrentPage] = useState(1);
  const PAGE_SIZE = 10;

  // UI States
  const [expandedQuestionIds, setExpandedQuestionIds] = useState([]);
  const [expandedFilters, setExpandedFilters] = useState({
    role: true,
    difficulty: true
  });

  // Fetch Questions and Saved Questions on Mount
  useEffect(() => {
    const fetchData = async () => {
      setIsLoading(true);
      try {
        const token = localStorage.getItem('token');
        if (!token) {
          handleSessionExpired();
          return;
        }

        // Fetch all questions
        const questionsResponse = await fetch(ENDPOINTS.QUESTIONS_GET, {
          headers: { 'Authorization': `Bearer ${token}` }
        });

        if (questionsResponse.status === 401) {
          handleSessionExpired();
          return;
        }

        let questionsData = [];
        if (questionsResponse.ok) {
          questionsData = await questionsResponse.json();
          setQuestions(questionsData);
        }

        // Fetch saved questions to sync bookmarks
        const savedResponse = await fetch(ENDPOINTS.SAVED_QUESTIONS_GET, {
          headers: { 'Authorization': `Bearer ${token}` }
        });

        if (savedResponse.status === 401) {
          handleSessionExpired();
          return;
        }

        if (savedResponse.ok) {
          const savedData = await savedResponse.json();
          const savedIds = savedData.map(sq => sq.questionId);
          setSavedQuestionIds(savedIds);
        }
      } catch (error) {
        console.error('Lỗi khi tải dữ liệu câu hỏi:', error);
      } finally {
        setIsLoading(false);
      }
    };

    fetchData();
  }, [handleSessionExpired]);

  // Toggle filter section collapse
  const toggleFilterSection = (section) => {
    setExpandedFilters(prev => ({
      ...prev,
      [section]: !prev[section]
    }));
  };

  // Handle Search Submit
  const handleSearchSubmit = (e) => {
    e.preventDefault();
  };

  // Handle Checkbox Change
  const handleCheckboxChange = (value, list, setList) => {
    const updatedList = list.includes(value)
      ? list.filter(item => item !== value)
      : [...list, value];
    setList(updatedList);
    setCurrentPage(1);
  };

  // Toggle Bookmark (Save/Unsave Question)
  const handleBookmarkToggle = async (questionId) => {
    const token = localStorage.getItem('token');
    if (!token) {
      handleSessionExpired();
      return;
    }
    if (isSavedLoading) return;

    const isAlreadySaved = savedQuestionIds.includes(questionId);

    setIsSavedLoading(true);
    try {
      if (isAlreadySaved) {
        // Unsave
        const response = await fetch(ENDPOINTS.SAVED_QUESTIONS_UNSAVE(questionId), {
          method: 'DELETE',
          headers: { 'Authorization': `Bearer ${token}` }
        });

        if (response.status === 401) {
          handleSessionExpired();
          return;
        }

        if (response.ok) {
          setSavedQuestionIds(prev => prev.filter(id => id !== questionId));
        }
      } else {
        // Save
        const response = await fetch(ENDPOINTS.SAVED_QUESTIONS_SAVE(questionId), {
          method: 'POST',
          headers: { 'Authorization': `Bearer ${token}` }
        });

        if (response.status === 401) {
          handleSessionExpired();
          return;
        }

        if (response.ok) {
          setSavedQuestionIds(prev => [...prev, questionId]);
        }
      }
    } catch (error) {
      console.error('Lỗi kết nối khi thay đổi trạng thái lưu câu hỏi:', error);
    } finally {
      setIsSavedLoading(false);
    }
  };

  // Toggle Answer Dropdown Accordion
  const toggleExpandQuestion = (questionId) => {
    setExpandedQuestionIds(prev =>
      prev.includes(questionId)
        ? prev.filter(id => id !== questionId)
        : [...prev, questionId]
    );
  };

  // Get dynamic unique filters from fetched questions
  const dynamicRoles = [...new Set(questions.map(q => q.roleTarget).filter(Boolean))].sort();

  // Helper mock for practice count (generated deterministically from ID)
  const getMockPracticeCount = (id) => {
    return (id * 149 + 17) % 3500 + 50;
  };

  // Map difficulty to text
  const getDifficultyText = (diff) => {
    if (diff === 'Easy' || diff === 0) return t('questions.difficulty_easy', 'Dễ');
    if (diff === 'Medium' || diff === 1) return t('questions.difficulty_medium', 'Trung bình');
    if (diff === 'Hard' || diff === 2) return t('questions.difficulty_hard', 'Khó');
    return diff;
  };

  // Map difficulty to color classes
  const getDifficultyBadgeClass = (diff) => {
    if (diff === 'Easy' || diff === 0) return 'bg-success-light/35 text-success border-success/20';
    if (diff === 'Medium' || diff === 1) return 'bg-warning-light/35 text-warning border-warning/20';
    if (diff === 'Hard' || diff === 2) return 'bg-error-light/35 text-error border-error/20';
    return 'bg-surface-3 text-text-secondary border-border';
  };

  // Filter and Search logic
  const filteredQuestions = questions.filter(q => {
    // 1. Search keyword
    const matchesSearch = !searchInputValue ||
      q.questionContent.toLowerCase().includes(searchInputValue.toLowerCase()) ||
      (q.suggestedAnswer && q.suggestedAnswer.toLowerCase().includes(searchInputValue.toLowerCase()));

    // 2. Role Filter
    const matchesRole = selectedRoles.length === 0 || selectedRoles.includes(q.roleTarget);

    // 3. Difficulty Filter
    const matchesDifficulty = selectedDifficulties.length === 0 || selectedDifficulties.some(d => {
      if (d === 'Easy') return q.difficulty === 'Easy' || q.difficulty === 0;
      if (d === 'Medium') return q.difficulty === 'Medium' || q.difficulty === 1;
      if (d === 'Hard') return q.difficulty === 'Hard' || q.difficulty === 2;
      return false;
    });

    // 4. Saved Filter
    const matchesBookmark = !showSavedOnly || savedQuestionIds.includes(q.questionId);

    return matchesSearch && matchesRole && matchesDifficulty && matchesBookmark;
  });

  // Sort logic
  const sortedQuestions = [...filteredQuestions].sort((a, b) => {
    if (sortOption === 'newest') {
      return b.questionId - a.questionId;
    } else {
      return getMockPracticeCount(b.questionId) - getMockPracticeCount(a.questionId);
    }
  });

  // Pagination logic
  const totalItems = sortedQuestions.length;
  const totalPages = Math.ceil(totalItems / PAGE_SIZE) || 1;
  const displayedQuestions = sortedQuestions.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);

  return (
    <UserLayout>
      <div className="space-y-6 pb-12">
        {/* Header Title - Standardized with Dashboard size */}
        <div>
          <h1 className="text-3xl font-bold text-text-primary tracking-tight mb-1">
            {t('questions.title', 'Ngân hàng câu hỏi')}
          </h1>
          <p className="text-base text-text-secondary leading-relaxed max-w-4xl">
            {t('questions.desc', 'Duyệt qua hàng ngàn câu hỏi phỏng vấn kỹ thuật và kỹ năng mềm được tuyển chọn, mở phòng các vòng phỏng vấn thực tế.')}
          </p>
        </div>

        {/* Search Bar */}
        <form onSubmit={handleSearchSubmit} className="flex gap-2.5">
          <div className="relative flex-1">
            <input
              type="text"
              placeholder={t('questions.search_placeholder', 'Tìm câu hỏi phỏng vấn (VD: React Hooks, B-Tree, Xử lý xung đột...)')}
              value={searchInputValue}
              onChange={(e) => {
                setSearchInputValue(e.target.value);
                setCurrentPage(1);
              }}
              className="w-full bg-surface-2 border border-border-strong rounded-xl pl-12 pr-4 py-3.5 text-sm text-text-primary focus:outline-none focus:border-primary-dark transition-all placeholder:text-text-disabled"
            />
            <Search size={20} className="absolute left-4 top-4 text-text-secondary" />
            {searchInputValue && (
              <button
                type="button"
                onClick={() => {
                  setSearchInputValue('');
                  setCurrentPage(1);
                }}
                className="absolute right-4 top-4 text-text-secondary hover:text-text-primary cursor-pointer"
              >
                <X size={16} />
              </button>
            )}
          </div>
          {/* Saved Toggle Filter Button */}
          <button
            type="button"
            onClick={() => {
              setShowSavedOnly(prev => !prev);
              setCurrentPage(1);
            }}
            className={`flex items-center gap-2 px-5 py-3.5 rounded-xl text-xs font-bold transition-all whitespace-nowrap cursor-pointer uppercase tracking-wider shadow-sm border ${
              showSavedOnly
                ? 'border-primary bg-primary-xlight text-primary-dark hover:bg-primary-light'
                : 'border-transparent bg-surface-2 text-text-secondary hover:bg-surface-3 hover:text-text-primary'
            }`}
          >
            <Bookmark size={16} className={showSavedOnly ? 'fill-current text-primary-dark' : 'text-text-secondary'} />
            {t('questions.filter_saved', 'Đã lưu')}
          </button>

          <button
            type="submit"
            className="bg-gradient-to-br from-primary to-[#4A90E2] hover:opacity-90 text-white text-xs font-bold px-7 py-3.5 rounded-xl transition-all cursor-pointer whitespace-nowrap uppercase tracking-wider shadow-sm"
          >
            {t('questions.search_button', 'Tìm kiếm')}
          </button>
        </form>

        {isLoading ? (
          <div className="flex flex-col items-center justify-center min-h-[300px] space-y-4">
            <Loader2 size={40} className="text-primary animate-spin" />
            <p className="text-sm text-text-secondary">{t('loading', 'Đang tải...')}</p>
          </div>
        ) : (
          <div className="grid grid-cols-1 lg:grid-cols-4 gap-6 items-start">

            {/* Left Sidebar Filter Column - Sticky with top offset matching the sticky header height */}
            <aside className="lg:col-span-1 space-y-4 sticky top-[93px] self-start">

              {/* Filter 1: VỊ TRÍ (Role) */}
              <div className="bg-surface-2 border border-border rounded-xl overflow-hidden shadow-sm">
                <button
                  onClick={() => toggleFilterSection('role')}
                  className="w-full px-4 py-3.5 flex items-center justify-between bg-surface-1 border-b border-border text-sm font-bold text-text-primary uppercase tracking-wider cursor-pointer"
                >
                  <span>{t('questions.filter_role', 'Vị trí (Role)')}</span>
                  <ChevronDown size={16} className={`transition-transform duration-300 transform ${expandedFilters.role ? 'rotate-180' : ''}`} />
                </button>
                <div className={`overflow-hidden transition-all duration-300 ease-in-out ${expandedFilters.role ? 'max-h-96 opacity-100' : 'max-h-0 opacity-0'}`}>
                  <div className="p-4 space-y-3 max-h-56 overflow-y-auto">
                    {dynamicRoles.length === 0 ? (
                      <p className="text-xs text-text-disabled italic">{t('questions.no_data', 'Không có dữ liệu')}</p>
                    ) : (
                      dynamicRoles.map(role => (
                        <label key={role} className="flex items-center text-xs font-semibold text-text-secondary cursor-pointer hover:text-primary-dark transition-colors">
                          <input
                            type="checkbox"
                            checked={selectedRoles.includes(role)}
                            onChange={() => handleCheckboxChange(role, selectedRoles, setSelectedRoles)}
                            className="mr-2.5 accent-primary h-4.5 w-4.5 rounded border-border-strong"
                          />
                          <span>{role}</span>
                          <span className="ml-auto text-[10px] text-text-disabled font-bold bg-surface-3 px-2 py-0.5 rounded border border-border/20">
                            {questions.filter(q => q.roleTarget === role).length}
                          </span>
                        </label>
                      ))
                    )}
                  </div>
                </div>
              </div>

              {/* Filter 2: ĐỘ KHÓ */}
              <div className="bg-surface-2 border border-border rounded-xl overflow-hidden shadow-sm">
                <button
                  onClick={() => toggleFilterSection('difficulty')}
                  className="w-full px-4 py-3.5 flex items-center justify-between bg-surface-1 border-b border-border text-sm font-bold text-text-primary uppercase tracking-wider cursor-pointer"
                >
                  <span>{t('questions.filter_difficulty', 'Độ khó')}</span>
                  <ChevronDown size={16} className={`transition-transform duration-300 transform ${expandedFilters.difficulty ? 'rotate-180' : ''}`} />
                </button>
                <div className={`overflow-hidden transition-all duration-300 ease-in-out ${expandedFilters.difficulty ? 'max-h-96 opacity-100' : 'max-h-0 opacity-0'}`}>
                  <div className="p-4 space-y-3">
                    {['Easy', 'Medium', 'Hard'].map(d => (
                      <label key={d} className="flex items-center text-xs font-semibold text-text-secondary cursor-pointer hover:text-primary-dark transition-colors">
                        <input
                          type="checkbox"
                          checked={selectedDifficulties.includes(d)}
                          onChange={() => handleCheckboxChange(d, selectedDifficulties, setSelectedDifficulties)}
                          className="mr-2.5 accent-primary h-4.5 w-4.5 rounded border-border-strong"
                        />
                        <span>{getDifficultyText(d)}</span>
                        <span className="ml-auto text-[10px] text-text-disabled font-bold bg-surface-3 px-2 py-0.5 rounded border border-border/20">
                          {questions.filter(q => {
                            if (d === 'Easy') return q.difficulty === 'Easy' || q.difficulty === 0;
                            if (d === 'Medium') return q.difficulty === 'Medium' || q.difficulty === 1;
                            if (d === 'Hard') return q.difficulty === 'Hard' || q.difficulty === 2;
                            return false;
                          }).length}
                        </span>
                      </label>
                    ))}
                  </div>
                </div>
              </div>

              {/* Mascot Studying Banner */}
              <div className="flex flex-col items-center text-center relative py-2">
                {/* Speech Bubble Above Mascot */}
                <div className="relative bg-primary-xlight text-primary-dark border border-primary-light/50 px-4 py-2.5 rounded-xl text-xs font-bold leading-relaxed max-w-[90%] mb-3.5 shadow-sm">
                  {/* Bubble tail arrow pointing down */}
                  <div className="absolute bottom-[-5px] left-1/2 transform -translate-x-1/2 rotate-45 w-2.5 h-2.5 bg-primary-xlight border-r border-b border-primary-light/50"></div>
                  "Let's study question!"
                </div>

                <img
                  src="/studying_mascot.jpg"
                  alt="Studying Mascot"
                  className="w-48 h-48 object-cover rounded-full border-2 border-primary/20 shadow-md"
                />
              </div>
            </aside>

            {/* Right List Column */}
            <main className="lg:col-span-3 space-y-4">

              {/* Controls bar */}

              {/* Questions List */}
              {displayedQuestions.length === 0 ? (
                <div className="bg-surface-2 border border-dashed border-border rounded-2xl p-14 text-center shadow-sm">
                  <HelpCircle size={40} className="text-text-disabled mx-auto mb-3" />
                  <h3 className="text-base font-bold text-text-primary mb-1.5">
                    {t('questions.no_questions_found', 'Không tìm thấy câu hỏi phù hợp')}
                  </h3>
                  <p className="text-xs text-text-secondary leading-relaxed">
                    {t('questions.no_questions_desc', 'Vui lòng thay đổi từ khóa tìm kiếm hoặc các tùy chọn bộ lọc.')}
                  </p>
                </div>
              ) : (
                <div className="space-y-4">
                  {displayedQuestions.map((q) => {
                    const isSaved = savedQuestionIds.includes(q.questionId);
                    const isExpanded = expandedQuestionIds.includes(q.questionId);
                    return (
                      <div
                        key={q.questionId}
                        className="bg-surface-2 border border-border hover:border-primary-light hover:shadow-[0_4px_20px_rgba(111,182,232,0.08)] rounded-xl p-5 sm:p-6 transition-all relative group flex flex-col justify-between"
                      >
                        <div>
                          {/* Tags line and Actions */}
                          <div className="flex items-start justify-between gap-4 mb-3.5">
                            <div className="flex flex-wrap gap-1.5">
                              {q.roleTarget && (
                                <span className="px-3 py-1 text-xs font-bold bg-primary-xlight text-primary-dark border border-primary-light/55 rounded-md">
                                  {q.roleTarget}
                                </span>
                              )}
                              <span className={`px-3 py-1 text-xs font-bold border rounded-md ${getDifficultyBadgeClass(q.difficulty)}`}>
                                {getDifficultyText(q.difficulty)}
                              </span>
                            </div>

                            <div className="flex items-center gap-1 shrink-0">
                              {/* Expand Chevron Icon button instead of Details modal */}
                              <button
                                onClick={() => toggleExpandQuestion(q.questionId)}
                                className={`p-1.5 rounded-lg border transition-all cursor-pointer ${isExpanded
                                    ? 'bg-primary-xlight border-primary-light text-primary-dark shadow-sm'
                                    : 'border-transparent text-text-secondary hover:text-text-primary hover:bg-surface-3'
                                  }`}
                                title={isExpanded ? (isVi ? 'Ẩn đáp án gợi ý' : 'Hide suggested answer') : (isVi ? 'Xem đáp án gợi ý' : 'Show suggested answer')}
                              >
                                <ChevronDown size={16} className={`transition-transform duration-300 transform ${isExpanded ? 'rotate-180' : ''}`} />
                              </button>

                              {/* Bookmark icon */}
                              <button
                                onClick={() => handleBookmarkToggle(q.questionId)}
                                disabled={isSavedLoading}
                                className={`p-1.5 rounded-lg border transition-all cursor-pointer ${isSaved
                                    ? 'bg-primary-xlight border-primary-light text-primary-dark shadow-sm'
                                    : 'border-transparent text-text-disabled hover:text-text-primary hover:bg-surface-3'
                                  }`}
                                title={isSaved ? t('questions.confirm_delete_bookmark') : t('questions.status_saved')}
                              >
                                <Bookmark size={16} className={isSaved ? 'fill-primary-dark text-primary-dark' : ''} />
                              </button>
                            </div>
                          </div>

                          {/* Question text - Increased font size */}
                          <h3
                            onClick={() => toggleExpandQuestion(q.questionId)}
                            className="text-base sm:text-[18px] font-extrabold text-text-primary leading-snug hover:text-primary-dark transition-colors mb-2 cursor-pointer"
                          >
                            {q.questionContent}
                          </h3>
                        </div>

                        {/* Expandable answer panel - Slide down effect */}
                        <div className={`overflow-hidden transition-all duration-300 ease-in-out ${isExpanded ? 'max-h-[300px] opacity-100 mt-4 pt-4 border-t border-border/50' : 'max-h-0 opacity-0'}`}>
                          {isExpanded && (
                            <div className="space-y-2">
                              <div className="flex items-center gap-1.5">
                                <Sparkles size={14} className="text-primary-dark" />
                                <h4 className="text-[11px] font-bold text-primary-dark uppercase tracking-wider">
                                  {t('questions.ai_suggested_answer', 'Gợi ý trả lời (Tips)')}
                                </h4>
                              </div>
                              <p className="text-sm text-text-secondary leading-relaxed bg-primary-xlight/20 border border-dashed border-primary-light/50 p-3.5 rounded-xl font-semibold">
                                {q.suggestedAnswer || t('questions.no_suggested_answer', 'Chưa có gợi ý trả lời')}
                              </p>
                            </div>
                          )}
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}

              {/* Pagination controls */}
              {totalPages > 1 && (
                <div className="flex justify-center items-center gap-1.5 pt-4">
                  <button
                    onClick={() => setCurrentPage(prev => Math.max(prev - 1, 1))}
                    disabled={currentPage === 1}
                    className="p-2 border border-border rounded-lg bg-surface-2 text-text-secondary hover:text-text-primary hover:bg-surface-3 transition-colors disabled:opacity-50 disabled:pointer-events-none cursor-pointer"
                  >
                    <ChevronLeft size={16} />
                  </button>

                  {Array.from({ length: totalPages }).map((_, index) => {
                    const pageNum = index + 1;
                    const isActive = pageNum === currentPage;
                    return (
                      <button
                        key={pageNum}
                        onClick={() => setCurrentPage(pageNum)}
                        className={`w-9 h-9 text-xs font-bold rounded-lg border transition-all cursor-pointer ${isActive
                            ? 'bg-gradient-to-br from-primary to-[#4A90E2] border-transparent text-white shadow-sm'
                            : 'border-border bg-surface-2 text-text-secondary hover:text-text-primary hover:bg-surface-3'
                          }`}
                      >
                        {pageNum}
                      </button>
                    );
                  })}

                  <button
                    onClick={() => setCurrentPage(prev => Math.min(prev + 1, totalPages))}
                    disabled={currentPage === totalPages}
                    className="p-2 border border-border rounded-lg bg-surface-2 text-text-secondary hover:text-text-primary hover:bg-surface-3 transition-colors disabled:opacity-50 disabled:pointer-events-none cursor-pointer"
                  >
                    <ChevronRight size={16} />
                  </button>
                </div>
              )}
            </main>
          </div>
        )}
      </div>
    </UserLayout>
  );
}

export default QuestionsPage;
