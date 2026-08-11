import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  AlertCircle,
  CalendarClock,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  Eye,
  FileQuestion,
  Filter,
  LoaderCircle,
  RefreshCw,
  Search,
  X,
} from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';
import interviewSessionService from '../../services/InterviewSessionService';
import { getInterviewReviewPath } from '../../routes/routePaths';
import { navigate } from '../../routes/navigation';
import { getInterviewHistoryCopy } from '../../features/interviewHistory/interviewHistoryCopy';
import '../../styles/user/InterviewHistory.css';

const formatDate = (value, includeTime = true) => {
  if (!value) return null;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  return new Intl.DateTimeFormat('vi-VN', {
    dateStyle: 'medium',
    ...(includeTime ? { timeStyle: 'short' } : {}),
  }).format(date);
};

const formatScore = (score, maxScore) => {
  const numericScore = Number(score);
  if (!Number.isFinite(numericScore)) return null;
  const numericMaxScore = Number(maxScore);
  return Number.isFinite(numericMaxScore) && numericMaxScore > 0
    ? `${numericScore.toFixed(2)}/${numericMaxScore}`
    : numericScore.toFixed(2);
};

const hasQuestionProgress = (session) => (
  Number.isFinite(session?.completedQuestionCount) && Number.isFinite(session?.questionCount)
);

const getStatusLabel = (status, copy) => copy.statuses[status] || status || '';
const getRoundLabel = (type, copy) => copy.rounds[type] || type || '';
const canReview = (session) => (
  session.status === 'Completed' && ['Technical', 'Behavior'].includes(session.interviewRoundType)
);

function SessionDetailDialog({ campaign, copy, onClose }) {
  const [result, setResult] = useState(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const dialogRef = useRef(null);
  const closeButtonRef = useRef(null);

  const loadResult = useCallback(async () => {
    if (campaign.status !== 'Completed') return;
    setIsLoading(true);
    setError('');
    try {
      setResult(await interviewSessionService.getCampaignResult(campaign.interviewCampaignId));
    } catch (loadError) {
      setError(copy.history.scoreError);
    } finally {
      setIsLoading(false);
    }
  }, [campaign.interviewCampaignId, campaign.status, copy.history.scoreError]);

  useEffect(() => {
    loadResult();
  }, [loadResult]);

  useEffect(() => {
    const handleKeyDown = (event) => {
      if (event.key === 'Escape') onClose();
      if (event.key !== 'Tab' || !dialogRef.current) return;
      const focusable = dialogRef.current.querySelectorAll(
        'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled])',
      );
      if (!focusable.length) return;
      const first = focusable[0];
      const last = focusable[focusable.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    closeButtonRef.current?.focus();
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onClose]);

  const availableReview = campaign.sessions?.find(canReview);
  const scoreBySession = new Map((result?.rounds || []).map((round) => [round.interviewSessionId, round]));

  return (
    <div className="interview-history-dialog-backdrop" role="presentation" onMouseDown={onClose}>
      <section
        className="interview-history-dialog"
        ref={dialogRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby="session-detail-title"
        onMouseDown={(event) => event.stopPropagation()}
      >
        <header>
          <div>
            <p>{copy.history.detailEyebrow}</p>
            <h2 id="session-detail-title">{copy.history.detailTitle.replace('{{id}}', campaign.interviewCampaignId)}</h2>
          </div>
          <button ref={closeButtonRef} type="button" onClick={onClose} aria-label={copy.history.closeAria}><X size={20} /></button>
        </header>

        <dl className="interview-history-dialog__meta">
          <div><dt>{copy.history.status}</dt><dd><span className={`interview-history-status interview-history-status--${campaign.status?.toLowerCase()}`}>{getStatusLabel(campaign.status, copy)}</span></dd></div>
          {formatDate(campaign.startedAt || campaign.createdAt) ? <div><dt>{copy.history.started}</dt><dd>{formatDate(campaign.startedAt || campaign.createdAt)}</dd></div> : null}
          {formatDate(campaign.completedAt) ? <div><dt>{copy.history.finished}</dt><dd>{formatDate(campaign.completedAt)}</dd></div> : null}
          {campaign.durationMinutes ? <div><dt>{copy.history.duration}</dt><dd>{copy.history.minutes.replace('{{count}}', campaign.durationMinutes)}</dd></div> : null}
          {campaign.language ? <div><dt>{copy.history.language}</dt><dd>{campaign.language.toUpperCase()}</dd></div> : null}
          {formatScore(result?.overallScore, result?.maxScore) ? <div><dt>{copy.history.totalScore}</dt><dd className="interview-history-dialog__score">{formatScore(result.overallScore, result.maxScore)}</dd></div> : null}
        </dl>

        <section className="interview-history-dialog__rounds" aria-label={copy.history.sessionRounds}>
          <h3>{copy.history.sessionRounds}</h3>
          {isLoading ? <p className="interview-history-dialog__loading"><LoaderCircle size={17} /> {copy.history.loadingResult}</p> : null}
          {error ? <div className="interview-history-dialog__error"><span>{error}</span><button type="button" onClick={loadResult}>{copy.history.retry}</button></div> : null}
          {(campaign.sessions || []).map((session) => {
            const score = scoreBySession.get(session.interviewSessionId);
            return (
              <article key={session.interviewSessionId}>
                <div>
                  <strong>{getRoundLabel(session.interviewRoundType, copy)}</strong>
                  {hasQuestionProgress(session) ? <span>{copy.history.answerCount.replace('{{completed}}', session.completedQuestionCount).replace('{{total}}', session.questionCount)}</span> : null}
                </div>
                <div>
                  {formatScore(score?.score, score?.maxScore) ? <b>{formatScore(score.score, score.maxScore)}</b> : null}
                  <span className={`interview-history-status interview-history-status--${session.status?.toLowerCase()}`}>{getStatusLabel(session.status, copy)}</span>
                  {canReview(session) ? <button type="button" className="interview-history-dialog__review" onClick={() => navigate(getInterviewReviewPath(session.interviewSessionId))}>{copy.history.review}</button> : null}
                </div>
              </article>
            );
          })}
        </section>

        {availableReview ? (
          <footer>
            <button type="button" className="interview-history-button interview-history-button--secondary" onClick={onClose}>{copy.history.close}</button>
            <button type="button" className="interview-history-button" onClick={() => navigate(getInterviewReviewPath(availableReview.interviewSessionId))}>
              <FileQuestion size={17} /> {copy.history.review}
            </button>
          </footer>
        ) : <footer><button type="button" className="interview-history-button interview-history-button--secondary" onClick={onClose}>{copy.history.close}</button></footer>}
      </section>
    </div>
  );
}

function InterviewHistoryPage() {
  const { i18n } = useTranslation();
  const copy = getInterviewHistoryCopy(i18n.resolvedLanguage || i18n.language);
  const [campaigns, setCampaigns] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [selectedCampaign, setSelectedCampaign] = useState(null);
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState('');
  const [round, setRound] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const loadHistory = useCallback(async () => {
    setIsLoading(true);
    setError('');
    try {
      const data = await interviewSessionService.getMyCampaigns();
      setCampaigns(Array.isArray(data) ? data : []);
    } catch (loadError) {
      setError(loadError?.status === 401 || loadError?.status === 403
        ? copy.history.unauthorized
        : copy.history.loadError);
    } finally {
      setIsLoading(false);
    }
  }, [copy.history.loadError, copy.history.unauthorized]);

  useEffect(() => { loadHistory(); }, [loadHistory]);

  const rows = useMemo(() => campaigns.flatMap((campaign) => (campaign.sessions || []).map((session) => ({
    ...session,
    displayStatus: (['Cancelled', 'Expired'].includes(campaign.status) && session.status !== 'Completed')
      ? campaign.status
      : session.status,
    campaignId: campaign.interviewCampaignId,
    campaignStatus: campaign.status,
    language: campaign.language,
    createdAt: session.updatedAt || campaign.createdAt,
    campaign,
  }))), [campaigns]);

  const filteredRows = useMemo(() => rows.filter((row) => {
    const searchable = `${row.campaignId} ${getRoundLabel(row.interviewRoundType, copy)} ${getStatusLabel(row.displayStatus, copy)}`.toLowerCase();
    return (!query || searchable.includes(query.trim().toLowerCase()))
      && (!status || row.displayStatus === status)
      && (!round || row.interviewRoundType === round);
  }), [copy, query, round, rows, status]);

  // Reset to page 1 whenever filters or page size change
  useEffect(() => {
    setCurrentPage(1);
  }, [query, status, round, pageSize]);

  const totalItems = filteredRows.length;
  const totalPages = Math.ceil(totalItems / pageSize) || 1;
  const startIndex = totalItems > 0 ? (currentPage - 1) * pageSize + 1 : 0;
  const endIndex = Math.min(currentPage * pageSize, totalItems);

  const paginatedRows = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredRows.slice(start, start + pageSize);
  }, [currentPage, filteredRows, pageSize]);

  const pageButtons = useMemo(() => {
    if (totalPages <= 7) {
      return Array.from({ length: totalPages }, (_, i) => i + 1);
    }
    const btns = [];
    btns.push(1);
    if (currentPage > 3) btns.push('start-ellipsis');
    const start = Math.max(2, currentPage - 1);
    const end = Math.min(totalPages - 1, currentPage + 1);
    for (let i = start; i <= end; i++) {
      if (!btns.includes(i)) btns.push(i);
    }
    if (currentPage < totalPages - 2) btns.push('end-ellipsis');
    if (!btns.includes(totalPages)) btns.push(totalPages);
    return btns;
  }, [currentPage, totalPages]);

  const summary = useMemo(() => ({
    total: rows.length,
    completed: rows.filter((row) => row.displayStatus === 'Completed').length,
    active: rows.filter((row) => ['Active', 'Pending'].includes(row.displayStatus)).length,
  }), [rows]);
  const showLanguage = useMemo(() => rows.every((row) => Boolean(row.language)), [rows]);
  const showQuestionProgress = useMemo(() => rows.every(hasQuestionProgress), [rows]);

  const resetFilters = () => { setQuery(''); setStatus(''); setRound(''); };

  let content;
  if (isLoading) {
    content = <div className="interview-history-state" role="status"><LoaderCircle className="interview-history-spin" size={32} /><p>{copy.history.loading}</p></div>;
  } else if (error) {
    content = <div className="interview-history-state" role="alert"><AlertCircle size={32} /><h2>{copy.history.loadTitle}</h2><p>{error}</p><button type="button" className="interview-history-button" onClick={loadHistory}><RefreshCw size={17} /> {copy.history.retry}</button></div>;
  } else if (!rows.length) {
    content = <div className="interview-history-state"><CalendarClock size={36} /><h2>{copy.history.emptyTitle}</h2><p>{copy.history.emptyDescription}</p></div>;
  } else if (!filteredRows.length) {
    content = <div className="interview-history-state"><Search size={34} /><h2>{copy.history.noResultsTitle}</h2><p>{copy.history.noResultsDescription}</p><button type="button" className="interview-history-button interview-history-button--secondary" onClick={resetFilters}>{copy.history.clearFilters}</button></div>;
  } else {
    content = (
      <div className="interview-history-table-wrap">
        <table className="interview-history-table">
          <thead><tr><th>{copy.history.time}</th><th>{copy.history.campaign}</th><th>{copy.history.round}</th>{showQuestionProgress ? <th>{copy.history.answers}</th> : null}{showLanguage ? <th>{copy.history.language}</th> : null}<th>{copy.history.status}</th><th><span className="sr-only">{copy.history.actions}</span></th></tr></thead>
          <tbody>{paginatedRows.map((row) => (
            <tr key={row.interviewSessionId}>
              {formatDate(row.createdAt) ? <td data-label={copy.history.time}>{formatDate(row.createdAt)}</td> : null}
              <td data-label={copy.history.campaign}><strong>#{row.campaignId}</strong></td>
              <td data-label={copy.history.round}>{getRoundLabel(row.interviewRoundType, copy)}</td>
              {showQuestionProgress && hasQuestionProgress(row) ? <td data-label={copy.history.answers}>{row.completedQuestionCount}/{row.questionCount}</td> : null}
              {showLanguage && row.language ? <td data-label={copy.history.language}>{row.language.toUpperCase()}</td> : null}
              <td data-label={copy.history.status}><span className={`interview-history-status interview-history-status--${row.displayStatus?.toLowerCase()}`}>{getStatusLabel(row.displayStatus, copy)}</span></td>
              <td className="interview-history-table__actions" data-label={copy.history.actions}>
                <button type="button" aria-label={copy.history.detailAria.replace('{{id}}', row.campaignId)} title={copy.history.viewDetail} onClick={() => setSelectedCampaign(row.campaign)}><Eye size={18} /></button>
                {canReview(row) ? <button type="button" aria-label={copy.history.reviewAria.replace('{{round}}', getRoundLabel(row.interviewRoundType, copy))} title={copy.history.review} onClick={() => navigate(getInterviewReviewPath(row.interviewSessionId))}><FileQuestion size={18} /></button> : null}
              </td>
            </tr>
          ))}</tbody>
        </table>

        {/* Pagination Bar */}
        <div className="pagination">
          <div className="pagination-info">
            <span>
              {copy.history.showing} <strong>{startIndex}-{endIndex}</strong> {copy.history.of} <strong>{totalItems}</strong> {copy.history.sessionsUnit}
            </span>
            <div className="page-size-selector">
              <label>{copy.history.pageSize}</label>
              <select
                value={pageSize}
                onChange={(e) => setPageSize(Number(e.target.value))}
                className="page-size-select"
              >
                <option value={10}>10</option>
                <option value={20}>20</option>
                <option value={50}>50</option>
                <option value={100}>100</option>
              </select>
            </div>
          </div>

          <div className="pagination-buttons">
            <button
              type="button"
              className="pagination-btn"
              disabled={currentPage === 1}
              onClick={() => setCurrentPage(1)}
              title={copy.history.firstPage}
            >
              <ChevronsLeft size={18} />
            </button>

            <button
              type="button"
              className="pagination-btn"
              disabled={currentPage === 1}
              onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
              title={copy.history.previousPage}
            >
              <ChevronLeft size={18} />
            </button>

            {pageButtons.map((btn, idx) => {
              if (typeof btn === 'string') {
                return (
                  <span key={`${btn}-${idx}`} className="pagination-ellipsis px-1.5 font-bold text-text-disabled">
                    ...
                  </span>
                );
              }
              const isActive = btn === currentPage;
              return (
                <button
                  key={btn}
                  type="button"
                  onClick={() => setCurrentPage(btn)}
                  className={`pagination-btn ${isActive ? 'active' : ''}`}
                >
                  {btn}
                </button>
              );
            })}

            <button
              type="button"
              className="pagination-btn"
              disabled={currentPage === totalPages}
              onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
              title={copy.history.nextPage}
            >
              <ChevronRight size={18} />
            </button>

            <button
              type="button"
              className="pagination-btn"
              disabled={currentPage === totalPages}
              onClick={() => setCurrentPage(totalPages)}
              title={copy.history.lastPage}
            >
              <ChevronsRight size={18} />
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <UserLayout>
      <section className="interview-history-page">
        <header className="interview-history-header"><div><p>{copy.history.eyebrow}</p><h1>{copy.history.title}</h1><span>{copy.history.subtitle}</span></div></header>
        <section className="interview-history-summary" aria-label={copy.history.title}>
          <article><span>{copy.history.total}</span><strong>{summary.total}</strong></article>
          <article><span>{copy.history.completed}</span><strong>{summary.completed}</strong></article>
          <article><span>{copy.history.active}</span><strong>{summary.active}</strong></article>
        </section>
        <section className="interview-history-controls" aria-label={copy.history.title}>
          <label className="interview-history-search"><Search size={18} /><span className="sr-only">{copy.history.search}</span><input value={query} onChange={(event) => setQuery(event.target.value)} placeholder={copy.history.search} /></label>
          <label><span>{copy.history.status}</span><div><select value={status} onChange={(event) => setStatus(event.target.value)}><option value="">{copy.history.all}</option>{Object.entries(copy.statuses).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select><ChevronDown size={16} /></div></label>
          <label><span>{copy.history.round}</span><div><select value={round} onChange={(event) => setRound(event.target.value)}><option value="">{copy.history.all}</option>{Object.entries(copy.rounds).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select><ChevronDown size={16} /></div></label>
          <button type="button" className="interview-history-filter-reset" onClick={resetFilters}><Filter size={17} /> {copy.history.reset}</button>
        </section>
        {content}
      </section>
      {selectedCampaign ? <SessionDetailDialog campaign={selectedCampaign} copy={copy} onClose={() => setSelectedCampaign(null)} /> : null}
    </UserLayout>
  );
}

export default InterviewHistoryPage;
