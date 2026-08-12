import React, { useEffect, useState, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Search,
  FileInput,
  Plus,
  Edit3,
  Trash2,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  X,
  AlertCircle
} from 'lucide-react';
import { questionService } from '../../../services/QuestionService';
import { codingService } from '../../../services/codingService';
import notify from '../../../utils/notification';
import '../../../styles/admin/QuestionManagementPage.css';

const DIFFICULTY_OPTIONS = ['all', 'Easy', 'Medium', 'Hard'];
const DEFAULT_ROLES = [
  'Backend Developer',
  'Frontend Developer',
  'Fullstack Developer',
  'Mobile Developer',
  'DevOps Engineer',
  'QA / Tester',
  'Data Engineer',
  'AI / ML Engineer',
];

function QuestionManagementPage() {
  const { t } = useTranslation('questionBank');

  const [questions, setQuestions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const [filters, setFilters] = useState({
    search: '',
    roleTarget: 'all',
    major: 'all',
    difficulty: 'all',
    includeDeleted: false,
  });

  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [totalItems, setTotalItems] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [pageSize, setPageSize] = useState(10);

  const [roleOptions, setRoleOptions] = useState(['all']);
  const [majorOptions, setMajorOptions] = useState(['all']);

  const [selectedQuestionIds, setSelectedQuestionIds] = useState([]);
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);

  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [questionToEdit, setQuestionToEdit] = useState(null);
  
  const [isAddModalOpen, setIsAddModalOpen] = useState(false);
  const [newQuestion, setNewQuestion] = useState({
    questionContent: '',
    suggestedAnswer: '',
    difficulty: 'Easy',
    roleTarget: '',
    major: '',
  });

  const [isImportModalOpen, setIsImportModalOpen] = useState(false);
  const [isImportCodingModalOpen, setIsImportCodingModalOpen] = useState(false);
  const [importFile, setImportFile] = useState(null);
  const [importing, setImporting] = useState(false);

  useEffect(() => {
    const loadFilters = async () => {
      try {
        const filtersData = await questionService.getAdminQuestionFilters();
        if (filtersData) {
          setMajorOptions(['all', ...(filtersData.majors || [])]);
          setRoleOptions(['all', ...(filtersData.roleTargets || [])]);
        }
      } catch (err) {
        console.error('Failed to load filters', err);
      }
    };
    loadFilters();
  }, []);

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(filters.search);
    }, 500);
    return () => clearTimeout(timer);
  }, [filters.search]);

  const fetchQuestions = useCallback(async () => {
    try {
      setLoading(true);
      const params = {
        pageNumber: currentPage,
        pageSize,
        keyword: debouncedSearch,
        roleTarget: filters.roleTarget,
        major: filters.major,
        difficulty: filters.difficulty,
        includeDeleted: filters.includeDeleted,
      };
      const result = await questionService.getAdminQuestions(params);
      if (result && result.items) {
        setQuestions(result.items);
        setTotalItems(result.totalItems || 0);
        setTotalPages(result.totalPages || 0);
      } else {
        setQuestions([]);
        setTotalItems(0);
        setTotalPages(0);
      }
    } catch (err) {
      setError(err.message || 'Unable to load questions');
    } finally {
      setLoading(false);
    }
  }, [currentPage, pageSize, debouncedSearch, filters.roleTarget, filters.major, filters.difficulty, filters.includeDeleted]);

  useEffect(() => {
    fetchQuestions();
  }, [fetchQuestions]);

  const handleFilterChange = (e) => {
    const { name, value, type, checked } = e.target;
    setFilters(prev => ({ ...prev, [name]: type === 'checkbox' ? checked : value }));
    setCurrentPage(1);
  };

  const handleSearchChange = (e) => {
    setFilters(prev => ({ ...prev, search: e.target.value }));
    setCurrentPage(1);
  };

  const handleClearFilters = () => {
    setFilters({ search: '', roleTarget: 'all', major: 'all', difficulty: 'all', includeDeleted: false });
    setCurrentPage(1);
  };

  const toggleSelectAll = () => {
    if (questions.length > 0 && selectedQuestionIds.length === questions.length) {
      setSelectedQuestionIds([]);
    } else {
      setSelectedQuestionIds(questions.map((q) => q.questionId));
    }
  };

  const toggleSelectQuestion = (questionId) => {
    setSelectedQuestionIds((prev) =>
      prev.includes(questionId)
        ? prev.filter((id) => id !== questionId)
        : [...prev, questionId]
    );
  };

  const openDeleteModal = (question = null) => {
    if (question) {
      if (!selectedQuestionIds.includes(question.questionId)) {
        setSelectedQuestionIds([question.questionId]);
      }
    }
    setIsDeleteModalOpen(true);
  };

  const closeDeleteModal = () => {
    setIsDeleteModalOpen(false);
  };

  const confirmDelete = async () => {
    if (selectedQuestionIds.length === 0) return;
    try {
      setDeleting(true);
      await Promise.all(selectedQuestionIds.map((id) => questionService.deleteAdminQuestion(id)));
      notify.success(`Đã xóa thành công ${selectedQuestionIds.length} câu hỏi.`);
      setSelectedQuestionIds([]);
      closeDeleteModal();
      fetchQuestions();
    } catch (err) {
      notify.error(err.message || 'Có lỗi xảy ra khi xóa câu hỏi.');
    } finally {
      setDeleting(false);
    }
  };

  const openEditModal = (question) => {
    setQuestionToEdit({
      ...question,
      expectedKeyPoints: question.expectedKeyPoints || question.suggestedAnswer || '',
    });
    setIsEditModalOpen(true);
  };

  const closeEditModal = () => {
    setIsEditModalOpen(false);
    setQuestionToEdit(null);
  };

  const confirmEdit = async () => {
    if (!questionToEdit) return;
    try {
      const defaultMajor = (majorOptions && majorOptions.find((m) => m !== 'all')) || 'Công nghệ thông tin';
      const keyPoints = questionToEdit.expectedKeyPoints ?? '';
      const payload = {
        ...questionToEdit,
        expectedKeyPoints: keyPoints,
        suggestedAnswer: questionToEdit.suggestedAnswer || keyPoints,
        major: questionToEdit.major && questionToEdit.major.trim() !== '' ? questionToEdit.major : defaultMajor,
      };
      await questionService.updateAdminQuestion(questionToEdit.questionId, payload);
      closeEditModal();
      fetchQuestions();
      notify.success('Cập nhật câu hỏi thành công.');
    } catch (err) {
      notify.error(err.message || 'Không thể cập nhật câu hỏi');
    }
  };

  const handleEditChange = (e) => {
    const { name, value } = e.target;
    setQuestionToEdit(prev => ({ ...prev, [name]: value }));
  };

  const openAddModal = () => setIsAddModalOpen(true);
  const closeAddModal = () => {
    setIsAddModalOpen(false);
    setNewQuestion({ questionContent: '', expectedKeyPoints: '', suggestedAnswer: '', difficulty: 'Easy', roleTarget: '', major: '' });
  };

  const handleAddChange = (e) => {
    const { name, value } = e.target;
    setNewQuestion(prev => ({ ...prev, [name]: value }));
  };

  const confirmAdd = async () => {
    try {
      const defaultMajor = (majorOptions && majorOptions.find((m) => m !== 'all')) || 'Công nghệ thông tin';
      const keyPoints = newQuestion.expectedKeyPoints ?? '';
      const payload = {
        ...newQuestion,
        expectedKeyPoints: keyPoints,
        suggestedAnswer: newQuestion.suggestedAnswer || keyPoints,
        major: newQuestion.major && newQuestion.major.trim() !== '' ? newQuestion.major : defaultMajor,
      };
      await questionService.createAdminQuestion(payload);
      closeAddModal();
      fetchQuestions();
      notify.success('Thêm câu hỏi mới thành công.');
    } catch (err) {
      notify.error(err.message || 'Không thể thêm câu hỏi');
    }
  };

  const openImportModal = () => setIsImportModalOpen(true);
  const closeImportModal = () => {
    setIsImportModalOpen(false);
    setImportFile(null);
  };
  
  const openImportCodingModal = () => setIsImportCodingModalOpen(true);
  const closeImportCodingModal = () => {
    setIsImportCodingModalOpen(false);
    setImportFile(null);
  };

  const handleImportFileChange = (e) => {
    if (e.target.files && e.target.files.length > 0) {
      const file = e.target.files[0];
      if (file.size > 5 * 1024 * 1024) {
        notify.warning(t('importSizeError', 'Kích thước file vượt quá 5MB. Vui lòng chọn file nhỏ hơn.'));
        e.target.value = '';
        return;
      }
      setImportFile(file);
    }
  };

  const confirmImport = async () => {
    if (!importFile) return;
    setImporting(true);
    try {
      await questionService.importQuestions(importFile);
      closeImportModal();
      fetchQuestions();
    } catch (err) {
      notify.error(err.message || 'Failed to import questions');
    } finally {
      setImporting(false);
    }
  };
  
  const confirmImportCoding = async () => {
    if (!importFile) return;
    setImporting(true);
    try {
      await codingService.importCodingQuestions(importFile);
      notify.success(t('importSuccess', 'Imported coding questions successfully!'));
      closeImportCodingModal();
    } catch (err) {
      notify.error(err.message || 'Failed to import coding questions');
    } finally {
      setImporting(false);
    }
  };

  const getPageNumbers = () => {
    const pages = [];
    const maxVisiblePages = 5;
    let startPage = Math.max(1, currentPage - Math.floor(maxVisiblePages / 2));
    let endPage = Math.min(totalPages, startPage + maxVisiblePages - 1);

    if (endPage - startPage + 1 < maxVisiblePages) {
      startPage = Math.max(1, endPage - maxVisiblePages + 1);
    }

    if (startPage > 1) pages.push(1);
    if (startPage > 2) pages.push('start-ellipsis');
    for (let page = startPage; page <= endPage; page += 1) {
      pages.push(page);
    }
    if (endPage < totalPages - 1) pages.push('end-ellipsis');
    if (endPage < totalPages) pages.push(totalPages);

    return pages;
  };

  const pageNumbers = getPageNumbers();

  const getDifficultyClass = (val) => {
    if (val === 0 || val === '0') return 'easy';
    if (val === 1 || val === '1') return 'medium';
    if (val === 2 || val === '2') return 'hard';
    return String(val || '').toLowerCase();
  };

  const getDifficultyLabel = (val) => {
    if (val === 0 || val === '0') return t('diffEasy', 'Easy');
    if (val === 1 || val === '1') return t('diffMedium', 'Medium');
    if (val === 2 || val === '2') return t('diffHard', 'Hard');
    return String(val || '');
  };

  return (
    <div className="question-management-page w-full animate-[fadeIn_0.5s_ease]">
      <style>{`
        @keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
        @keyframes cardEntrance {
          from { opacity: 0; transform: translateY(16px); }
          to { opacity: 1; transform: translateY(0); }
        }
        .table-row-animate {
          animation: cardEntrance 0.5s cubic-bezier(0.16, 1, 0.3, 1) backwards;
          animation-delay: var(--delay, 0ms);
        }
      `}</style>
      <div className="page-header">
        <div className="breadcrumb">
          <span>{t('breadcrumbAdmin', 'Admin')}</span>
          <span className="separator">/</span>
          <span aria-current="page">{t('breadcrumb', 'Questions')}</span>
        </div>
        <div className="header-top">
          <div className="title-section">
            <h1 className="page-title">{t('pageTitle', 'Interview Question Management')}</h1>
            <p className="page-description">
              {t('pageDescription', 'Manage the question bank by IT role, major, and difficulty.')}
            </p>
          </div>

          <div className="page-actions">
            <button type="button" className="btn-secondary" onClick={openImportCodingModal}>
              <FileInput size={16} />
              {t('importCodingExcel', 'Import Coding')}
            </button>
            <button type="button" className="btn-secondary" onClick={openImportModal}>
              <FileInput size={16} />
              {t('importExcel', 'Import Excel')}
            </button>
            <button type="button" className="btn-primary" onClick={openAddModal}>
              <Plus size={16} />
              {t('addQuestion', 'Add question')}
            </button>
          </div>
        </div>
      </div>

      <section className="filter-card">
        <div className="filter-row">
          <div className="filter-group search-group">
            <Search size={20} />
            <input
              type="text"
              name="search"
              className="search-input"
              value={filters.search}
              onChange={handleSearchChange}
              placeholder={t('searchPlaceholder', 'Search by keyword')}
            />
          </div>

          <select
            name="roleTarget"
            value={filters.roleTarget}
            onChange={handleFilterChange}
            className="filter-select"
          >
            {roleOptions.map((option) => (
              <option key={option} value={option}>
                {option === 'all' ? t('statusAll', 'All Roles') : option}
              </option>
            ))}
          </select>

          <select
            name="major"
            value={filters.major}
            onChange={handleFilterChange}
            className="filter-select"
          >
            {majorOptions.map((option) => (
              <option key={option} value={option}>
                {option === 'all' ? t('statusAll', 'All Majors') : option}
              </option>
            ))}
          </select>

          <select
            name="difficulty"
            value={filters.difficulty}
            onChange={handleFilterChange}
            className="filter-select"
          >
            {DIFFICULTY_OPTIONS.map((option) => (
              <option key={option} value={option}>
                {option === 'all' ? t('statusAll', 'All Difficulties') : t(`diff${option}`, option)}
              </option>
            ))}
          </select>

          <button type="button" className="btn-secondary btn-clear" onClick={handleClearFilters}>
            {t('clearFilters', 'Clear filters')}
          </button>
        </div>
      </section>

      <section className="table-card">
        <div className="table-header-row">
          <p className="table-summary">
            {t('tableSummary', 'Showing {{total}} questions', { total: totalItems })}
          </p>
          {selectedQuestionIds.length > 0 && (
            <button
              type="button"
              className="btn-danger"
              onClick={() => openDeleteModal()}
            >
              <Trash2 size={16} /> Xóa đã chọn ({selectedQuestionIds.length})
            </button>
          )}
        </div>

        {error && (
          <div className="error-message">
            <AlertCircle size={20} />
            <span>{error}</span>
          </div>
        )}

        <div className="table-scroll">
          <table className="question-table">
            <thead>
              <tr>
                <th style={{ width: '40px', textAlign: 'center' }}>
                  <input
                    type="checkbox"
                    className="question-checkbox"
                    checked={questions.length > 0 && selectedQuestionIds.length === questions.length}
                    onChange={toggleSelectAll}
                  />
                </th>
                <th style={{ width: '10%' }}>{t('tableId', 'ID')}</th>
                <th style={{ width: '38%' }}>{t('tableQuestion', 'Question')}</th>
                <th style={{ width: '20%' }}>{t('tableRole', 'Role Target')}</th>
                <th style={{ width: '15%' }}>{t('tableDifficulty', 'Difficulty')}</th>
                <th style={{ width: '13%', textAlign: 'center' }}>{t('tableActions', 'Actions')}</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan="6" style={{ textAlign: 'center', padding: '40px' }}>
                    {t('loading', 'Loading questions...')}
                  </td>
                </tr>
              ) : questions.length === 0 ? (
                <tr>
                  <td colSpan="6" style={{ textAlign: 'center', padding: '40px' }}>
                    {t('noResults', 'No questions found.')}
                  </td>
                </tr>
              ) : (
                questions.map((question, index) => (
                  <tr key={question.questionId} className="table-row-animate" style={{ '--delay': `${index * 40}ms` }}>
                    <td style={{ textAlign: 'center' }}>
                      <input
                        type="checkbox"
                        className="question-checkbox"
                        checked={selectedQuestionIds.includes(question.questionId)}
                        onChange={() => toggleSelectQuestion(question.questionId)}
                      />
                    </td>
                    <td>{question.questionCode || `Q-${question.questionId}`}</td>
                    <td>
                      <div className="question-content-preview">
                        {question.questionContent}
                      </div>
                    </td>
                    <td>{question.roleTarget}</td>
                    <td>
                      <span className={`status-badge status-${getDifficultyClass(question.difficulty)}`}>
                        {getDifficultyLabel(question.difficulty)}
                      </span>
                    </td>
                    <td>
                      <div className="action-buttons">
                        <button
                          type="button"
                          className="icon-button"
                          title={t('edit', 'Edit')}
                          onClick={() => openEditModal(question)}
                        >
                          <Edit3 size={18} />
                        </button>
                        <button
                          type="button"
                          className="icon-button danger"
                          title={t('delete', 'Delete')}
                          onClick={() => openDeleteModal(question)}
                        >
                          <Trash2 size={18} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination Bar */}
        <div className="pagination">
          <div className="pagination-info">
            <span>
              Hiển thị {totalItems === 0 ? 0 : (currentPage - 1) * pageSize + 1}-{Math.min(currentPage * pageSize, totalItems)} trên tổng số {totalItems} câu hỏi
            </span>
            <div className="page-size-selector">
              <label>Số lượng mỗi trang:</label>
              <select
                value={pageSize}
                onChange={(e) => {
                  setPageSize(Number(e.target.value));
                  setCurrentPage(1);
                }}
                className="page-size-select"
              >
                <option value={10}>10</option>
                <option value={20}>20</option>
                <option value={50}>50</option>
              </select>
            </div>
          </div>

          <div className="pagination-buttons">
            <div className="pagination-desktop">
              <button
                className="pagination-btn"
                type="button"
                disabled={currentPage === 1}
                onClick={() => setCurrentPage(1)}
                title="Trang đầu"
              >
                <ChevronsLeft size={18} />
              </button>
              <button
                className="pagination-btn"
                type="button"
                disabled={currentPage === 1}
                onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
                title="Trang trước"
              >
                <ChevronLeft size={18} />
              </button>

              {pageNumbers.map((button, index) =>
                button === 'start-ellipsis' || button === 'end-ellipsis' ? (
                  <span key={`ellipsis-${index}`} className="pagination-ellipsis">
                    ...
                  </span>
                ) : (
                  <button
                    key={button}
                    className={`pagination-btn ${currentPage === button ? 'active' : ''}`}
                    type="button"
                    onClick={() => setCurrentPage(button)}
                  >
                    {button}
                  </button>
                )
              )}

              <button
                className="pagination-btn"
                type="button"
                disabled={currentPage >= totalPages}
                onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
                title="Trang sau"
              >
                <ChevronRight size={18} />
              </button>
              <button
                className="pagination-btn"
                type="button"
                disabled={currentPage >= totalPages}
                onClick={() => setCurrentPage(totalPages)}
                title="Trang cuối"
              >
                <ChevronsRight size={18} />
              </button>
            </div>
          </div>
        </div>
      </section>

      {/* Delete Confirmation Modal */}
      {isDeleteModalOpen && (
        <div className="modal-backdrop">
          <div className="modal-card">
            <div className="modal-header">
              <h3 className="modal-title">{t('deleteConfirmTitle', 'Xóa câu hỏi')}</h3>
              <button type="button" className="btn-close" onClick={closeDeleteModal} disabled={deleting}>
                <X size={20} />
              </button>
            </div>
            <div className="modal-body">
              <p style={{ margin: '0 0 12px', fontWeight: 500 }}>
                Bạn có chắc chắn muốn xóa <strong>{selectedQuestionIds.length}</strong> câu hỏi đã chọn dưới đây không? Thao tác này không thể hoàn tác.
              </p>

              <div className="selected-questions-container">
                {questions
                  .filter((q) => selectedQuestionIds.includes(q.questionId))
                  .map((q) => (
                    <label key={q.questionId} className="selected-question-item">
                      <input
                        type="checkbox"
                        className="question-checkbox"
                        checked={selectedQuestionIds.includes(q.questionId)}
                        onChange={() => toggleSelectQuestion(q.questionId)}
                      />
                      <span className="selected-question-text">
                        <strong>{q.questionCode || `Q-${q.questionId}`}:</strong> {q.questionContent}
                      </span>
                    </label>
                  ))}
              </div>
            </div>
            <div className="modal-footer">
              <button type="button" className="btn-secondary" onClick={closeDeleteModal} disabled={deleting}>
                {t('cancel', 'Hủy')}
              </button>
              <button
                type="button"
                className="btn-danger"
                onClick={confirmDelete}
                disabled={selectedQuestionIds.length === 0 || deleting}
              >
                <Trash2 size={16} />
                {deleting ? 'Đang xóa...' : `Xóa (${selectedQuestionIds.length} câu hỏi)`}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Edit Modal */}
      {isEditModalOpen && questionToEdit && (
        <div className="modal-backdrop">
          <div className="modal-card edit-modal">
            <div className="modal-header">
              <h3 className="modal-title">{t('editQuestionTitle', 'Edit Question')}</h3>
              <button type="button" className="btn-close" onClick={closeEditModal}>
                <X size={20} />
              </button>
            </div>
            <div className="modal-body">
              <div className="modal-form-group">
                <label className="modal-label">{t('tableQuestion', 'Question Content')}</label>
                <textarea
                  name="questionContent"
                  className="modal-input textarea"
                  value={questionToEdit.questionContent || ''}
                  onChange={handleEditChange}
                  rows={4}
                />
              </div>
              <div className="modal-form-group">
                <label className="modal-label">Ý chính gợi ý (Expected Key Points / Tips)</label>
                <textarea
                  name="expectedKeyPoints"
                  className="modal-input textarea"
                  value={questionToEdit.expectedKeyPoints || ''}
                  onChange={handleEditChange}
                  rows={4}
                />
              </div>
              <div className="modal-form-row">
                <div className="modal-form-group">
                  <label className="modal-label">{t('tableDifficulty', 'Difficulty')}</label>
                  <select
                    name="difficulty"
                    className="modal-input"
                    value={questionToEdit.difficulty || 'Easy'}
                    onChange={handleEditChange}
                  >
                    {DIFFICULTY_OPTIONS.filter(d => d !== 'all').map(opt => <option key={opt} value={opt}>{t(`diff${opt}`, opt)}</option>)}
                  </select>
                </div>
                <div className="modal-form-group">
                  <label className="modal-label">{t('tableRole', 'Role Target')}</label>
                  <select
                    name="roleTarget"
                    className="modal-input"
                    value={questionToEdit.roleTarget || ''}
                    onChange={handleEditChange}
                  >
                    <option value="">-- Chọn vai trò --</option>
                    {Array.from(new Set([...DEFAULT_ROLES, ...roleOptions.filter(r => r !== 'all')])).map(role => (
                      <option key={role} value={role}>{role}</option>
                    ))}
                    {questionToEdit.roleTarget && !roleOptions.includes(questionToEdit.roleTarget) && !DEFAULT_ROLES.includes(questionToEdit.roleTarget) && (
                      <option value={questionToEdit.roleTarget}>{questionToEdit.roleTarget}</option>
                    )}
                  </select>
                </div>
              </div>
            </div>
            <div className="modal-footer">
              <button type="button" className="btn-secondary" onClick={closeEditModal}>
                {t('cancel', 'Cancel')}
              </button>
              <button type="button" className="btn-primary" onClick={confirmEdit}>
                {t('saveChanges', 'Save Changes')}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Add Modal */}
      {isAddModalOpen && (
        <div className="modal-backdrop">
          <div className="modal-card edit-modal">
            <div className="modal-header">
              <h3 className="modal-title">{t('addQuestionTitle', 'Add Question')}</h3>
              <button type="button" className="btn-close" onClick={closeAddModal}>
                <X size={20} />
              </button>
            </div>
            <div className="modal-body">
              <div className="modal-form-group">
                <label className="modal-label">{t('tableQuestion', 'Question Content')}</label>
                <textarea
                  name="questionContent"
                  className="modal-input textarea"
                  value={newQuestion.questionContent || ''}
                  onChange={handleAddChange}
                  rows={4}
                />
              </div>
              <div className="modal-form-group">
                <label className="modal-label">Ý chính gợi ý (Expected Key Points / Tips)</label>
                <textarea
                  name="expectedKeyPoints"
                  className="modal-input textarea"
                  value={newQuestion.expectedKeyPoints || ''}
                  onChange={handleAddChange}
                  rows={4}
                />
              </div>
              <div className="modal-form-row">
                <div className="modal-form-group">
                  <label className="modal-label">{t('tableDifficulty', 'Difficulty')}</label>
                  <select
                    name="difficulty"
                    className="modal-input"
                    value={newQuestion.difficulty || 'Easy'}
                    onChange={handleAddChange}
                  >
                    {DIFFICULTY_OPTIONS.filter(d => d !== 'all').map(opt => <option key={opt} value={opt}>{t(`diff${opt}`, opt)}</option>)}
                  </select>
                </div>
                <div className="modal-form-group">
                  <label className="modal-label">{t('tableRole', 'Role Target')}</label>
                  <select
                    name="roleTarget"
                    className="modal-input"
                    value={newQuestion.roleTarget || ''}
                    onChange={handleAddChange}
                  >
                    <option value="">-- Chọn vai trò --</option>
                    {Array.from(new Set([...DEFAULT_ROLES, ...roleOptions.filter(r => r !== 'all')])).map(role => (
                      <option key={role} value={role}>{role}</option>
                    ))}
                  </select>
                </div>
              </div>
            </div>
            <div className="modal-footer">
              <button type="button" className="btn-secondary" onClick={closeAddModal}>
                {t('cancel', 'Cancel')}
              </button>
              <button type="button" className="btn-primary" onClick={confirmAdd}>
                {t('saveChanges', 'Save Changes')}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Import Modal */}
      {isImportModalOpen && (
        <div className="modal-backdrop">
          <div className="modal-card">
            <div className="modal-header">
              <h3 className="modal-title">{t('importExcelTitle', 'Import Questions')}</h3>
              <button type="button" className="btn-close" onClick={closeImportModal} disabled={importing}>
                <X size={20} />
              </button>
            </div>
            <div className="modal-body">
              <p>{t('importExcelDesc', 'Upload an Excel file to bulk import questions.')}</p>
              <input type="file" accept=".xlsx, .xls" onChange={handleImportFileChange} />
            </div>
            <div className="modal-footer">
              <button type="button" className="btn-secondary" onClick={closeImportModal} disabled={importing}>
                {t('cancel', 'Cancel')}
              </button>
              <button type="button" className="btn-primary" onClick={confirmImport} disabled={!importFile || importing}>
                {importing ? t('importing', 'Importing...') : t('import', 'Import')}
              </button>
            </div>
          </div>
        </div>
      )}
      
      {/* Import Coding Modal */}
      {isImportCodingModalOpen && (
        <div className="modal-backdrop">
          <div className="modal-card">
            <div className="modal-header">
              <h3 className="modal-title">{t('importCodingTitle', 'Import Coding Questions')}</h3>
              <button type="button" className="btn-close" onClick={closeImportCodingModal} disabled={importing}>
                <X size={20} />
              </button>
            </div>
            <div className="modal-body">
              <p>{t('importCodingDesc', 'Upload an Excel file to bulk import coding questions.')}</p>
              <input type="file" accept=".xlsx, .xls" onChange={handleImportFileChange} />
            </div>
            <div className="modal-footer">
              <button type="button" className="btn-secondary" onClick={closeImportCodingModal} disabled={importing}>
                {t('cancel', 'Cancel')}
              </button>
              <button type="button" className="btn-primary" onClick={confirmImportCoding} disabled={!importFile || importing}>
                {importing ? t('importing', 'Importing...') : t('import', 'Import')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default QuestionManagementPage;
