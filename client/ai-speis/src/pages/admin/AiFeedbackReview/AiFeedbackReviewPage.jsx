import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { createPortal } from 'react-dom';
import {
  AlertCircle,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  Eye,
  Flag,
  Loader2,
  RefreshCw,
  Search,
  ShieldCheck,
  X,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { getAdminFeedback, getAdminFeedbackDetail } from '../../../services/aiEvaluationFeedbackApi';
import notify from '../../../utils/notification';
import '../../../styles/admin/UserManagementPage.css';
import './AiFeedbackReviewPage.css';

const REASON_LABELS = {
  INCORRECT_SCORE: ['Điểm đánh giá chưa chính xác', 'Incorrect score'],
  INACCURATE_FEEDBACK: ['Nhận xét chưa chính xác', 'Inaccurate feedback'],
  MISSING_CONTEXT: ['Thiếu bằng chứng hoặc ngữ cảnh', 'Missing evidence or context'],
  HALLUCINATION: ['AI đưa ra thông tin không có căn cứ', 'Hallucinated information'],
  BIAS_OR_UNFAIRNESS: ['Đánh giá thiên vị hoặc không công bằng', 'Biased or unfair evaluation'],
  UNCLEAR_EXPLANATION: ['Giải thích chưa rõ ràng', 'Unclear explanation'],
  OFFENSIVE_OR_INAPPROPRIATE: ['Nội dung không phù hợp', 'Offensive or inappropriate content'],
  OTHER: ['Khác', 'Other'],
};

const formatDate = (value, locale) => value
  ? new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
  : '—';

function AiFeedbackReviewPage() {
  const { i18n } = useTranslation();
  const isVi = (i18n.resolvedLanguage || i18n.language || 'vi').startsWith('vi');
  const locale = isVi ? 'vi-VN' : 'en-US';

  const copy = useMemo(() => isVi ? {
    breadcrumb: 'Phản hồi đánh giá AI',
    title: 'Phản hồi đánh giá AI',
    description: 'Xem danh sách và chi tiết các đánh giá AI được người dùng phản hồi.',
    search: 'Tìm theo người dùng, lý do hoặc nội dung...',
    reset: 'Đặt lại',
    emptyTitle: 'Không có phản hồi phù hợp',
    emptyBody: 'Thử thay đổi từ khóa tìm kiếm.',
    loadError: 'Không thể tải danh sách phản hồi.',
    retry: 'Làm mới',
    close: 'Đóng',
    user: 'Người gửi',
    type: 'Loại đánh giá',
    submitted: 'Ngày gửi',
    actions: 'Thao tác',
    titleHeader: 'Lý do & Nội dung',
    viewDetail: 'Xem chi tiết',
    detail: 'Chi tiết phản hồi',
    explanation: 'Nội dung chi tiết',
    executiveSummary: 'Tóm tắt đánh giá',
    strengths: 'Điểm mạnh',
    gaps: 'Điểm cần cải thiện',
    immutable: 'Bảng phản hồi không lưu câu hỏi, transcript hoặc điểm từng câu. Kết quả AI cấp vòng bên dưới được đọc trực tiếp từ kết quả phỏng vấn.',
    showing: 'Hiển thị',
    of: 'trong tổng số',
    feedback: 'phản hồi',
    pageSize: 'Số hàng:',
  } : {
    breadcrumb: 'AI Evaluation Feedback',
    title: 'AI Evaluation Feedback',
    description: 'View the list and details of user-reported AI evaluations.',
    search: 'Search by user, reason, or explanation...',
    reset: 'Reset',
    emptyTitle: 'No matching feedback',
    emptyBody: 'Try a different search term.',
    loadError: 'Feedback could not be loaded.',
    retry: 'Refresh',
    close: 'Close',
    user: 'Submitted by',
    type: 'Evaluation type',
    submitted: 'Submitted',
    actions: 'Actions',
    titleHeader: 'Reason & Details',
    viewDetail: 'View Details',
    detail: 'Feedback detail',
    explanation: 'Explanation',
    executiveSummary: 'Executive summary',
    strengths: 'Strengths',
    gaps: 'Areas for improvement',
    immutable: 'The feedback table stores no questions, transcripts, or per-question scores. The round-level AI result below is loaded directly from the interview result.',
    showing: 'Showing',
    of: 'of',
    feedback: 'feedback',
    pageSize: 'Rows:',
  }, [isVi]);

  const [searchDraft, setSearchDraft] = useState('');
  const [search, setSearch] = useState('');
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [data, setData] = useState({ items: [], totalItems: 0, totalPages: 0 });
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [selected, setSelected] = useState(null);
  const [detailLoading, setDetailLoading] = useState(false);

  const loadFeedback = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const result = await getAdminFeedback({ search, pageNumber, pageSize });
      setData(result || { items: [], totalItems: 0, totalPages: 0 });
    } catch {
      setError(copy.loadError);
    } finally {
      setLoading(false);
    }
  }, [copy.loadError, pageNumber, pageSize, search]);

  useEffect(() => { loadFeedback(); }, [loadFeedback]);

  const openDetail = async (feedbackId) => {
    setDetailLoading(true);
    try {
      const item = await getAdminFeedbackDetail(feedbackId);
      setSelected(item);
    } catch {
      notify.error(copy.loadError);
    } finally {
      setDetailLoading(false);
    }
  };

  const totalItems = data.totalItems || 0;
  const totalPages = Math.max(1, data.totalPages || Math.ceil(totalItems / pageSize));
  const startIndex = totalItems === 0 ? 0 : (pageNumber - 1) * pageSize + 1;
  const endIndex = totalItems === 0 ? 0 : Math.min(pageNumber * pageSize, totalItems);

  const pageButtons = useMemo(() => {
    const buttons = [];
    if (totalPages <= 7) {
      for (let p = 1; p <= totalPages; p += 1) {
        buttons.push(p);
      }
      return buttons;
    }

    const leftBound = Math.max(2, pageNumber - 2);
    const rightBound = Math.min(totalPages - 1, pageNumber + 2);

    buttons.push(1);
    if (leftBound > 2) buttons.push('start-ellipsis');
    for (let p = leftBound; p <= rightBound; p += 1) {
      buttons.push(p);
    }
    if (rightBound < totalPages - 1) buttons.push('end-ellipsis');
    buttons.push(totalPages);

    return buttons;
  }, [pageNumber, totalPages]);

  return (
    <div className="admin-dashboard-page user-management-page">
      {/* Page Header */}
      <div className="page-header">
        <div className="breadcrumb">
          <span>Admin</span>
          <span className="separator">/</span>
          <span aria-current="page">{copy.breadcrumb}</span>
        </div>

        <div className="header-top">
          <div className="title-section">
            <h1 className="page-title">{copy.title}</h1>
            <p className="page-description">{copy.description}</p>
          </div>
          <button
            type="button"
            onClick={loadFeedback}
            className="btn-secondary"
            style={{ display: 'inline-flex', alignItems: 'center', gap: '8px' }}
            title={copy.retry}
          >
            <RefreshCw size={16} />
            <span>{copy.retry}</span>
          </button>
        </div>
      </div>

      <div className="page-content">
        {/* Filter Card */}
        <div className="filter-card">
          <form
            className="filter-row"
            style={{ gridTemplateColumns: searchDraft ? '1fr auto' : '1fr' }}
            onSubmit={(event) => {
              event.preventDefault();
              setSearch(searchDraft.trim());
              setPageNumber(1);
            }}
          >
            <div className="filter-group search-group">
              <Search size={20} />
              <input
                type="text"
                placeholder={copy.search}
                value={searchDraft}
                onChange={(e) => setSearchDraft(e.target.value)}
                className="search-input"
              />
            </div>
            {searchDraft && (
              <button
                type="button"
                className="btn-secondary filter-reset-btn"
                onClick={() => {
                  setSearchDraft('');
                  setSearch('');
                  setPageNumber(1);
                }}
              >
                {copy.reset}
              </button>
            )}
          </form>
        </div>

        {/* Error State */}
        {error ? (
          <div className="error-state">
            <AlertCircle size={20} />
            <div>
              <p>{error}</p>
              <button
                type="button"
                onClick={loadFeedback}
                className="btn-secondary"
                style={{ marginTop: '12px' }}
              >
                {copy.retry}
              </button>
            </div>
          </div>
        ) : (
          /* Table Card */
          <div className="table-card">
            {loading ? (
              <table className="users-table">
                <thead>
                  <tr>
                    <th>{copy.titleHeader}</th>
                    <th>{copy.user}</th>
                    <th>{copy.type}</th>
                    <th>{copy.submitted}</th>
                    <th className="col-actions">{copy.actions}</th>
                  </tr>
                </thead>
                <tbody>
                  {[...Array(5)].map((_, i) => (
                    <tr key={i} className="skeleton-row">
                      <td><div className="skeleton skeleton-text" /></td>
                      <td><div className="skeleton skeleton-text" /></td>
                      <td><div className="skeleton skeleton-text" /></td>
                      <td><div className="skeleton skeleton-text" /></td>
                      <td><div className="skeleton skeleton-actions" /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : !data.items?.length ? (
              <div className="empty-state">
                <Flag size={48} />
                <h3>{copy.emptyTitle}</h3>
                <p>{copy.emptyBody}</p>
              </div>
            ) : (
              <>
                <table className="users-table">
                  <thead>
                    <tr>
                      <th>{copy.titleHeader}</th>
                      <th>{copy.user}</th>
                      <th>{copy.type}</th>
                      <th>{copy.submitted}</th>
                      <th className="col-actions">{copy.actions}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.items.map((item) => (
                      <tr
                        key={item.feedbackId}
                        onClick={() => openDetail(item.feedbackId)}
                        className="cursor-pointer"
                      >
                        <td className="col-name" style={{ maxWidth: '340px' }}>
                          <strong className="block text-sm text-text-primary">
                            {(REASON_LABELS[item.reason] || [item.reason, item.reason])[isVi ? 0 : 1]}
                          </strong>
                          <span className="mt-0.5 block truncate text-xs text-text-secondary">
                            {item.explanation}
                          </span>
                        </td>
                        <td className="col-email">
                          <span className="block font-semibold text-text-primary">{item.userName}</span>
                          <span className="text-xs text-text-secondary">{item.userEmail}</span>
                        </td>
                        <td className="col-role">
                          <span className="role-badge">{item.evaluationType}</span>
                        </td>
                        <td className="col-date">{formatDate(item.createdAt, locale)}</td>
                        <td className="col-actions" onClick={(e) => e.stopPropagation()}>
                          <div className="action-buttons">
                            <button
                              type="button"
                              className="action-btn"
                              title={copy.viewDetail}
                              aria-label={copy.viewDetail}
                              onClick={() => openDetail(item.feedbackId)}
                            >
                              <Eye size={18} />
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>

                {/* Pagination */}
                <div className="pagination">
                  <div className="pagination-info">
                    <span>
                      {copy.showing} {startIndex}-{endIndex} {copy.of} {totalItems} {copy.feedback}
                    </span>
                    <div className="page-size-selector">
                      <label>{copy.pageSize}</label>
                      <select
                        value={pageSize}
                        onChange={(e) => {
                          setPageSize(Number(e.target.value));
                          setPageNumber(1);
                        }}
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
                    <div className="pagination-desktop">
                      <button
                        className="pagination-btn"
                        type="button"
                        disabled={pageNumber <= 1}
                        onClick={() => setPageNumber(1)}
                      >
                        <ChevronsLeft size={18} />
                      </button>
                      <button
                        className="pagination-btn"
                        type="button"
                        disabled={pageNumber <= 1}
                        onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
                      >
                        <ChevronLeft size={18} />
                      </button>

                      {pageButtons.map((button) => (
                        button === 'start-ellipsis' || button === 'end-ellipsis' ? (
                          <span key={button} className="pagination-ellipsis">…</span>
                        ) : (
                          <button
                            key={button}
                            className={`pagination-btn ${pageNumber === button ? 'active' : ''}`}
                            type="button"
                            onClick={() => setPageNumber(button)}
                          >
                            {button}
                          </button>
                        )
                      ))}

                      <button
                        className="pagination-btn"
                        type="button"
                        disabled={pageNumber >= totalPages}
                        onClick={() => setPageNumber((p) => Math.min(totalPages, p + 1))}
                      >
                        <ChevronRight size={18} />
                      </button>
                      <button
                        className="pagination-btn"
                        type="button"
                        disabled={pageNumber >= totalPages}
                        onClick={() => setPageNumber(totalPages)}
                      >
                        <ChevronsRight size={18} />
                      </button>
                    </div>

                    <div className="pagination-mobile">
                      <button
                        className="pagination-btn"
                        type="button"
                        disabled={pageNumber <= 1}
                        onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
                      >
                        <ChevronLeft size={18} />
                      </button>
                      <span className="current-page">{pageNumber}</span>
                      <button
                        className="pagination-btn"
                        type="button"
                        disabled={pageNumber >= totalPages}
                        onClick={() => setPageNumber((p) => Math.min(totalPages, p + 1))}
                      >
                        <ChevronRight size={18} />
                      </button>
                    </div>
                  </div>
                </div>
              </>
            )}
          </div>
        )}
      </div>

      {/* Modal detail */}
      {(selected || detailLoading) ? createPortal(
        <div
          className="ai-feedback-modal-overlay"
          role="presentation"
          onMouseDown={(event) => { if (event.target === event.currentTarget) setSelected(null); }}
        >
          <div
            className="ai-feedback-modal-container"
            role="dialog"
            aria-modal="true"
            aria-label={copy.detail}
            onMouseDown={(event) => event.stopPropagation()}
          >
            {detailLoading ? (
              <div className="grid min-h-80 place-items-center"><Loader2 className="animate-spin text-primary" size={28} /></div>
            ) : selected ? (
              <>
                <header className="ai-feedback-modal-header">
                  <div className="flex min-w-0 items-center gap-3">
                    <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-primary text-white"><Flag size={20} /></span>
                    <div className="min-w-0">
                      <div className="mb-1 flex flex-wrap items-center gap-2">
                        <span className="text-xs text-text-secondary">#{selected.feedbackId} · {formatDate(selected.createdAt, locale)}</span>
                      </div>
                      <h2 className="m-0 truncate text-lg font-extrabold text-text-primary">
                        {(REASON_LABELS[selected.reason] || [selected.reason, selected.reason])[isVi ? 0 : 1]}
                      </h2>
                      <p className="mb-0 mt-0.5 text-xs text-text-secondary">{copy.detail}</p>
                    </div>
                  </div>
                  <button
                    type="button"
                    className="grid h-9 w-9 shrink-0 place-items-center rounded-full text-text-muted transition-colors hover:bg-surface hover:text-text-primary"
                    onClick={() => setSelected(null)}
                    aria-label={copy.close}
                  >
                    <X size={19} />
                  </button>
                </header>

                <div className="ai-feedback-modal-body">
                  <div className="flex gap-3 rounded-lg border border-primary/20 bg-primary-xlight p-3 text-xs leading-5 text-primary-dark">
                    <ShieldCheck className="shrink-0" size={18} />
                    <span>{copy.immutable}</span>
                  </div>
                  <dl className="grid grid-cols-1 gap-4 text-sm sm:grid-cols-2">
                    <div>
                      <dt className="text-xs font-bold uppercase text-text-muted">{copy.user}</dt>
                      <dd className="m-0 mt-1 font-semibold text-text-primary">{selected.userName}<br /><span className="font-normal text-text-secondary">{selected.userEmail}</span></dd>
                    </div>
                    <div>
                      <dt className="text-xs font-bold uppercase text-text-muted">{copy.type}</dt>
                      <dd className="m-0 mt-1 font-semibold text-text-primary">{selected.evaluationType} · Session #{selected.interviewSessionId}</dd>
                    </div>
                  </dl>
                  <section>
                    <h3 className="mb-2 border-b border-border pb-2 text-sm font-bold text-primary">{copy.explanation}</h3>
                    <p className="m-0 whitespace-pre-wrap text-sm leading-6 text-text-primary">{selected.explanation}</p>
                  </section>
                  <section>
                    <h3 className="mb-3 border-b border-border pb-2 text-sm font-bold text-primary">AI evaluation · {selected.evaluationType}</h3>
                    <div className="grid grid-cols-1 divide-y divide-border rounded-lg border border-border lg:grid-cols-3 lg:divide-x lg:divide-y-0">
                      <div className="min-w-0 p-4">
                        <h4 className="mb-2 mt-0 text-xs font-bold uppercase text-text-muted">{copy.executiveSummary}</h4>
                        <p className="m-0 whitespace-pre-wrap text-sm leading-6 text-text-primary">{selected.aiExecutiveSummary || '—'}</p>
                      </div>
                      <div className="min-w-0 p-4">
                        <h4 className="mb-2 mt-0 text-xs font-bold uppercase text-text-muted">{copy.strengths}</h4>
                        {selected.aiStrengths?.length ? (
                          <ul className="m-0 space-y-2 pl-5 text-sm leading-6 text-text-primary">
                            {selected.aiStrengths.map((item, index) => <li key={`${item}-${index}`}>{item}</li>)}
                          </ul>
                        ) : <p className="m-0 text-sm text-text-muted">—</p>}
                      </div>
                      <div className="min-w-0 p-4">
                        <h4 className="mb-2 mt-0 text-xs font-bold uppercase text-text-muted">{copy.gaps}</h4>
                        {selected.aiGaps?.length ? (
                          <ul className="m-0 space-y-2 pl-5 text-sm leading-6 text-text-primary">
                            {selected.aiGaps.map((item, index) => <li key={`${item}-${index}`}>{item}</li>)}
                          </ul>
                        ) : <p className="m-0 text-sm text-text-muted">—</p>}
                      </div>
                    </div>
                  </section>
                </div>

                <footer className="ai-feedback-modal-footer">
                  <button
                    type="button"
                    onClick={() => setSelected(null)}
                    className="btn-secondary"
                  >
                    {copy.close}
                  </button>
                </footer>
              </>
            ) : null}
          </div>
        </div>,
        document.body,
      ) : null}
    </div>
  );
}

export default AiFeedbackReviewPage;
