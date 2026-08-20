import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  AlertCircle,
  CalendarClock,
  Check,
  CheckCircle2,
  ChevronRight,
  Clock,
  Code2,
  FileQuestion,
  History,
  Layers,
  Play,
  Plus,
  RefreshCw,
  Search,
  Sparkles,
  Trophy,
  UserCheck,
  XCircle,
} from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';
import interviewSessionService from '../../services/InterviewSessionService';
import { getInterviewReviewPath, getCampaignResultPath, USER_ROUTES } from '../../routes/routePaths';
import { navigate } from '../../routes/navigation';
import { beginNewInterviewCampaign } from '../../utils/interviewContext';
import { getInterviewHistoryCopy, formatInterviewTitle } from '../../features/interviewHistory/interviewHistoryCopy';

// Design System Components
import Card from '../../components/UI/Card';
import Button from '../../components/UI/Button';
import Badge from '../../components/UI/Badge';
import EmptyState from '../../components/UI/EmptyState';
import Spinner from '../../components/UI/Spinner';
import Alert from '../../components/UI/Alert';
import Modal from '../../components/UI/Modal';
import Pagination from '../../components/UI/Pagination';
import ProgressBar from '../../components/UI/ProgressBar';

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
    ? `${numericScore.toFixed(1)}/${numericMaxScore}`
    : numericScore.toFixed(1);
};

// Determine Campaign Mode: 'Mock' or 'Practice'
const getCampaignMode = (campaign) => {
  const rawMode = (campaign.mode || campaign.Mode || '').toString().toLowerCase();
  const sessions = campaign.sessions || campaign.Sessions || [];
  if (rawMode === 'realtest' || rawMode === 'mock' || rawMode === 'mockinterview' || sessions.length === 3) {
    return 'Mock';
  }
  return 'Practice';
};

const getStatusConfig = (status, copy) => {
  switch (status) {
    case 'Completed':
      return { label: copy.statuses.Completed, variant: 'success', icon: CheckCircle2 };
    case 'Active':
    case 'Pending':
    case 'InProgress':
      return { label: copy.statuses.Active, variant: 'primary', icon: Clock };
    case 'Cancelled':
      return { label: copy.statuses.Cancelled, variant: 'neutral', icon: XCircle };
    case 'Expired':
      return { label: copy.statuses.Expired, variant: 'error', icon: AlertCircle };
    default:
      return { label: copy.statuses[status] || status || '—', variant: 'neutral', icon: Clock };
  }
};

const getRoundConfig = (type, copy) => {
  switch (type) {
    case 'Technical':
      return { label: copy.rounds.Technical, variant: 'ai', icon: Code2 };
    case 'Behavior':
    case 'Behavioral':
      return { label: copy.rounds.Behavior, variant: 'secondary', icon: UserCheck };
    case 'Code':
    case 'Coding':
      return { label: copy.rounds.Code, variant: 'primary', icon: Layers };
    default:
      return { label: type || 'Vòng', variant: 'neutral', icon: FileQuestion };
  }
};

const canReviewSession = (session) => session.status === 'Completed';

// Detail Modal for Campaign
function SessionDetailModal({ campaign, copy, onClose }) {
  const [result, setResult] = useState(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');

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

  const scoreBySession = useMemo(() => {
    const map = new Map();
    if (result?.rounds) {
      result.rounds.forEach((r) => {
        if (r.interviewSessionId) map.set(r.interviewSessionId, r);
      });
    }
    return map;
  }, [result]);

  return (
    <Modal
      isOpen={!!campaign}
      onClose={onClose}
      title={copy.history.detailTitle.replace('{{id}}', campaign.interviewCampaignId)}
      size="lg"
    >
      <div className="flex flex-col gap-4">
        {/* Campaign Info Grid */}
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 p-3.5 bg-surface-muted rounded-xl text-xs">
          <div>
            <span className="text-text-muted block mb-0.5">{copy.history.status}</span>
            <Badge variant={getStatusConfig(campaign.status, copy).variant} size="sm">
              {getStatusConfig(campaign.status, copy).label}
            </Badge>
          </div>
          {(campaign.startedAt || campaign.createdAt) && (
            <div>
              <span className="text-text-muted block mb-0.5">{copy.history.started}</span>
              <span className="font-semibold text-text-primary">{formatDate(campaign.startedAt || campaign.createdAt)}</span>
            </div>
          )}
          {campaign.durationMinutes && (
            <div>
              <span className="text-text-muted block mb-0.5">{copy.history.duration}</span>
              <span className="font-semibold text-text-primary">{campaign.durationMinutes} {copy.history.minutes.replace('{{count}}', '')}</span>
            </div>
          )}
          {formatScore(result?.overallScore, result?.maxScore) && (
            <div>
              <span className="text-text-muted block mb-0.5">{copy.history.overallScore}</span>
              <span className="font-extrabold text-primary text-sm">{formatScore(result.overallScore, result.maxScore)}</span>
            </div>
          )}
        </div>

        {/* Sessions / Rounds Horizontal Layout (3 rounds side-by-side) */}
        <div className="flex flex-col gap-3">
          <h4 className="text-sm font-bold text-text-primary">{copy.history.sessionRounds}</h4>

          {isLoading && (
            <div className="py-6 flex items-center justify-center">
              <Spinner size="md" label={copy.history.loadingResult} />
            </div>
          )}

          {error && (
            <Alert variant="error" onClose={() => setError('')}>
              {error}
            </Alert>
          )}

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            {(campaign.sessions || []).map((session) => {
              const score = scoreBySession.get(session.interviewSessionId);
              const rConfig = getRoundConfig(session.interviewRoundType, copy);
              const sConfig = getStatusConfig(session.status, copy);
              const isCoding = ['Code', 'Coding'].includes(session.interviewRoundType);

              let codingPassed = session.passedTestCases;
              let codingTotal = session.totalTestCases;
              if (score?.codingQuestions && score.codingQuestions.length > 0) {
                codingPassed = score.codingQuestions.reduce((sum, q) => sum + (q.passedTestCases || 0), 0);
                codingTotal = score.codingQuestions.reduce((sum, q) => sum + (q.totalTestCases || 0), 0);
              }

              return (
                <div
                  key={session.interviewSessionId}
                  className="p-4 bg-surface rounded-xl border border-border flex flex-col justify-between items-center text-center gap-3 shadow-xs"
                >
                  <div className="flex flex-col items-center gap-2 w-full">
                    <div className="flex items-center justify-center gap-1.5 flex-wrap">
                      <Badge variant={rConfig.variant} size="sm" icon={rConfig.icon}>
                        {rConfig.label}
                      </Badge>
                      <Badge variant={sConfig.variant} size="sm">
                        {sConfig.label}
                      </Badge>
                    </div>

                    {isCoding && (codingPassed != null || codingTotal != null) && (
                      <span className="text-xs font-semibold text-text-secondary mt-1">
                        Testcase pass: <strong className="text-text-primary">{codingPassed ?? 0}/{codingTotal ?? 0}</strong>
                      </span>
                    )}
                  </div>

                  <div className="flex flex-col items-center gap-2 w-full mt-1 pt-2 border-t border-border/60">
                    {formatScore(score?.score, score?.maxScore) && (
                      <div className="text-center">
                        <span className="text-[10px] text-text-muted block uppercase font-bold">{copy.history.totalScore}</span>
                        <span className="text-base font-extrabold text-primary">{formatScore(score.score, score.maxScore)}</span>
                      </div>
                    )}

                    {canReviewSession(session) && (
                      <Button
                        variant="primary"
                        size="sm"
                        onClick={() => {
                          onClose();
                          if (isCoding) {
                            navigate(getCampaignResultPath(campaign.interviewCampaignId));
                          } else {
                            navigate(getInterviewReviewPath(session.interviewSessionId));
                          }
                        }}
                        className="w-full justify-center"
                      >
                        {copy.history.viewResultBtn}
                      </Button>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </div>

        {/* Modal Actions */}
        <div className="flex justify-end gap-2 pt-3 border-t border-border mt-2">
          <Button variant="outline" size="md" onClick={onClose}>
            {copy.history.close}
          </Button>
        </div>
      </div>
    </Modal>
  );
}

function InterviewHistoryPage() {
  const { i18n } = useTranslation();
  const copy = getInterviewHistoryCopy(i18n.resolvedLanguage || i18n.language);

  const [campaigns, setCampaigns] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');
  const [selectedCampaign, setSelectedCampaign] = useState(null);

  // Filter States (Session-based)
  const [query, setQuery] = useState('');
  const [modeFilter, setModeFilter] = useState('All'); // 'All' | 'Practice' | 'Mock'
  const [roundFilter, setRoundFilter] = useState('All'); // 'All' | 'Technical' | 'Behavioral' | 'Coding'
  const [statusFilter, setStatusFilter] = useState('');

  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 6; // 6 session cards per page

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

  // Session-based list of Campaigns
  const sessionList = useMemo(() => {
    return campaigns.map((campaign) => {
      const mode = getCampaignMode(campaign);
      const sessions = campaign.sessions || [];
      const completedRounds = sessions.filter((s) => s.status === 'Completed').length;
      const totalRounds = sessions.length;
      const totalQuestionsAnswered = sessions.reduce((sum, s) => sum + (s.completedQuestionCount || 0), 0);

      return {
        campaign,
        id: campaign.interviewCampaignId,
        mode,
        status: campaign.status,
        createdAt: campaign.startedAt || campaign.createdAt,
        sessions,
        completedRounds,
        totalRounds,
        totalQuestionsAnswered,
      };
    });
  }, [campaigns]);

  // Filtered Sessions
  const filteredSessions = useMemo(() => {
    return sessionList.filter((session) => {
      const modeMatch = modeFilter === 'All' || session.mode === modeFilter;

      const roundMatch = roundFilter === 'All' || session.sessions.some((s) => {
        const type = (s.interviewRoundType || '').toLowerCase();
        const target = roundFilter.toLowerCase();
        if (target === 'technical') return type === 'technical';
        if (target === 'behavioral' || target === 'behavior') return type === 'behavior' || type === 'behavioral';
        if (target === 'coding' || target === 'code') return type === 'code' || type === 'coding';
        return false;
      });

      const statusMatch = !statusFilter || session.status === statusFilter;

      const jobTitle = session.campaign?.jobTitle || session.campaign?.JobTitle || session.campaign?.roleTarget || '';
      const searchable = `session #${session.id} ${session.mode} ${session.status} ${jobTitle} ${session.sessions.map((s) => s.interviewRoundType).join(' ')}`.toLowerCase();
      const queryMatch = !query || searchable.includes(query.trim().toLowerCase());

      return modeMatch && roundMatch && statusMatch && queryMatch;
    });
  }, [modeFilter, query, roundFilter, sessionList, statusFilter]);

  // Reset to page 1 whenever filters change
  useEffect(() => {
    setCurrentPage(1);
  }, [query, modeFilter, roundFilter, statusFilter]);

  const totalItems = filteredSessions.length;
  const totalPages = Math.ceil(totalItems / pageSize) || 1;

  const paginatedSessions = useMemo(() => {
    const start = (currentPage - 1) * pageSize;
    return filteredSessions.slice(start, start + pageSize);
  }, [currentPage, filteredSessions, pageSize]);

  // Session-based Summary Metrics
  const summary = useMemo(() => {
    const total = sessionList.length;
    const practiceCount = sessionList.filter((s) => s.mode === 'Practice').length;
    const mockCount = sessionList.filter((s) => s.mode === 'Mock').length;
    const completedCount = sessionList.filter((s) => s.status === 'Completed').length;
    const completionRate = total > 0 ? Math.round((completedCount / total) * 100) : 0;

    return {
      total,
      practiceCount,
      mockCount,
      completedCount,
      completionRate,
    };
  }, [sessionList]);

  const resetFilters = () => {
    setQuery('');
    setModeFilter('All');
    setRoundFilter('All');
    setStatusFilter('');
  };

  const handleStartNewInterview = () => {
    beginNewInterviewCampaign();
    navigate(USER_ROUTES.INTERVIEW_MODE);
  };

  const modeTabOptions = [
    { id: 'All', label: copy.modes.All },
    { id: 'Practice', label: copy.modes.Practice },
    { id: 'Mock', label: copy.modes.Mock },
  ];

  return (
    <UserLayout>
      <div className="flex flex-col gap-8 pb-12 max-w-7xl mx-auto animate-pageEntrance">

        {/* Question Bank Styled Header */}
        <div>
          <h1 className="text-3xl font-bold text-text-primary tracking-tight mb-1">
            {copy.history.title}
          </h1>
          <p className="text-base text-text-secondary leading-relaxed max-w-4xl">
            {copy.history.subtitle}
          </p>
        </div>

        {/* Session-based Summary Metrics */}
        <section className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          <Card variant="elevated" className="p-4 flex items-center gap-3">
            <div className="p-3 bg-primary-xlight text-primary rounded-xl shrink-0">
              <History size={22} />
            </div>
            <div>
              <span className="text-xs font-semibold text-text-muted block">{copy.history.total}</span>
              <span className="text-2xl font-extrabold text-text-primary">{summary.total} <span className="text-xs font-normal text-text-secondary">{copy.history.sessionsUnit}</span></span>
            </div>
          </Card>

          <Card variant="elevated" className="p-4 flex items-center gap-3">
            <div className="p-3 bg-secondary-xlight text-secondary rounded-xl shrink-0">
              <Sparkles size={22} />
            </div>
            <div>
              <span className="text-xs font-semibold text-text-muted block">{copy.history.practiceCount}</span>
              <span className="text-2xl font-extrabold text-text-primary">{summary.practiceCount} <span className="text-xs font-normal text-text-secondary">{copy.history.sessionsUnit}</span></span>
            </div>
          </Card>

          <Card variant="elevated" className="p-4 flex items-center gap-3">
            <div className="p-3 bg-warning-light text-warning rounded-xl shrink-0">
              <Trophy size={22} />
            </div>
            <div>
              <span className="text-xs font-semibold text-text-muted block">{copy.history.mockCount}</span>
              <span className="text-2xl font-extrabold text-text-primary">{summary.mockCount} <span className="text-xs font-normal text-text-secondary">{copy.history.sessionsUnit}</span></span>
            </div>
          </Card>

          <Card variant="elevated" className="p-4 flex items-center gap-3">
            <div className="p-3 bg-success-light text-success rounded-xl shrink-0">
              <CheckCircle2 size={22} />
            </div>
            <div>
              <span className="text-xs font-semibold text-text-muted block">{copy.history.completionRate}</span>
              <span className="text-2xl font-extrabold text-text-primary">{summary.completionRate}%</span>
            </div>
          </Card>
        </section>

        {/* Question Bank Styled Search & Filter Bar */}
        <div className="flex flex-col sm:flex-row gap-2.5">

          {/* Quick Mode Filter Tabs */}
          <div className="flex items-center gap-1.5 overflow-x-auto">
            {modeTabOptions.map((tab) => {
              const isActive = modeFilter === tab.id;
              return (
                <button
                  key={tab.id}
                  type="button"
                  onClick={() => setModeFilter(tab.id)}
                  className={`px-4 py-3.5 rounded-xl text-xs font-bold transition-all duration-300 whitespace-nowrap cursor-pointer uppercase tracking-wider border shadow-sm ${isActive
                      ? 'border-primary bg-primary-xlight text-primary-dark'
                      : 'border-border bg-surface-2 text-text-secondary hover:bg-surface-3 hover:text-text-primary'
                    }`}
                >
                  {tab.label}
                </button>
              );
            })}

            <button
              type="button"
              onClick={handleStartNewInterview}
              className="bg-primary hover:bg-primary-dark hover:shadow-lg text-white text-xs font-bold px-6 py-3.5 rounded-xl transition-all duration-300 cursor-pointer whitespace-nowrap uppercase tracking-wider shadow-md flex items-center gap-1.5"
            >
              <Plus size={16} />
              {copy.history.newInterviewBtn}
            </button>
          </div>
        </div>

        {/* Content Area: Loading, Error, Empty, or Session Card Grid */}
        {isLoading ? (
          <div className="py-16 flex flex-col items-center justify-center gap-3">
            <Spinner size="lg" label={copy.history.loading} />
            <p className="text-xs text-text-muted font-medium">{copy.history.loading}</p>
          </div>
        ) : error ? (
          <Alert variant="error" title={copy.history.loadTitle} className="my-4">
            <div className="flex items-center justify-between gap-4 w-full">
              <span>{error}</span>
              <Button variant="outline" size="sm" icon={RefreshCw} onClick={loadHistory}>
                {copy.history.retry}
              </Button>
            </div>
          </Alert>
        ) : !sessionList.length ? (
          <EmptyState
            icon={CalendarClock}
            title={copy.history.emptyTitle}
            description={copy.history.emptyDescription}
            actionLabel={copy.history.newInterviewBtn}
            onAction={handleStartNewInterview}
          />
        ) : !filteredSessions.length ? (
          <EmptyState
            icon={Search}
            title={copy.history.noResultsTitle}
            description={copy.history.noResultsDescription}
            actionLabel={copy.history.clearFilters}
            onAction={resetFilters}
          />
        ) : (
          <div className="flex flex-col gap-6">
            {/* Session Cards Grid */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
              {paginatedSessions.map((sessionItem) => {
                const { campaign, id, mode, status, createdAt, sessions, completedRounds, totalRounds } = sessionItem;
                const sConfig = getStatusConfig(status, copy);
                const isMock = mode === 'Mock';

                const sessionTitle = formatInterviewTitle(campaign, copy);

                const reviewableRound = sessions.find(canReviewSession);
                const progressPercent = totalRounds > 0 ? Math.round((completedRounds / totalRounds) * 100) : 0;

                return (
                  <Card
                    key={id}
                    variant={isMock ? 'ai' : 'default'}
                    className="p-5 flex flex-col justify-between hover:border-primary/40 hover:shadow-md transition-all group relative overflow-hidden"
                  >
                    {/* Top Header: Mode Badge + Status Badge */}
                    <div className="flex flex-col gap-3">
                      <div className="flex items-start justify-between gap-2">
                        <Badge
                          variant={isMock ? 'secondary' : 'primary'}
                          size="sm"
                          icon={isMock ? Sparkles : Play}
                        >
                          {isMock ? copy.modes.Mock : copy.modes.Practice}
                        </Badge>

                        <Badge variant={sConfig.variant} size="sm" icon={sConfig.icon}>
                          {sConfig.label}
                        </Badge>
                      </div>

                      {/* Title, Timestamp, Secondary Session ID & Overall Score */}
                      <div className="flex items-start justify-between gap-2">
                        <div className="flex flex-col gap-0.5 flex-1 min-w-0">
                          <h3
                            className="text-base font-extrabold text-text-primary group-hover:text-primary transition-colors line-clamp-1"
                            title={sessionTitle}
                          >
                            {sessionTitle}
                          </h3>
                          <div className="flex items-center gap-2 text-[11px] text-text-muted mt-0.5 flex-wrap">
                            <span className="flex items-center gap-1">
                              <Clock size={12} /> {formatDate(createdAt) || 'Gần đây'}
                            </span>

                            {campaign.language && (
                              <span className="px-1.5 py-0.2 bg-surface-muted border border-border/70 rounded text-[10px] uppercase font-bold text-text-secondary">
                                {campaign.language}
                              </span>
                            )}
                          </div>
                        </div>

                        {/* Prominent Final Overall Score */}
                        {status === 'Completed' && (
                          <div className="flex flex-col items-end shrink-0 pl-2 bg-primary-xlight/60 px-2.5 py-1 rounded-lg border border-primary/20">
                            <span className="text-[9px] uppercase font-extrabold text-primary-dark tracking-wider">
                              {copy.history.overallScore}
                            </span>
                            <span className="text-base font-black text-primary drop-shadow-sm">
                              {formatScore(campaign.overallScore ?? campaign.OverallScore, 10) || '10.0/10'}
                            </span>
                          </div>
                        )}
                      </div>

                      {/* Round List Pills with Checkmarks */}
                      <div className="flex flex-col gap-2 my-1">
                        <span className="text-[11px] font-semibold text-text-muted">
                          {copy.history.roundsCount.replace('{{count}}', totalRounds)}:
                        </span>

                        <div className="flex flex-wrap gap-1.5">
                          {sessions.map((s, idx) => {
                            const rConf = getRoundConfig(s.interviewRoundType, copy);
                            const isRoundCompleted = s.status === 'Completed';

                            return (
                              <span
                                key={s.interviewSessionId || idx}
                                className={`
                                  inline-flex items-center gap-1 px-2.5 py-1 rounded-md text-xs font-semibold border transition-all
                                  ${isRoundCompleted
                                    ? 'bg-success-light/40 text-success border-success/30'
                                    : 'bg-surface-muted text-text-secondary border-border'
                                  }
                                `}
                              >
                                {isRoundCompleted ? <Check size={12} /> : <Clock size={12} />}
                                {rConf.label}
                              </span>
                            );
                          })}
                        </div>
                      </div>

                      {/* Session Progress Bar */}
                      <div className="flex flex-col gap-1 mt-1 p-3 bg-surface-muted/60 rounded-lg border border-border/50">
                        <div className="flex items-center justify-between text-xs font-semibold">
                          <span className="text-text-secondary">
                            {copy.history.roundsProgress.replace('{{completed}}', completedRounds).replace('{{total}}', totalRounds)}
                          </span>
                          <span className="text-text-primary font-bold">{progressPercent}%</span>
                        </div>
                        <ProgressBar
                          value={completedRounds}
                          max={totalRounds || 1}
                          variant={completedRounds === totalRounds ? 'success' : 'primary'}
                          size="sm"
                        />
                      </div>
                    </div>

                    {/* Card Actions Footer */}
                    <div className="flex items-center justify-end gap-2 pt-4 mt-4 border-t border-border/60">
                      {status === 'Completed' && reviewableRound ? (
                        <>
                          <button
                            type="button"
                            onClick={() => setSelectedCampaign(campaign)}
                            className="text-xs font-semibold text-text-secondary hover:text-primary transition-colors focus-ring rounded mr-auto"
                          >
                            {copy.history.viewDetailBtn}
                          </button>

                          <Button
                            variant="primary"
                            size="sm"
                            icon={ChevronRight}
                            onClick={() => {
                              const isCoding = ['Code', 'Coding'].includes(reviewableRound.interviewRoundType);
                              if (isCoding) {
                                navigate(getCampaignResultPath(campaign.interviewCampaignId));
                              } else {
                                navigate(getInterviewReviewPath(reviewableRound.interviewSessionId));
                              }
                            }}
                            className="shadow-sm"
                          >
                            {copy.history.viewResultBtn}
                          </Button>
                        </>
                      ) : (
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => setSelectedCampaign(campaign)}
                        >
                          {copy.history.viewDetailBtn}
                        </Button>
                      )}
                    </div>
                  </Card>
                );
              })}
            </div>

            {/* Pagination Controls */}
            {totalPages > 1 && (
              <Pagination
                currentPage={currentPage}
                totalPages={totalPages}
                onPageChange={(page) => setCurrentPage(page)}
                totalItems={totalItems}
                pageSize={pageSize}
                className="rounded-xl border border-border"
              />
            )}
          </div>
        )}

        {/* Campaign Detail Modal */}
        {selectedCampaign && (
          <SessionDetailModal
            campaign={selectedCampaign}
            copy={copy}
            onClose={() => setSelectedCampaign(null)}
          />
        )}
      </div>
    </UserLayout>
  );
}

export default InterviewHistoryPage;
