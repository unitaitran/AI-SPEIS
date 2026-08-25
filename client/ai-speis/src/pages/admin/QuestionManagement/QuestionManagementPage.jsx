import React, { useEffect, useState, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Search,
  FileInput,
  Plus,
  Edit3,
  Trash2,
  RotateCcw,
  ArchiveRestore,
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
const EXPERIENCE_LEVEL_OPTIONS = [
  'Intern/Fresher',
  'Fresher/Junior',
  'Junior',
  'Junior/Middle',
  'Middle',
  'Middle/Senior',
  'Senior',
];
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
  const [isTrashView, setIsTrashView] = useState(false);
  const [deleteMode, setDeleteMode] = useState('soft');

  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [questionToEdit, setQuestionToEdit] = useState(null);
  
  const [isAddModalOpen, setIsAddModalOpen] = useState(false);
  const [newQuestion, setNewQuestion] = useState({
    questionContent: '',
    expectedKeyPoints: '',
    suggestedAnswer: '',
    difficulty: 'Easy',
    roleTarget: '',
    major: '',
    experienceLevel: 'Fresher/Junior',
    clarificationQuestion: '',
    followUp1: '',
    followUp2: '',
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
      const result = isTrashView
        ? await questionService.getAdminQuestionTrash(params)
        : await questionService.getAdminQuestions(params);
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
  }, [currentPage, pageSize, debouncedSearch, filters.roleTarget, filters.major, filters.difficulty, filters.includeDeleted, isTrashView]);

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

  const openDeleteModal = (question = null, mode = 'soft') => {
    if (question) {
      if (!selectedQuestionIds.includes(question.questionId)) {
        setSelectedQuestionIds([question.questionId]);
      }
    }
    setDeleteMode(mode);
    setIsDeleteModalOpen(true);
  };

  const closeDeleteModal = () => {
    setIsDeleteModalOpen(false);
  };

  const confirmDelete = async () => {
    if (selectedQuestionIds.length === 0) return;
    try {
      setDeleting(true);
      if (deleteMode === 'purge') {
        await Promise.all(selectedQuestionIds.map((id) => questionService.requestAdminQuestionPurge(id)));
        notify.success(t('purgeRequestSuccess', { count: selectedQuestionIds.length }));
      } else {
        await Promise.all(selectedQuestionIds.map((id) => questionService.deleteAdminQuestion(id)));
        notify.success(t('softDeleteSuccess', { count: selectedQuestionIds.length }));
      }
      setSelectedQuestionIds([]);
      closeDeleteModal();
      fetchQuestions();
    } catch (err) {
      notify.error(err.message || t('questionDeleteError'));
    } finally {
      setDeleting(false);
    }
  };

  const handleRestore = async (questionId) => {
    try {
      setDeleting(true);
      await questionService.restoreAdminQuestion(questionId);
      notify.success(t('restoreSuccess'));
      fetchQuestions();
    } catch (err) {
      notify.error(err.message || t('restoreError'));
    } finally {
      setDeleting(false);
    }
  };

  const toggleTrashView = () => {
    setSelectedQuestionIds([]);
    setCurrentPage(1);
    setIsTrashView((current) => !current);
  };

  const openEditModal = (question) => {
    setQuestionToEdit({
      ...question,
      expectedKeyPoints: question.expectedKeyPoints || question.suggestedAnswer || '',
      experienceLevel: question.experienceLevel || 'Fresher/Junior',
      clarificationQuestion: question.clarificationQuestion || '',
      followUp1: question.followUp1 || '',
      followUp2: question.followUp2 || '',
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
        experienceLevel: questionToEdit.experienceLevel || 'Fresher/Junior',
        clarificationQuestion: questionToEdit.clarificationQuestion || '',
        followUp1: questionToEdit.followUp1 || '',
        followUp2: questionToEdit.followUp2 || '',
      };
      await questionService.updateAdminQuestion(questionToEdit.questionId, payload);
      closeEditModal();
      fetchQuestions();
      notify.success(t('updateQuestionSuccess', 'Cập nhật câu hỏi thành công.'));
    } catch (err) {
      notify.error(err.message || t('updateQuestionError', 'Không thể cập nhật câu hỏi'));
    }
  };

  const handleEditChange = (e) => {
    const { name, value } = e.target;
    setQuestionToEdit(prev => ({ ...prev, [name]: value }));
  };

  const openAddModal = () => setIsAddModalOpen(true);
  const closeAddModal = () => {
    setIsAddModalOpen(false);
    setNewQuestion({
      questionContent: '',
      expectedKeyPoints: '',
      suggestedAnswer: '',
      difficulty: 'Easy',
      roleTarget: '',
      major: '',
      experienceLevel: 'Fresher/Junior',
      clarificationQuestion: '',
      followUp1: '',
      followUp2: '',
    });
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
        experienceLevel: newQuestion.experienceLevel || 'Fresher/Junior',
        clarificationQuestion: newQuestion.clarificationQuestion || '',
        followUp1: newQuestion.followUp1 || '',
        followUp2: newQuestion.followUp2 || '',
      };
      await questionService.createAdminQuestion(payload);
      closeAddModal();
      fetchQuestions();
      notify.success(t('createQuestionSuccess', 'Thêm câu hỏi mới thành công.'));
    } catch (err) {
      notify.error(err.message || t('createQuestionError', 'Không thể thêm câu hỏi'));
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
            <h1 className="page-title">{isTrashView ? t('trashTitle') : t('pageTitle', 'Interview Question Management')}</h1>
            <p className="page-description">
              {isTrashView
                ? t('trashDescription')
                : t('pageDescription', 'Manage the question bank by IT role, major, and difficulty.')}
            </p>
          </div>

          <div className="page-actions">
            <button type="button" className="btn-secondary" onClick={toggleTrashView}>
              {isTrashView ? <ArchiveRestore size={16} /> : <Trash2 size={16} />}
              {isTrashView ? t('backToQuestionBank') : t('openTrash')}
            </button>
            {!isTrashView && <>
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
            </>}
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
          {!isTrashView && selectedQuestionIds.length > 0 && (
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
                      {isTrashView && (
                        <div className="trash-question-status">
                          {question.purgeStatus === 'Requested'
                            ? (question.lastPurgeError || t('purgeRequestedStatus'))
                            : t('trashAvailableStatus')}
                        </div>
                      )}
                    </td>
                    <td>{question.roleTarget}</td>
                    <td>
                      <span className={`status-badge status-${getDifficultyClass(question.difficulty)}`}>
                        {getDifficultyLabel(question.difficulty)}
                      </span>
                    </td>
                    <td>
                      <div className="action-buttons">
                        {isTrashView ? <>
                          <button
                            type="button"
                            className="icon-button"
                            title={t('restore')}
                            disabled={deleting}
                            onClick={() => handleRestore(question.questionId)}
                          >
                            <RotateCcw size={18} />
                          </button>
                          <button
                            type="button"
                            className="icon-button danger"
                            title={t('requestPurge')}
                            disabled={deleting || question.purgeStatus === 'Requested'}
                            onClick={() => openDeleteModal(question, 'purge')}
                          >
                            <Trash2 size={18} />
                          </button>
                        </> : <>
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
                        </>}
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
              <h3 className="modal-title">
                {deleteMode === 'purge' ? t('purgeConfirmTitle') : t('moveToTrashTitle')}
              </h3>
              <button type="button" className="btn-close" onClick={closeDeleteModal} disabled={deleting}>
                <X size={20} />
              </button>
            </div>
            <div className="modal-body">
              <p style={{ margin: '0 0 12px', fontWeight: 500 }}>
                {deleteMode === 'purge'
                  ? t('purgeConfirmText')
                  : t('moveToTrashConfirmText', { count: selectedQuestionIds.length })}
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
                {deleting
                  ? t('processing')
                  : deleteMode === 'purge'
                    ? t('purgeRequestAction')
                    : t('moveToTrashAction', { count: selectedQuestionIds.length })}
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
                <label className="modal-label">{t('tableQuestion', 'Question Content')} *</label>
                <textarea
                  name="questionContent"
                  className="modal-input textarea"
                  value={questionToEdit.questionContent || ''}
                  onChange={handleEditChange}
                  rows={3}
                  placeholder={t('questionContentPlaceholder', 'Nhập nội dung câu hỏi...')}
                />
              </div>
              <div className="modal-form-group">
                <label className="modal-label">{t('expectedKeyPointsLabel', 'Ý chính gợi ý (Expected Key Points / Tips)')}</label>
                <textarea
                  name="expectedKeyPoints"
                  className="modal-input textarea"
                  value={questionToEdit.expectedKeyPoints || ''}
                  onChange={handleEditChange}
                  rows={3}
                  placeholder={t('expectedKeyPointsPlaceholder', 'Các ý chính câu trả lời cần đạt được...')}
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
                    <option value="">{t('selectRolePlaceholder', '-- Chọn vai trò --')}</option>
                    {Array.from(new Set([...DEFAULT_ROLES, ...roleOptions.filter(r => r !== 'all')])).map(role => (
                      <option key={role} value={role}>{role}</option>
                    ))}
                    {questionToEdit.roleTarget && !roleOptions.includes(questionToEdit.roleTarget) && !DEFAULT_ROLES.includes(questionToEdit.roleTarget) && (
                      <option value={questionToEdit.roleTarget}>{questionToEdit.roleTarget}</option>
                    )}
                  </select>
                </div>
                <div className="modal-form-group">
                  <label className="modal-label">{t('experienceLevelLabel', 'Cấp độ kinh nghiệm (Experience Level)')}</label>
                  <select
                    name="experienceLevel"
                    className="modal-input"
                    value={questionToEdit.experienceLevel || 'Fresher/Junior'}
                    onChange={handleEditChange}
                  >
                    {EXPERIENCE_LEVEL_OPTIONS.map(lvl => (
                      <option key={lvl} value={lvl}>{lvl}</option>
                    ))}
                  </select>
                </div>
              </div>
              <div className="modal-form-group">
                <label className="modal-label">{t('clarificationQuestionLabel', 'Câu hỏi làm rõ (Clarification Question)')}</label>
                <input
                  type="text"
                  name="clarificationQuestion"
                  className="modal-input"
                  value={questionToEdit.clarificationQuestion || ''}
                  onChange={handleEditChange}
                  placeholder={t('clarificationQuestionPlaceholder', 'Câu hỏi làm rõ khi ứng viên trả lời mơ hồ...')}
                />
              </div>
              <div className="modal-form-row">
                <div className="modal-form-group">
                  <label className="modal-label">{t('followUp1Label', 'Câu hỏi đào sâu 1 (Follow-up 1)')}</label>
                  <input
                    type="text"
                    name="followUp1"
                    className="modal-input"
                    value={questionToEdit.followUp1 || ''}
                    onChange={handleEditChange}
                    placeholder={t('followUp1Placeholder', 'Câu hỏi mở rộng hoặc probe sâu hơn...')}
                  />
                </div>
                <div className="modal-form-group">
                  <label className="modal-label">{t('followUp2Label', 'Câu hỏi đào sâu 2 (Follow-up 2)')}</label>
                  <input
                    type="text"
                    name="followUp2"
                    className="modal-input"
                    value={questionToEdit.followUp2 || ''}
                    onChange={handleEditChange}
                    placeholder={t('followUp2Placeholder', 'Câu hỏi tình huống thử thách nâng cao...')}
                  />
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
                <label className="modal-label">{t('tableQuestion', 'Question Content')} *</label>
                <textarea
                  name="questionContent"
                  className="modal-input textarea"
                  value={newQuestion.questionContent || ''}
                  onChange={handleAddChange}
                  rows={3}
                  placeholder={t('questionContentPlaceholder', 'Nhập nội dung câu hỏi...')}
                />
              </div>
              <div className="modal-form-group">
                <label className="modal-label">{t('expectedKeyPointsLabel', 'Ý chính gợi ý (Expected Key Points / Tips)')}</label>
                <textarea
                  name="expectedKeyPoints"
                  className="modal-input textarea"
                  value={newQuestion.expectedKeyPoints || ''}
                  onChange={handleAddChange}
                  rows={3}
                  placeholder={t('expectedKeyPointsPlaceholder', 'Các ý chính câu trả lời cần đạt được...')}
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
                    <option value="">{t('selectRolePlaceholder', '-- Chọn vai trò --')}</option>
                    {Array.from(new Set([...DEFAULT_ROLES, ...roleOptions.filter(r => r !== 'all')])).map(role => (
                      <option key={role} value={role}>{role}</option>
                    ))}
                  </select>
                </div>
                <div className="modal-form-group">
                  <label className="modal-label">{t('experienceLevelLabel', 'Cấp độ kinh nghiệm (Experience Level)')}</label>
                  <select
                    name="experienceLevel"
                    className="modal-input"
                    value={newQuestion.experienceLevel || 'Fresher/Junior'}
                    onChange={handleAddChange}
                  >
                    {EXPERIENCE_LEVEL_OPTIONS.map(lvl => (
                      <option key={lvl} value={lvl}>{lvl}</option>
                    ))}
                  </select>
                </div>
              </div>
              <div className="modal-form-group">
                <label className="modal-label">{t('clarificationQuestionLabel', 'Câu hỏi làm rõ (Clarification Question)')}</label>
                <input
                  type="text"
                  name="clarificationQuestion"
                  className="modal-input"
                  value={newQuestion.clarificationQuestion || ''}
                  onChange={handleAddChange}
                  placeholder={t('clarificationQuestionPlaceholder', 'Câu hỏi làm rõ khi ứng viên trả lời mơ hồ...')}
                />
              </div>
              <div className="modal-form-row">
                <div className="modal-form-group">
                  <label className="modal-label">{t('followUp1Label', 'Câu hỏi đào sâu 1 (Follow-up 1)')}</label>
                  <input
                    type="text"
                    name="followUp1"
                    className="modal-input"
                    value={newQuestion.followUp1 || ''}
                    onChange={handleAddChange}
                    placeholder={t('followUp1Placeholder', 'Câu hỏi mở rộng hoặc probe sâu hơn...')}
                  />
                </div>
                <div className="modal-form-group">
                  <label className="modal-label">{t('followUp2Label', 'Câu hỏi đào sâu 2 (Follow-up 2)')}</label>
                  <input
                    type="text"
                    name="followUp2"
                    className="modal-input"
                    value={newQuestion.followUp2 || ''}
                    onChange={handleAddChange}
                    placeholder={t('followUp2Placeholder', 'Câu hỏi tình huống thử thách nâng cao...')}
                  />
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
