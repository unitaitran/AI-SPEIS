import React, { useEffect, useMemo, useState } from 'react';
//import { useTranslation } from 'react-i18next';
import { useTranslation } from 'react-i18next';
import {
  Search,
  FileInput,
  Plus,
  Eye,
  Edit3,
  Trash2,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
} from 'lucide-react';
import questionService from '../../../services/QuestionService';
import './QuestionManagementPage.css';

const MOCK_QUESTIONS = [
  {
    id: 'Q-001',
    code: 'Q-001',
    content: 'Explain what RESTful APIs are and why they are useful.',
    role: 'Backend Developer',
    techStack: 'Node.js',
    type: 'Technical',
    difficulty: 'Medium',
    source: 'Internal',
    status: 'Active',
  },
  {
    id: 'Q-002',
    code: 'Q-002',
    content: 'How do you optimize React applications for performance?',
    role: 'Frontend Developer',
    techStack: 'React',
    type: 'Technical',
    difficulty: 'Medium',
    source: 'Internal',
    status: 'Draft',
  },
  {
    id: 'Q-003',
    code: 'Q-003',
    content: 'What is the difference between CI and CD?',
    role: 'DevOps Engineer',
    techStack: 'AWS',
    type: 'Behavioral',
    difficulty: 'Easy',
    source: 'External',
    status: 'Active',
  },
  {
    id: 'Q-004',
    code: 'Q-004',
    content: 'Describe how serverless architecture can be used in a microservices environment.',
    role: 'Cloud Engineer',
    techStack: 'AWS',
    type: 'System Design',
    difficulty: 'Hard',
    source: 'Internal',
    status: 'Active',
  },
  {
    id: 'Q-005',
    code: 'Q-005',
    content: 'How would you write a unit test for a form validation function?',
    role: 'QA Engineer',
    techStack: 'JavaScript',
    type: 'Technical',
    difficulty: 'Easy',
    source: 'Internal',
    status: 'Disabled',
  },
  {
    id: 'Q-006',
    code: 'Q-006',
    content: 'What considerations do you make when designing a scalable database schema?',
    role: 'Data Engineer',
    techStack: 'Python',
    type: 'Technical',
    difficulty: 'Hard',
    source: 'External',
    status: 'Draft',
  },
  {
    id: 'Q-007',
    code: 'Q-007',
    content: 'Describe a challenging bug you fixed in production.',
    role: 'Full Stack Engineer',
    techStack: 'React',
    type: 'Behavioral',
    difficulty: 'Medium',
    source: 'Internal',
    status: 'Active',
  },
  {
    id: 'Q-008',
    code: 'Q-008',
    content: 'How do you handle authentication and authorization in a web application?',
    role: 'Full Stack Engineer',
    techStack: 'Node.js',
    type: 'Technical',
    difficulty: 'Medium',
    source: 'External',
    status: 'Active',
  },
  {
    id: 'Q-009',
    code: 'Q-009',
    content: 'What is the purpose of Docker and how does it compare with a virtual machine?',
    role: 'DevOps Engineer',
    techStack: 'Docker',
    type: 'Technical',
    difficulty: 'Medium',
    source: 'Internal',
    status: 'Active',
  },
  {
    id: 'Q-010',
    code: 'Q-010',
    content: 'How do you ensure code quality when working in a distributed team?',
    role: 'Backend Developer',
    techStack: 'Java',
    type: 'Behavioral',
    difficulty: 'Easy',
    source: 'External',
    status: 'Draft',
  },
];

const ROLE_OPTIONS = [
  'all',
  'Backend Developer',
  'Frontend Developer',
  'Full Stack Engineer',
  'DevOps Engineer',
  'QA Engineer',
  'Data Engineer',
  'Cloud Engineer',
];

const TECH_STACK_OPTIONS = [
  'all',
  'React',
  'Node.js',
  'JavaScript',
  'Python',
  'AWS',
  'Docker',
  'Java',
];

const QUESTION_TYPE_OPTIONS = [
  'all',
  'Technical',
  'Behavioral',
  'System Design',
];

const DIFFICULTY_OPTIONS = ['all', 'Easy', 'Medium', 'Hard'];

const STATUS_CHIPS = [
  { value: 'all', label: 'statusAll' },
  { value: 'Active', label: 'statusActive' },
  { value: 'Draft', label: 'statusDraft' },
  { value: 'Disabled', label: 'statusDisabled' },
];

function QuestionManagementPage() {
  const { t } = useTranslation('questionBank');

  const [questions, setQuestions] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [useMockData, setUseMockData] = useState(false);
  const [filters, setFilters] = useState({
    search: '',
    role: 'all',
    techStack: 'all',
    questionType: 'all',
    difficulty: 'all',
    status: 'all',
  });
  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 8;

  useEffect(() => {
    const loadQuestions = async () => {
      try {
        setLoading(true);
        setError(null);
        setUseMockData(false);

        const result = await questionService.getQuestions();

        if (Array.isArray(result)) {
          setQuestions(result);
        } else if (result?.items) {
          setQuestions(result.items);
        } else if (Array.isArray(result?.data)) {
          setQuestions(result.data);
        } else {
          setQuestions(MOCK_QUESTIONS);
          setUseMockData(true);
        }
      } catch (fetchError) {
        setError(fetchError.message || 'Unable to load questions');
        setQuestions(MOCK_QUESTIONS);
        setUseMockData(true);
      } finally {
        setLoading(false);
      }
    };

    loadQuestions();
  }, []);

  const handleFilterChange = (event) => {
    const { name, value } = event.target;
    setFilters((prev) => ({ ...prev, [name]: value }));
    setCurrentPage(1);
  };

  const handleSearchChange = (event) => {
    const { value } = event.target;
    setFilters((prev) => ({ ...prev, search: value }));
    setCurrentPage(1);
  };

  const handleStatusChipClick = (status) => {
    setFilters((prev) => ({ ...prev, status }));
    setCurrentPage(1);
  };

  const handleClearFilters = () => {
    setFilters({
      search: '',
      role: 'all',
      techStack: 'all',
      questionType: 'all',
      difficulty: 'all',
      status: 'all',
    });
    setCurrentPage(1);
  };

  const filteredQuestions = useMemo(() => {
    return questions.filter((question) => {
      const searchTerm = filters.search.trim().toLowerCase();
      const matchesSearch = searchTerm
        ? [question.code, question.content, question.source]
            .filter(Boolean)
            .some((value) => value.toLowerCase().includes(searchTerm))
        : true;

      const matchesRole = filters.role === 'all' || question.role === filters.role;
      const matchesTechStack = filters.techStack === 'all' || question.techStack === filters.techStack;
      const matchesType = filters.questionType === 'all' || question.type === filters.questionType;
      const matchesDifficulty = filters.difficulty === 'all' || question.difficulty === filters.difficulty;
      const matchesStatus = filters.status === 'all' || question.status === filters.status;

      return matchesSearch && matchesRole && matchesTechStack && matchesType && matchesDifficulty && matchesStatus;
    });
  }, [questions, filters]);

  const totalQuestions = filteredQuestions.length;
  const totalPages = Math.max(1, Math.ceil(totalQuestions / pageSize));
  const paginatedQuestions = filteredQuestions.slice((currentPage - 1) * pageSize, currentPage * pageSize);
  const startIndex = totalQuestions === 0 ? 0 : (currentPage - 1) * pageSize + 1;
  const endIndex = Math.min(currentPage * pageSize, totalQuestions);

  const getPageNumbers = () => {
    const pages = [];
    const startPage = Math.max(1, currentPage - 1);
    const endPage = Math.min(totalPages, currentPage + 1);

    if (startPage > 1) {
      pages.push(1);
    }

    if (startPage > 2) {
      pages.push('start-ellipsis');
    }

    for (let page = startPage; page <= endPage; page += 1) {
      pages.push(page);
    }

    if (endPage < totalPages - 1) {
      pages.push('end-ellipsis');
    }

    if (endPage < totalPages) {
      pages.push(totalPages);
    }

    return pages;
  };

  const pageNumbers = getPageNumbers();

  return (
    <div className="question-management-page">
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
            <button
              type="button"
              className="btn-secondary"
              onClick={() => console.log('Import Excel')}
            >
              <FileInput size={16} />
              {t('importExcel', 'Import Excel')}
            </button>
            <button
              type="button"
              className="btn-primary"
              onClick={() => console.log('Add question')}
            >
              <Plus size={16} />
              {t('addQuestion', 'Add question')}
            </button>
          </div>
        </div>
      </div>

      <section className="filter-card">
        <div className="filter-layout">
          <label className="filter-field search-field">
            <Search size={18} />
            <input
              type="text"
              name="search"
              className="search-input"
              value={filters.search}
              onChange={handleSearchChange}
              placeholder={t('searchPlaceholder', 'Search by question content, code, or source')}
            />
          </label>

          <label className="filter-field">
            <span>{t('roleLabel', 'Role')}</span>
            <select
              name="role"
              value={filters.role}
              onChange={handleFilterChange}
              className="filter-select"
            >
              {ROLE_OPTIONS.map((option) => (
                <option key={option} value={option}>
                  {option === 'all' ? t('statusAll', 'All') : option}
                </option>
              ))}
            </select>
          </label>

          <label className="filter-field">
            <span>{t('techStackLabel', 'Tech Stack')}</span>
            <select
              name="techStack"
              value={filters.techStack}
              onChange={handleFilterChange}
              className="filter-select"
            >
              {TECH_STACK_OPTIONS.map((option) => (
                <option key={option} value={option}>
                  {option === 'all' ? t('statusAll', 'All') : option}
                </option>
              ))}
            </select>
          </label>

          <label className="filter-field">
            <span>{t('questionTypeLabel', 'Question Type')}</span>
            <select
              name="questionType"
              value={filters.questionType}
              onChange={handleFilterChange}
              className="filter-select"
            >
              {QUESTION_TYPE_OPTIONS.map((option) => (
                <option key={option} value={option}>
                  {option === 'all' ? t('statusAll', 'All') : option}
                </option>
              ))}
            </select>
          </label>

          <label className="filter-field">
            <span>{t('difficultyLabel', 'Difficulty')}</span>
            <select
              name="difficulty"
              value={filters.difficulty}
              onChange={handleFilterChange}
              className="filter-select"
            >
              {DIFFICULTY_OPTIONS.map((option) => (
                <option key={option} value={option}>
                  {option === 'all' ? t('statusAll', 'All') : option}
                </option>
              ))}
            </select>
          </label>
        </div>

        <div className="filter-footer">
          <div className="status-chip-row">
            {STATUS_CHIPS.map((chip) => (
              <button
                type="button"
                className={`status-chip ${filters.status === chip.value ? 'is-active' : ''}`}
                onClick={() => handleStatusChipClick(chip.value)}
                key={chip.value}
              >
                {t(chip.label, chip.label === 'statusAll' ? 'All' : chip.label)}
              </button>
            ))}
          </div>

          <button
            type="button"
            className="btn-secondary btn-reset"
            onClick={handleClearFilters}
          >
            {t('clearFilters', 'Clear filters')}
          </button>
        </div>
      </section>

      <section className="table-card">
        <div className="table-header-row">
          <div>
            <p className="table-summary">
              {t('showing', 'Showing')} {startIndex}-{endIndex} {t('of', 'of')} {totalQuestions} {t('questions', 'questions')}
            </p>
          </div>
          {error && <p className="table-error">{error}</p>}
        </div>

        <div className="table-scroll">
          <table className="question-table">
            <thead>
              <tr>
                <th>
                  <input
                    type="checkbox"
                    aria-label="Select all questions"
                    checked={paginatedQuestions.length > 0 && paginatedQuestions.every((q) => q.selected)}
                    readOnly
                  />
                </th>
                <th>{t('tableCode', 'Code')}</th>
                <th>{t('tableContent', 'Question Content')}</th>
                <th>{t('tableRole', 'Role')}</th>
                <th>{t('tableTechStack', 'Tech Stack')}</th>
                <th>{t('tableType', 'Question Type')}</th>
                <th>{t('tableDifficulty', 'Difficulty')}</th>
                <th>{t('tableSource', 'Source')}</th>
                <th>{t('tableStatus', 'Status')}</th>
                <th>{t('tableActions', 'Actions')}</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan="10" className="loading-row">
                    Loading questions...
                  </td>
                </tr>
              ) : paginatedQuestions.length === 0 ? (
                <tr>
                  <td colSpan="10" className="empty-row">
                    {t('noQuestions', 'No questions match the selected filter.')}
                  </td>
                </tr>
              ) : (
                paginatedQuestions.map((question) => (
                  <tr key={question.id}>
                    <td>
                      <input
                        type="checkbox"
                        aria-label={`Select ${question.code}`}
                        readOnly
                      />
                    </td>
                    <td>{question.code}</td>
                    <td>{question.content}</td>
                    <td>{question.role}</td>
                    <td>{question.techStack}</td>
                    <td>{question.type}</td>
                    <td>{question.difficulty}</td>
                    <td>{question.source}</td>
                    <td>
                      <span className={`status-badge status-${question.status.toLowerCase()}`}>
                        {question.status}
                      </span>
                    </td>
                    <td>
                      <div className="action-buttons">
                        <button
                          type="button"
                          className="icon-button"
                          onClick={() => console.log('View question', question.id)}
                          title={t('actionView', 'View question')}
                        >
                          <Eye size={16} />
                        </button>
                        <button
                          type="button"
                          className="icon-button"
                          onClick={() => console.log('Edit question', question.id)}
                          title={t('actionEdit', 'Edit question')}
                        >
                          <Edit3 size={16} />
                        </button>
                        <button
                          type="button"
                          className="icon-button danger"
                          onClick={() => console.log('Delete question', question.id)}
                          title={t('actionDelete', 'Delete question')}
                        >
                          <Trash2 size={16} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        <div className="pagination-row">
          <div className="pagination-info">
            {t('showing', 'Showing')} {startIndex}-{endIndex} {t('of', 'of')} {totalQuestions} {t('questions', 'questions')}
          </div>
          <div className="pagination-actions">
            <button
              type="button"
              className="pagination-button"
              disabled={currentPage === 1}
              onClick={() => setCurrentPage(1)}
              aria-label="First page"
            >
              <ChevronsLeft size={16} />
            </button>
            <button
              type="button"
              className="pagination-button"
              disabled={currentPage === 1}
              onClick={() => setCurrentPage((prev) => Math.max(1, prev - 1))}
              aria-label="Previous page"
            >
              <ChevronLeft size={16} />
            </button>
            {pageNumbers.map((page) => (
              <React.Fragment key={page}>
                {typeof page === 'string' ? (
                  <span className="pagination-ellipsis">…</span>
                ) : (
                  <button
                    type="button"
                    className={`pagination-button ${page === currentPage ? 'is-active' : ''}`}
                    onClick={() => setCurrentPage(page)}
                  >
                    {page}
                  </button>
                )}
              </React.Fragment>
            ))}
            <button
              type="button"
              className="pagination-button"
              disabled={currentPage === totalPages}
              onClick={() => setCurrentPage((prev) => Math.min(totalPages, prev + 1))}
              aria-label="Next page"
            >
              <ChevronRight size={16} />
            </button>
            <button
              type="button"
              className="pagination-button"
              disabled={currentPage === totalPages}
              onClick={() => setCurrentPage(totalPages)}
              aria-label="Last page"
            >
              <ChevronsRight size={16} />
            </button>
          </div>
        </div>
      </section>
    </div>
  );
}

export default QuestionManagementPage;
