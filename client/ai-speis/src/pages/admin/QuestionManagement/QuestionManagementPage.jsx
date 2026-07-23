import React, { useEffect, useMemo, useState, useCallback } from 'react';
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
  AlertCircle,
  ArrowUpDown,
} from 'lucide-react';
import { questionService } from '../../../services/QuestionService';
import { codingService } from '../../../services/codingService';
import notify from '../../../utils/notification';
import '../../../styles/admin/QuestionManagementPage.css';

const ALL_OPTION = 'all';

const normalizeAdminDifficulty = (value) => {
  const text = String(value ?? '').trim().toLowerCase();
  if (text === '1' || text === 'easy' || text === 'dễ') return 1;
  if (text === '2' || text === 'medium' || text === 'trung bình') return 2;
  if (text === '3' || text === 'hard' || text === 'khó') return 3;
  if (text === '0') return 1;

  const numeric = Number(value);
  if ([1, 2, 3].includes(numeric)) return numeric;
  return null;
};

const getDifficultyMeta = (value, t) => {
  const normalized = normalizeAdminDifficulty(value);
  if (normalized === 1) return { label: t('diffEasy', 'Dễ'), badge: 'active', value: '1' };
  if (normalized === 2) return { label: t('diffMedium', 'Trung bình'), badge: 'draft', value: '2' };
  if (normalized === 3) return { label: t('diffHard', 'Khó'), badge: 'danger', value: '3' };
  return { label: String(value ?? ''), badge: 'inactive', value: String(value ?? '') };
};

const collectDistinct = (values) => [...new Set(values.filter(Boolean).map((value) => String(value).trim()).filter(Boolean))].sort((a, b) => a.localeCompare(b));

const getDisplayText = (...values) => {
  for (const value of values) {
    if (value === null || value === undefined) continue;
    if (typeof value === 'string') {
      const trimmed = value.trim();
      if (trimmed) return trimmed;
      continue;
    }
    if (typeof value === 'number') {
      return String(value);
    }
    if (typeof value === 'boolean') {
      return String(value);
    }
    if (typeof value === 'object') {
      const nested = value.name ?? value.title ?? value.label ?? value.code ?? value.value;
      const resolved = getDisplayText(nested);
      if (resolved) return resolved;
    }
  }

  return '';
};

const getQuestionCode = (question) => getDisplayText(question?.questionCode, question?.code, question?.questionId ? `Q-${question.questionId}` : '', question?.id ? `Q-${question.id}` : '');

const getQuestionCodeForMeta = (question) => getDisplayText(question?.code, question?.questionCode, question?.questionId ? `Q${question.questionId}` : '', question?.id ? `Q${question.id}` : '');

const getQuestionTypeText = (question) => getDisplayText(
  question?.questionType,
  question?.interviewType,
  question?.questionTypeName,
  question?.interviewTypeName,
  question?.questionType?.name,
  question?.interviewType?.name
);

const getRoleText = (question) => getDisplayText(
  question?.role?.name,
  question?.roleName,
  question?.roleTarget?.name,
  question?.roleTarget,
  question?.role,
  question?.roleTargetName
);

const getTechStackText = (question) => getDisplayText(
  question?.techStack?.name,
  question?.techStackName,
  question?.techStack,
  question?.skill?.name,
  question?.skill,
  question?.techStack?.displayName
);

const getCategoryText = (question) => getDisplayText(
  question?.category?.name,
  question?.categoryName,
  question?.major?.name,
  question?.major,
  question?.specialization?.name,
  question?.specialization
);

const getStatusMeta = (value) => {
  const text = getDisplayText(value).toLowerCase();
  if (!text) return { label: '', badge: 'inactive' };
  if (['active', '1', 'true', 'enabled'].includes(text)) return { label: 'Active', badge: 'active' };
  if (['inactive', '0', 'false', 'disabled'].includes(text)) return { label: 'Inactive', badge: 'inactive' };
  return { label: getDisplayText(value), badge: 'inactive' };
};

function QuestionManagementPage() {
  const { t } = useTranslation('questionBank');

  const [questions, setQuestions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const [filters, setFilters] = useState({
    search: '',
    roleTarget: ALL_OPTION,
    major: ALL_OPTION,
    difficulty: ALL_OPTION,
    techStack: ALL_OPTION,
    interviewType: ALL_OPTION,
    tags: ALL_OPTION,
    status: ALL_OPTION,
  });

  const [filterOptions, setFilterOptions] = useState({
    roleTargets: [ALL_OPTION],
    majors: [ALL_OPTION],
    difficulties: [ALL_OPTION],
    techStacks: [ALL_OPTION],
    interviewTypes: [ALL_OPTION],
    tags: [ALL_OPTION],
    statuses: [ALL_OPTION],
  });

  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [totalItems, setTotalItems] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [sortBy, setSortBy] = useState('createdAt');
  const [sortDirection, setSortDirection] = useState('desc');
  const pageSize = 10;

  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [questionToDelete, setQuestionToDelete] = useState(null);

  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [questionToEdit, setQuestionToEdit] = useState(null);

  const [isAddModalOpen, setIsAddModalOpen] = useState(false);
  const [newQuestion, setNewQuestion] = useState({
    questionContent: '',
    suggestedAnswer: '',
    difficulty: '',
    roleTarget: '',
    major: '',
    questionType: '',
    techStack: '',
    tags: '',
    status: 'Active',
  });

  const [isImportModalOpen, setIsImportModalOpen] = useState(false);
  const [isImportCodingModalOpen, setIsImportCodingModalOpen] = useState(false);
  const [importFile, setImportFile] = useState(null);
  const [importing, setImporting] = useState(false);

  const loadFilterOptions = useCallback(async () => {
    try {
      const allRows = [];
      let pageNumber = 1;
      let totalPagesForFilters = 1;

      do {
        const result = await questionService.getAdminQuestions({
          pageNumber,
          pageSize: 100,
          sortBy: 'questionId',
          sortDirection: 'asc',
        });

        allRows.push(...(result?.items || []));
        totalPagesForFilters = result?.totalPages || 1;
        pageNumber += 1;
      } while (pageNumber <= totalPagesForFilters);

      const roleTargets = collectDistinct(allRows.map((item) => getRoleText(item)));
      const majors = collectDistinct(allRows.map((item) => getCategoryText(item)));
      const techStacks = collectDistinct(allRows.map((item) => getTechStackText(item)));
      const interviewTypes = collectDistinct(allRows.map((item) => getQuestionTypeText(item)));
      const tags = collectDistinct(allRows.map((item) => item.tags || item.keywordTags));
      const difficulties = collectDistinct(allRows.map((item) => getDifficultyMeta(item.difficulty, t).value));
      const statuses = collectDistinct(allRows.map((item) => getStatusMeta(item.status).label));

      setFilterOptions({
        roleTargets: [ALL_OPTION, ...roleTargets],
        majors: [ALL_OPTION, ...majors],
        difficulties: [ALL_OPTION, ...difficulties],
        techStacks: [ALL_OPTION, ...techStacks],
        interviewTypes: [ALL_OPTION, ...interviewTypes],
        tags: [ALL_OPTION, ...tags],
        statuses: [ALL_OPTION, ...statuses],
      });
    } catch (err) {
      console.error('Failed to load filters', err);
    }
  }, [t]);

  useEffect(() => {
    loadFilterOptions();
  }, [loadFilterOptions]);

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(filters.search.trim());
    }, 500);

    return () => clearTimeout(timer);
  }, [filters.search]);

  const fetchQuestions = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);

      const result = await questionService.getAdminQuestions({
        pageNumber: currentPage,
        pageSize,
        keyword: debouncedSearch,
        roleTarget: filters.roleTarget,
        major: filters.major,
        difficulty: filters.difficulty,
        techStack: filters.techStack,
        interviewType: filters.interviewType,
        tags: filters.tags,
        status: filters.status,
        sortBy,
        sortDirection,
      });

      setQuestions(result?.items || []);
      setTotalItems(result?.totalItems || 0);
      setTotalPages(result?.totalPages || 0);
    } catch (err) {
      setError(err.message || t('loadFailed', 'Unable to load questions'));
      setQuestions([]);
      setTotalItems(0);
      setTotalPages(0);
    } finally {
      setLoading(false);
    }
  }, [
    currentPage,
    pageSize,
    debouncedSearch,
    filters.roleTarget,
    filters.major,
    filters.difficulty,
    filters.techStack,
    filters.interviewType,
    filters.tags,
    filters.status,
    sortBy,
    sortDirection,
    t,
  ]);

  useEffect(() => {
    fetchQuestions();
  }, [fetchQuestions]);

  useEffect(() => {
    if (totalPages > 0 && currentPage > totalPages) {
      setCurrentPage(totalPages);
    }
  }, [currentPage, totalPages]);

  const activeDifficultyOptions = useMemo(
    () => filterOptions.difficulties.filter((option) => option !== ALL_OPTION),
    [filterOptions.difficulties]
  );

  useEffect(() => {
    if (!newQuestion.difficulty && activeDifficultyOptions.length > 0) {
      setNewQuestion((prev) => ({ ...prev, difficulty: activeDifficultyOptions[0] }));
    }
  }, [newQuestion.difficulty, activeDifficultyOptions]);

  const hasActiveFilters = useMemo(() => {
    return Boolean(
      debouncedSearch
      || filters.roleTarget !== ALL_OPTION
      || filters.major !== ALL_OPTION
      || filters.difficulty !== ALL_OPTION
      || filters.techStack !== ALL_OPTION
      || filters.interviewType !== ALL_OPTION
      || filters.tags !== ALL_OPTION
      || filters.status !== ALL_OPTION
    );
  }, [
    debouncedSearch,
    filters.roleTarget,
    filters.major,
    filters.difficulty,
    filters.techStack,
    filters.interviewType,
    filters.tags,
    filters.status,
  ]);

  const handleFilterChange = (e) => {
    const { name, value } = e.target;
    setFilters((prev) => ({ ...prev, [name]: value }));
    setCurrentPage(1);
  };

  const handleSearchChange = (e) => {
    setFilters((prev) => ({ ...prev, search: e.target.value }));
    setCurrentPage(1);
  };

  const handleClearFilters = () => {
    setFilters({
      search: '',
      roleTarget: ALL_OPTION,
      major: ALL_OPTION,
      difficulty: ALL_OPTION,
      techStack: ALL_OPTION,
      interviewType: ALL_OPTION,
      tags: ALL_OPTION,
      status: ALL_OPTION,
    });
    setSortBy('createdAt');
    setSortDirection('desc');
    setCurrentPage(1);
  };

  const handleSort = (field) => {
    if (sortBy !== field) {
      setSortBy(field);
      setSortDirection('asc');
    } else {
      setSortDirection((prev) => (prev === 'asc' ? 'desc' : 'asc'));
    }
    setCurrentPage(1);
  };

  const openDeleteModal = (question) => {
    setQuestionToDelete(question);
    setIsDeleteModalOpen(true);
  };

  const closeDeleteModal = () => {
    setIsDeleteModalOpen(false);
    setQuestionToDelete(null);
  };

  const confirmDelete = async () => {
    if (!questionToDelete) return;

    try {
      await questionService.deleteAdminQuestion(questionToDelete.questionId);
      closeDeleteModal();
      await fetchQuestions();
      await loadFilterOptions();
    } catch (err) {
      notify.error(err.message || t('deleteFailed', 'Failed to delete question'));
    }
  };

  const openEditModal = (question) => {
    setQuestionToEdit({
      ...question,
      techStack: question.techStack || '',
      questionType: question.questionType || '',
      tags: question.tags || '',
      status: question.status || 'Active',
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
      await questionService.updateAdminQuestion(questionToEdit.questionId, questionToEdit);
      closeEditModal();
      await fetchQuestions();
      await loadFilterOptions();
    } catch (err) {
      notify.error(err.message || t('updateFailed', 'Failed to update question'));
    }
  };

  const handleEditChange = (e) => {
    const { name, value } = e.target;
    setQuestionToEdit((prev) => ({ ...prev, [name]: value }));
  };

  const openAddModal = () => setIsAddModalOpen(true);

  const closeAddModal = () => {
    setIsAddModalOpen(false);
    setNewQuestion({
      questionContent: '',
      suggestedAnswer: '',
      difficulty: activeDifficultyOptions[0] || '',
      roleTarget: '',
      major: '',
      questionType: '',
      techStack: '',
      tags: '',
      status: 'Active',
    });
  };

  const handleAddChange = (e) => {
    const { name, value } = e.target;
    setNewQuestion((prev) => ({ ...prev, [name]: value }));
  };

  const confirmAdd = async () => {
    try {
      await questionService.createAdminQuestion(newQuestion);
      closeAddModal();
      await fetchQuestions();
      await loadFilterOptions();
    } catch (err) {
      notify.error(err.message || t('addFailed', 'Failed to add question'));
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
    if (!e.target.files || e.target.files.length === 0) {
      return;
    }

    const file = e.target.files[0];

    if (file.size > 5 * 1024 * 1024) {
      notify.warning(t('importSizeError', 'File size exceeds 5MB. Please choose a smaller file.'));
      e.target.value = '';
      return;
    }

    setImportFile(file);
  };

  const confirmImport = async () => {
    if (!importFile) return;

    setImporting(true);
    try {
      await questionService.importQuestions(importFile);
      closeImportModal();
      await fetchQuestions();
      await loadFilterOptions();
    } catch (err) {
      notify.error(err.message || t('importFailed', 'Failed to import questions'));
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
      await fetchQuestions();
      await loadFilterOptions();
    } catch (err) {
      notify.error(err.message || t('importCodingFailed', 'Failed to import coding questions'));
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

  const getDifficultyClass = (value) => {
    const norm = normalizeAdminDifficulty(value);
    if (norm === 1) return 'active';
    if (norm === 2) return 'draft';
    if (norm === 3) return 'danger';
    return 'inactive';
  };

  const getDifficultyLabel = (value) => {
    return getDifficultyMeta(value, t).label;
  };

  const getStatusClass = (status) => {
    return getStatusMeta(status).badge;
  };

  const formatCreatedDate = (value) => {
    if (!value) return '';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return '';

    return date.toLocaleString(undefined, {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    });
  };

  const renderSortIcon = (field) => (
    <ArrowUpDown
      size={14}
      style={{ opacity: sortBy === field ? 1 : 0.5 }}
      aria-hidden="true"
    />
  );

  const questionCountLabel = t('tableSummary', 'Showing {{total}} questions', { total: totalItems });
  const difficultyFilterOptions = useMemo(() => {
    const options = activeDifficultyOptions.map((option) => ({
      value: option,
      label: getDifficultyMeta(option, t).label,
    }));

    return options.length > 0
      ? options
      : [
          { value: '1', label: t('diffEasy', 'D?') },
          { value: '2', label: t('diffMedium', 'Trung b�nh') },
          { value: '3', label: t('diffHard', 'Kh�') },
        ];
  }, [activeDifficultyOptions, t]);

  const formDifficultyOptions = difficultyFilterOptions;

  return (
    <div className="question-management-page w-full animate-[fadeIn_0.5s_ease]">
      <style>{`\n        @keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }\n        @keyframes cardEntrance {\n          from { opacity: 0; transform: translateY(16px); }\n          to { opacity: 1; transform: translateY(0); }\n        }\n        .table-row-animate {\n          animation: cardEntrance 0.5s cubic-bezier(0.16, 1, 0.3, 1) backwards;\n          animation-delay: var(--delay, 0ms);\n        }\n      `}</style>

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
              {t('pageDescription', 'Manage the question bank by IT role, tech stack, difficulty, and interview type.')}
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
        <div className="filter-search-row">
          <div className="filter-group search-group">
            <Search size={18} />
            <input
              type="text"
              name="search"
              className="search-input"
              value={filters.search}
              onChange={handleSearchChange}
              placeholder={t('searchPlaceholder', 'Search by question content, code, or source')}
            />
          </div>
        </div>

        <div className="filter-controls-row">
          <select name="roleTarget" value={filters.roleTarget} onChange={handleFilterChange} className="filter-select">
            {filterOptions.roleTargets.map((option) => (
              <option key={option} value={option}>
                {option === ALL_OPTION ? t('statusAll', 'All') : option}
              </option>
            ))}
          </select>

          <select name="techStack" value={filters.techStack} onChange={handleFilterChange} className="filter-select">
            {filterOptions.techStacks.map((option) => (
              <option key={option} value={option}>
                {option === ALL_OPTION ? t('statusAll', 'All') : option}
              </option>
            ))}
          </select>

          <select name="difficulty" value={filters.difficulty} onChange={handleFilterChange} className="filter-select">
            <option value={ALL_OPTION}>{t('statusAll', 'All')}</option>
            {difficultyFilterOptions.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>

          <select name="interviewType" value={filters.interviewType} onChange={handleFilterChange} className="filter-select">
            {filterOptions.interviewTypes.map((option) => (
              <option key={option} value={option}>
                {option === ALL_OPTION ? t('statusAll', 'All') : option}
              </option>
            ))}
          </select>

          <button type="button" className="btn-secondary btn-clear" onClick={handleClearFilters}>
            {t('clearFilters', 'Reset Filter')}
          </button>
        </div>
      </section>
      <section className="table-card">
        <div className="table-header-row">
          <div className="table-summary-block">
            <p className="table-summary">{questionCountLabel}</p>
          </div>
        </div>

        {error && (
          <div className="error-message">
            <AlertCircle size={18} />
            <span>{error}</span>
          </div>
        )}

        <div className="table-scroll">
          <table className="question-table">
            <colgroup>
              <col style={{ width: '90px' }} />
              <col style={{ width: '34%' }} />
              <col style={{ width: '150px' }} />
              <col style={{ width: '150px' }} />
              <col style={{ width: '150px' }} />
              <col style={{ width: '120px' }} />
              <col style={{ width: '120px' }} />
              <col style={{ width: '110px' }} />
            </colgroup>
            <thead>
              <tr>
                <th className="header-id">
                  <button type="button" className="sort-button" onClick={() => handleSort('questionId')}>
                    {t('tableId', 'ID')} {renderSortIcon('questionId')}
                  </button>
                </th>
                <th className="header-question">
                  <button type="button" className="sort-button" onClick={() => handleSort('questionContent')}>
                    {t('tableQuestion', 'Question')} {renderSortIcon('questionContent')}
                  </button>
                </th>
                <th className="header-role">
                  <button type="button" className="sort-button" onClick={() => handleSort('roleTarget')}>
                    {t('tableRole', 'Role')} {renderSortIcon('roleTarget')}
                  </button>
                </th>
                <th className="header-tech-stack">
                  <button type="button" className="sort-button" onClick={() => handleSort('techStack')}>
                    {t('tableTechStack', 'Tech Stack')} {renderSortIcon('techStack')}
                  </button>
                </th>
                <th className="header-category">
                  <button type="button" className="sort-button" onClick={() => handleSort('major')}>
                    {t('tableCategory', 'Category / Major')} {renderSortIcon('major')}
                  </button>
                </th>
                <th className="header-difficulty">
                  <button type="button" className="sort-button" onClick={() => handleSort('difficulty')}>
                    {t('tableDifficulty', 'Difficulty')} {renderSortIcon('difficulty')}
                  </button>
                </th>
                <th className="header-status">
                  <button type="button" className="sort-button" onClick={() => handleSort('status')}>
                    {t('tableStatus', 'Status')} {renderSortIcon('status')}
                  </button>
                </th>
                <th className="actions-header">{t('tableActions', 'Actions')}</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan="8" style={{ textAlign: 'center', padding: '40px' }}>
                    {t('loading', 'Loading...')}
                  </td>
                </tr>
              ) : questions.length === 0 ? (
                <tr>
                  <td colSpan="8" style={{ textAlign: 'center', padding: '40px' }}>
                    {hasActiveFilters
                      ? t('noResults', 'No questions found.')
                      : t('emptyData', 'No questions available in the database yet.')}
                  </td>
                </tr>
              ) : (
                questions.map((question, index) => (
                  <tr key={question.questionId} className="table-row-animate" style={{ '--delay': `${index * 40}ms` }}>
                    <td>{getQuestionCode(question)}</td>
                    <td className="question-cell">
                      <div className="question-cell-inner">
                        <div className="question-content-preview">{getDisplayText(question.questionContent)}</div>
                        <div className="question-meta-row">
                          <span className="question-code-text">{getQuestionCodeForMeta(question)}</span>
                          {getQuestionTypeText(question) && (
                            <span className="question-type-chip">{getQuestionTypeText(question)}</span>
                          )}
                        </div>
                      </div>
                    </td>
                    <td>{getRoleText(question)}</td>
                    <td>{getTechStackText(question)}</td>
                    <td>{getCategoryText(question)}</td>
                    <td>
                      <span className={`status-badge status-${getDifficultyClass(question.difficulty)}`}>
                        {getDifficultyLabel(question.difficulty)}
                      </span>
                    </td>
                    <td>
                      <span className={`status-badge status-${getStatusClass(question.status)}`}>
                        {getStatusMeta(question.status).label}
                      </span>
                    </td>
                    <td className="actions-cell">
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

        {totalPages > 1 && (
          <div className="pagination">
            <div className="pagination-buttons">
              <button
                className="pagination-btn"
                type="button"
                disabled={currentPage === 1}
                onClick={() => setCurrentPage(1)}
              >
                <ChevronsLeft size={18} />
              </button>
              <button
                className="pagination-btn"
                type="button"
                disabled={currentPage === 1}
                onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
              >
                <ChevronLeft size={18} />
              </button>

              {pageNumbers.map((button) =>
                button === 'start-ellipsis' || button === 'end-ellipsis' ? (
                  <span key={`${button}-${currentPage}`} className="pagination-ellipsis">
                    …
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
                disabled={currentPage === totalPages}
                onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
              >
                <ChevronRight size={18} />
              </button>
              <button
                className="pagination-btn"
                type="button"
                disabled={currentPage === totalPages}
                onClick={() => setCurrentPage(totalPages)}
              >
                <ChevronsRight size={18} />
              </button>
            </div>
          </div>
        )}
      </section>

      {isDeleteModalOpen && (
        <div className="modal-backdrop">
          <div className="modal-card">
            <div className="modal-header">
              <h3 className="modal-title">{t('deleteConfirmTitle', 'Delete Question')}</h3>
              <button type="button" className="btn-close" onClick={closeDeleteModal}>
                <X size={20} />
              </button>
            </div>
            <div className="modal-body">
              <p>{t('deleteConfirmText', 'Are you sure you want to delete this question? This action cannot be undone.')}</p>
            </div>
            <div className="modal-footer">
              <button type="button" className="btn-secondary" onClick={closeDeleteModal}>
                {t('cancel', 'Cancel')}
              </button>
              <button type="button" className="btn-danger" onClick={confirmDelete}>
                {t('confirmDelete', 'Delete')}
              </button>
            </div>
          </div>
        </div>
      )}

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
                <label className="modal-label">{t('tableAnswer', 'Suggested Answer')}</label>
                <textarea
                  name="suggestedAnswer"
                  className="modal-input textarea"
                  value={questionToEdit.suggestedAnswer || ''}
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
                    value={String(getDifficultyMeta(questionToEdit.difficulty, t).value || activeDifficultyOptions[0] || '')}
                    onChange={handleEditChange}
                  >
                    {formDifficultyOptions.map((opt) => (
                      <option key={opt.value} value={opt.value}>{opt.label}</option>
                    ))}
                  </select>
                </div>
                <div className="modal-form-group">
                  <label className="modal-label">{t('tableRole', 'Role Target')}</label>
                  <input
                    type="text"
                    name="roleTarget"
                    className="modal-input"
                    value={questionToEdit.roleTarget || ''}
                    onChange={handleEditChange}
                  />
                </div>
                <div className="modal-form-group">
                  <label className="modal-label">{t('tableMajor', 'Major')}</label>
                  <input
                    type="text"
                    name="major"
                    className="modal-input"
                    value={questionToEdit.major || ''}
                    onChange={handleEditChange}
                  />
                </div>
              </div>
              <div className="modal-form-row">
                <div className="modal-form-group">
                  <label className="modal-label">{t('tableType', 'Question Type')}</label>
                  <input
                    type="text"
                    name="questionType"
                    className="modal-input"
                    value={questionToEdit.questionType || ''}
                    onChange={handleEditChange}
                  />
                </div>
                <div className="modal-form-group">
                  <label className="modal-label">{t('tableTechStack', 'Tech Stack')}</label>
                  <input
                    type="text"
                    name="techStack"
                    className="modal-input"
                    value={questionToEdit.techStack || ''}
                    onChange={handleEditChange}
                  />
                </div>
                <div className="modal-form-group">
                  <label className="modal-label">{t('tagsLabel', 'Tags')}</label>
                  <input
                    type="text"
                    name="tags"
                    className="modal-input"
                    value={questionToEdit.tags || ''}
                    onChange={handleEditChange}
                  />
                </div>
                <div className="modal-form-group">
                  <label className="modal-label">{t('tableStatus', 'Status')}</label>
                  <select
                    name="status"
                    className="modal-input"
                    value={questionToEdit.status || 'Active'}
                    onChange={handleEditChange}
                  >
                    {filterOptions.statuses.filter((s) => s !== ALL_OPTION).map((status) => (
                      <option key={status} value={status}>{status}</option>
                    ))}
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
                <label className="modal-label">{t('tableAnswer', 'Suggested Answer')}</label>
                <textarea
                  name="suggestedAnswer"
                  className="modal-input textarea"
                  value={newQuestion.suggestedAnswer || ''}
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
                    value={String(newQuestion.difficulty || activeDifficultyOptions[0] || '')}
                    onChange={handleAddChange}
                  >
                    {formDifficultyOptions.map((opt) => (
                      <option key={opt.value} value={opt.value}>{opt.label}</option>
                    ))}
                  </select>
                </div>
                <div className="modal-form-group">
                  <label className="modal-label">{t('tableRole', 'Role Target')}</label>
                  <input
                    type="text"
                    name="roleTarget"
                    className="modal-input"
                    value={newQuestion.roleTarget || ''}
                    onChange={handleAddChange}
                  />
                </div>
                <div className="modal-form-group">
                  <label className="modal-label">{t('tableMajor', 'Major')}</label>
                  <input
                    type="text"
                    name="major"
                    className="modal-input"
                    value={newQuestion.major || ''}
                    onChange={handleAddChange}
                  />
                </div>
              </div>
              <div className="modal-form-row">
                <div className="modal-form-group">
                  <label className="modal-label">{t('tableType', 'Question Type')}</label>
                  <input
                    type="text"
                    name="questionType"
                    className="modal-input"
                    value={newQuestion.questionType || ''}
                    onChange={handleAddChange}
                  />
                </div>
                <div className="modal-form-group">
                  <label className="modal-label">{t('tableTechStack', 'Tech Stack')}</label>
                  <input
                    type="text"
                    name="techStack"
                    className="modal-input"
                    value={newQuestion.techStack || ''}
                    onChange={handleAddChange}
                  />
                </div>
                <div className="modal-form-group">
                  <label className="modal-label">{t('tagsLabel', 'Tags')}</label>
                  <input
                    type="text"
                    name="tags"
                    className="modal-input"
                    value={newQuestion.tags || ''}
                    onChange={handleAddChange}
                  />
                </div>
                <div className="modal-form-group">
                  <label className="modal-label">{t('tableStatus', 'Status')}</label>
                  <select
                    name="status"
                    className="modal-input"
                    value={newQuestion.status || 'Active'}
                    onChange={handleAddChange}
                  >
                    {filterOptions.statuses.filter((s) => s !== ALL_OPTION).map((status) => (
                      <option key={status} value={status}>{status}</option>
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

