import React, { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Globe } from 'lucide-react';
import i18n from '../../i18n';
import UserLayout from '../../layouts/user/UserLayout';
import './QuestionBankPage.css';
import './CVAnalysisDashboard.css';
import enData from '../../locales/en/questionBank.json';
import viData from '../../locales/vi/questionBank.json';

if (!i18n.hasResourceBundle('en', 'questionBank')) {
  i18n.addResourceBundle('en', 'questionBank', enData, true, true);
}
if (!i18n.hasResourceBundle('vi', 'questionBank')) {
  i18n.addResourceBundle('vi', 'questionBank', viData, true, true);
}

const mockQuestions = [
  {
    id: 1,
    title: 'How do you optimize React application performance?',
    description: 'Discuss memoization, lazy loading, and reconciliation behavior for performance tuning.',
    role: 'frontend',
    category: 'frontend',
    difficulty: 'medium',
    status: 'new',
    recommended: true,
    createdAt: '2026-06-01',
  },
  {
    id: 2,
    title: 'Explain event delegation in JavaScript.',
    description: 'Describe how event bubbling works and why delegation is useful for dynamic UIs.',
    role: 'frontend',
    category: 'frontend',
    difficulty: 'easy',
    status: 'practice',
    recommended: false,
    createdAt: '2026-06-02',
  },
  {
    id: 3,
    title: 'Design a URL shortener service.',
    description: 'Talk through service components, storage strategy, and scaling considerations.',
    role: 'fullstack',
    category: 'system-design',
    difficulty: 'hard',
    status: 'new',
    recommended: false,
    createdAt: '2026-06-03',
  },
  {
    id: 4,
    title: 'How do you ensure database consistency in distributed systems?',
    description: 'Explain CAP theorem, 2PC, and eventual consistency patterns.',
    role: 'backend',
    category: 'system-design',
    difficulty: 'hard',
    status: 'practice',
    recommended: true,
    createdAt: '2026-05-29',
  },
  {
    id: 5,
    title: 'Walk me through the OAuth 2.0 authorization code flow.',
    description: 'Include steps for token exchange, refresh, and security recommendations.',
    role: 'backend',
    category: 'security',
    difficulty: 'medium',
    status: 'mastered',
    recommended: false,
    createdAt: '2026-05-30',
  },
  {
    id: 6,
    title: 'How do you handle a conflict on a team project?',
    description: 'Share a behavioral story that shows collaboration and constructive problem solving.',
    role: 'product',
    category: 'behavioral',
    difficulty: 'easy',
    status: 'new',
    recommended: false,
    createdAt: '2026-06-04',
  },
  {
    id: 7,
    title: 'What is the difference between REST and GraphQL?',
    description: 'Compare data fetching patterns, versioning, and client flexibility.',
    role: 'fullstack',
    category: 'backend',
    difficulty: 'medium',
    status: 'practice',
    recommended: false,
    createdAt: '2026-06-05',
  },
  {
    id: 8,
    title: 'How do you write unit tests for a React component?',
    description: 'Talk about test utilities, assertions, and maintaining testable UI.',
    role: 'frontend',
    category: 'frontend',
    difficulty: 'easy',
    status: 'mastered',
    recommended: false,
    createdAt: '2026-05-28',
  },
  {
    id: 9,
    title: 'Describe a time when you improved a product feature based on feedback.',
    description: 'Build a strong story that highlights customer empathy and iteration.',
    role: 'product',
    category: 'behavioral',
    difficulty: 'medium',
    status: 'practice',
    recommended: true,
    createdAt: '2026-06-06',
  },
];

const roles = [
  { value: 'all', labelKey: 'allRoles' },
  { value: 'frontend', labelKey: 'roleFrontend' },
  { value: 'backend', labelKey: 'roleBackend' },
  { value: 'fullstack', labelKey: 'roleFullstack' },
  { value: 'product', labelKey: 'roleProduct' },
];

const categories = [
  { value: 'all', labelKey: 'allCategories' },
  { value: 'frontend', labelKey: 'categoryFrontend' },
  { value: 'backend', labelKey: 'categoryBackend' },
  { value: 'system-design', labelKey: 'categorySystemDesign' },
  { value: 'security', labelKey: 'categorySecurity' },
  { value: 'behavioral', labelKey: 'categoryBehavioral' },
];

const difficulties = [
  { value: 'all', labelKey: 'allDifficulties' },
  { value: 'easy', labelKey: 'difficultyEasy' },
  { value: 'medium', labelKey: 'difficultyMedium' },
  { value: 'hard', labelKey: 'difficultyHard' },
];

const statuses = [
  { value: 'all', labelKey: 'allStatuses' },
  { value: 'new', labelKey: 'statusNew' },
  { value: 'practice', labelKey: 'statusPractice' },
  { value: 'mastered', labelKey: 'statusMastered' },
];

const sortOptions = [
  { value: 'newest', labelKey: 'sortNewest' },
  { value: 'difficulty', labelKey: 'sortDifficulty' },
  { value: 'role', labelKey: 'sortRole' },
];

const pageSize = 6;

function QuestionBankPage() {
  const { t, i18n } = useTranslation('questionBank');
  const [search, setSearch] = useState('');
  const [role, setRole] = useState('all');
  const [category, setCategory] = useState('all');
  const [difficulty, setDifficulty] = useState('all');
  const [status, setStatus] = useState('all');
  const [sortBy, setSortBy] = useState('newest');
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!i18n.hasResourceBundle('en', 'questionBank')) {
      i18n.addResourceBundle('en', 'questionBank', enData, true, true);
    }
    if (!i18n.hasResourceBundle('vi', 'questionBank')) {
      i18n.addResourceBundle('vi', 'questionBank', viData, true, true);
    }
  }, [i18n]);

  useEffect(() => {
    setLoading(true);
    setError(null);
    const timer = window.setTimeout(() => {
      try {
        setLoading(false);
      } catch (err) {
        setError(t('error'));
        setLoading(false);
      }
    }, 400);
    return () => window.clearTimeout(timer);
  }, [t]);

  const filteredQuestions = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase();
    return mockQuestions
      .filter((question) => {
        const matchesSearch = normalizedSearch
          ? question.title.toLowerCase().includes(normalizedSearch)
            || question.description.toLowerCase().includes(normalizedSearch)
          : true;
        const matchesRole = role === 'all' || question.role === role;
        const matchesCategory = category === 'all' || question.category === category;
        const matchesDifficulty = difficulty === 'all' || question.difficulty === difficulty;
        const matchesStatus = status === 'all' || question.status === status;
        return matchesSearch && matchesRole && matchesCategory && matchesDifficulty && matchesStatus;
      })
      .sort((a, b) => {
        if (sortBy === 'difficulty') {
          const order = { easy: 1, medium: 2, hard: 3 };
          return order[a.difficulty] - order[b.difficulty];
        }
        if (sortBy === 'role') {
          return a.role.localeCompare(b.role);
        }
        return new Date(b.createdAt) - new Date(a.createdAt);
      });
  }, [search, role, category, difficulty, status, sortBy]);

  const recommendedQuestion = useMemo(
    () => mockQuestions.find((question) => question.recommended) || mockQuestions[0],
    []
  );

  const totalPages = Math.max(1, Math.ceil(filteredQuestions.length / pageSize));
  const currentPageQuestions = filteredQuestions.slice((page - 1) * pageSize, page * pageSize);

  useEffect(() => {
    if (page > totalPages) {
      setPage(1);
    }
  }, [page, totalPages]);

  const toggleLanguage = () => {
    const newLang = i18n.language === 'vi' ? 'en' : 'vi';
    i18n.changeLanguage(newLang);
  };

  const resetFilters = () => {
    setSearch('');
    setRole('all');
    setCategory('all');
    setDifficulty('all');
    setStatus('all');
    setSortBy('newest');
    setPage(1);
  };

  return (
    <UserLayout>
      <div className="question-bank-page cv-page">

        <header className="qb-header">
  <div>
    <p className="qb-kicker">{t('title')}</p>
    <h1>{t('subtitle')}</h1>
  </div>

  <div className="qb-header-actions">
    <button
      type="button"
      className="language-button"
      onClick={toggleLanguage}
      aria-label={t('aria.languageSwitch', 'Toggle language')}
    >
      <Globe size={16} />
      <span className="lang-text">
        {i18n.language === 'vi' ? 'VI / EN' : 'EN / VI'}
      </span>
    </button>

    <button
      type="button"
      className="qb-clear-button"
      onClick={resetFilters}
    >
      {t('btnClear')}
    </button>
  </div>
</header>

        <section className="qb-controls">
          <label className="qb-control-group">
            <span>{t('searchPlaceholder')}</span>
            <input
              type="text"
              className="qb-input"
              value={search}
              onChange={(event) => {
                setSearch(event.target.value);
                setPage(1);
              }}
              placeholder={t('searchPlaceholder')}
              aria-label={t('searchPlaceholder')}
            />
          </label>

          <div className="qb-filters-grid">
            <label className="qb-control-group">
              <span>{t('roleFilterLabel')}</span>
              <select
                className="qb-select"
                value={role}
                onChange={(event) => {
                  setRole(event.target.value);
                  setPage(1);
                }}
              >
                {roles.map((option) => (
                  <option key={option.value} value={option.value}>
                    {t(option.labelKey)}
                  </option>
                ))}
              </select>
            </label>

            <label className="qb-control-group">
              <span>{t('categoryFilterLabel')}</span>
              <select
                className="qb-select"
                value={category}
                onChange={(event) => {
                  setCategory(event.target.value);
                  setPage(1);
                }}
              >
                {categories.map((option) => (
                  <option key={option.value} value={option.value}>
                    {t(option.labelKey)}
                  </option>
                ))}
              </select>
            </label>

            <label className="qb-control-group">
              <span>{t('difficultyFilterLabel')}</span>
              <select
                className="qb-select"
                value={difficulty}
                onChange={(event) => {
                  setDifficulty(event.target.value);
                  setPage(1);
                }}
              >
                {difficulties.map((option) => (
                  <option key={option.value} value={option.value}>
                    {t(option.labelKey)}
                  </option>
                ))}
              </select>
            </label>

            <label className="qb-control-group">
              <span>{t('statusFilterLabel')}</span>
              <select
                className="qb-select"
                value={status}
                onChange={(event) => {
                  setStatus(event.target.value);
                  setPage(1);
                }}
              >
                {statuses.map((option) => (
                  <option key={option.value} value={option.value}>
                    {t(option.labelKey)}
                  </option>
                ))}
              </select>
            </label>

            <label className="qb-control-group qb-sort-group">
              <span>{t('sortLabel')}</span>
              <select
                className="qb-select"
                value={sortBy}
                onChange={(event) => setSortBy(event.target.value)}
              >
                {sortOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {t(option.labelKey)}
                  </option>
                ))}
              </select>
            </label>
          </div>
        </section>

        <section className="qb-recommended card">
          <div className="qb-recommended-header">
            <div>
              <p className="qb-section-label">{t('recommendedTitle')}</p>
              <h2>{recommendedQuestion.title}</h2>
            </div>
            <span className="qb-badge">{t('recommendedLabel')}</span>
          </div>
          <p className="qb-recommended-copy">{recommendedQuestion.description}</p>
          <div className="qb-meta-row">
            <span>{t('roleLabel')}: {t(`role${capitalize(recommendedQuestion.role)}`)}</span>
            <span>{t('categoryLabel')}: {t(`category${formatCategoryLabel(recommendedQuestion.category)}`)}</span>
            <span>{t('difficultyLabel')}: {t(`difficulty${capitalize(recommendedQuestion.difficulty)}`)}</span>
          </div>
        </section>

        <section className="qb-list-section">
          {loading ? (
            <div className="qb-loading">{t('loading')}</div>
          ) : error ? (
            <div className="qb-error">{error}</div>
          ) : currentPageQuestions.length === 0 ? (
            <div className="qb-empty">{t('searchNoResults')}</div>
          ) : (
            <div className="qb-grid">
              {currentPageQuestions.map((question) => (
                <article key={question.id} className="qb-card card">
                  <div className="qb-card-top">
                    <div>
                      <h3>{question.title}</h3>
                      <p>{question.description}</p>
                    </div>
                    {question.recommended && <span className="qb-card-pill">{t('recommendedLabel')}</span>}
                  </div>
                  <div className="qb-tags">
                    <span>{t(`role${capitalize(question.role)}`)}</span>
                    <span>{t(`category${formatCategoryLabel(question.category)}`)}</span>
                    <span>{t(`difficulty${capitalize(question.difficulty)}`)}</span>
                    <span>{t(`status${capitalize(question.status)}`)}</span>
                  </div>
                  <button type="button" className="qb-primary-button">
                    {t('btnPractice')}
                  </button>
                </article>
              ))}
            </div>
          )}
        </section>

        <section className="qb-pagination">
          <button
            type="button"
            className="qb-page-button"
            onClick={() => setPage((current) => Math.max(current - 1, 1))}
            disabled={page === 1}
          >
            {t('prev')}
          </button>
          <div className="qb-page-info">
            {t('pageLabel')} {page} / {totalPages}
          </div>
          <button
            type="button"
            className="qb-page-button"
            onClick={() => setPage((current) => Math.min(current + 1, totalPages))}
            disabled={page === totalPages}
          >
            {t('next')}
          </button>
        </section>
      </div>
    </UserLayout>
  );
}

function capitalize(value) {
  if (!value) return '';
  return value.charAt(0).toUpperCase() + value.slice(1);
}

function formatCategoryLabel(categoryKey) {
  return categoryKey
    .split('-')
    .map((part) => capitalize(part))
    .join('');
}

export default QuestionBankPage;
