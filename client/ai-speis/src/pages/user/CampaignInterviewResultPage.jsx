import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  AlertCircle,
  ArrowLeft,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  ClipboardCheck,
  Clock,
  Code2,
  FileQuestion,
  FileText,
  Layers,
  Lightbulb,
  MessageSquareText,
  Play,
  Sparkles,
  Target,
  UserCheck,
} from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';
import interviewSessionService from '../../services/InterviewSessionService';
import technicalInterviewApi from '../../services/technicalInterviewApi';
import behavioralInterviewApi from '../../services/behavioralInterviewApi';
import { normalizeTechnicalInterviewResult } from '../../features/technicalInterview/technicalInterviewResult';
import { getInterviewHistoryCopy, formatInterviewTitle } from '../../features/interviewHistory/interviewHistoryCopy';

// UI Primitives
import Card from '../../components/UI/Card';
import Button from '../../components/UI/Button';
import Badge from '../../components/UI/Badge';
import Spinner from '../../components/UI/Spinner';
import Alert from '../../components/UI/Alert';
import EmptyState from '../../components/UI/EmptyState';

const formatDate = (value) => {
  if (!value) return null;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  return new Intl.DateTimeFormat('vi-VN', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(date);
};

const formatScore = (score, maxScore = 10) => {
  const numericScore = Number(score);
  if (!Number.isFinite(numericScore)) return null;
  const numericMaxScore = Number(maxScore);
  return Number.isFinite(numericMaxScore) && numericMaxScore > 0
    ? `${numericScore.toFixed(1)}/${numericMaxScore}`
    : numericScore.toFixed(1);
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
      return { label: copy.statuses.Cancelled, variant: 'neutral', icon: AlertCircle };
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

const normalizeBehaviorReview = (result, state) => {
  const answers = (state?.transcript || []).filter((entry) => String(entry.role).toLowerCase() === 'candidate');
  return {
    overallScore: result?.overallScore,
    maxScore: result?.maxScore,
    questions: (result?.mainQuestions || []).map((question) => ({
      id: question.sessionQuestionId,
      order: question.mainQuestionIndex,
      question: question.question,
      questionType: 'MAIN',
      skill: question.skill,
      score: question.score,
      maxScore: result?.maxScore || 10,
      dimensions: question.dimensions || [],
      strengths: question.strengths || [],
      missingPoints: question.missingPoints || [],
      transcript: answers.find((answer) => answer.sessionQuestionId === question.sessionQuestionId)?.content || '',
      feedbackSummary: '',
      suggestions: result?.summary?.recommendationsForImprovement || [],
    })),
  };
};

const normalizeTechnicalReview = (result) => {
  const normalized = normalizeTechnicalInterviewResult(result);
  return {
    overallScore: normalized?.technicalScore,
    maxScore: normalized?.maxScore,
    questions: (normalized?.questionResults || []).map((question) => ({
      id: question.attemptId || question.mainQuestionIndex,
      order: question.mainQuestionIndex,
      question: question.content || question.question,
      questionType: question.questionType,
      skill: question.skill || question.targetSkill,
      score: question.score,
      maxScore: question.maxScore || 10,
      dimensions: question.rubricBreakdown || question.dimensions || [],
      strengths: question.strengths || [],
      missingPoints: question.missingPoints || [],
      transcript: question.answerTranscript || '',
      feedbackSummary: question.feedbackSummary,
      suggestions: question.suggestions || [],
      adaptiveHistory: question.subQuestionResults || [],
    })),
  };
};

function FeedbackList({ icon: Icon, title, items, tone = 'neutral' }) {
  if (!items?.length) return null;
  return (
    <div className={`p-4 rounded-xl border flex flex-col gap-2 ${
      tone === 'positive' 
        ? 'bg-success-light/30 border-success/30 text-success-dark' 
        : tone === 'focus' 
        ? 'bg-warning-light/30 border-warning/30 text-warning-dark'
        : 'bg-primary-xlight/30 border-primary/30 text-primary-dark'
    }`}>
      <h4 className="text-xs font-bold uppercase tracking-wider flex items-center gap-1.5">
        <Icon size={16} /> {title}
      </h4>
      <ul className="list-disc list-inside text-xs text-text-primary space-y-1">
        {items.map((item, idx) => (
          <li key={`${item}-${idx}`}>{item}</li>
        ))}
      </ul>
    </div>
  );
}

function CampaignInterviewResultPage({ campaignId }) {
  const { i18n } = useTranslation();
  const copy = getInterviewHistoryCopy(i18n.resolvedLanguage || i18n.language);

  const [campaignResult, setCampaignResult] = useState(null);
  const [campaignData, setCampaignData] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');

  // Round Selection State
  const [activeRoundSessionId, setActiveRoundSessionId] = useState(null);
  const [roundReview, setRoundReview] = useState(null);
  const [isRoundLoading, setIsRoundLoading] = useState(false);
  const [roundError, setRoundError] = useState('');
  const [selectedQuestionIndex, setSelectedQuestionIndex] = useState(0);

  const loadCampaign = useCallback(async () => {
    if (!campaignId) {
      setError(copy.review.missingId);
      setIsLoading(false);
      return;
    }
    setIsLoading(true);
    setError('');
    try {
      const [res, myCampaigns] = await Promise.all([
        interviewSessionService.getCampaignResult(campaignId).catch(() => null),
        interviewSessionService.getMyCampaigns().catch(() => []),
      ]);

      const foundCampaign = (myCampaigns || []).find((c) => String(c.interviewCampaignId) === String(campaignId));
      setCampaignResult(res);
      setCampaignData(foundCampaign);

      const firstSession = foundCampaign?.sessions?.[0] || res?.rounds?.[0];
      if (firstSession) {
        setActiveRoundSessionId(firstSession.interviewSessionId);
      }
    } catch (loadError) {
      setError(copy.history.loadError);
    } finally {
      setIsLoading(false);
    }
  }, [campaignId, copy.history.loadError, copy.review.missingId]);

  useEffect(() => {
    loadCampaign();
  }, [loadCampaign]);

  // Active round object
  const activeSessionObj = useMemo(() => {
    const fromCampaign = (campaignData?.sessions || []).find((s) => s.interviewSessionId === activeRoundSessionId);
    const fromResult = (campaignResult?.rounds || []).find((r) => r.interviewSessionId === activeRoundSessionId);
    return { ...fromCampaign, ...fromResult };
  }, [activeRoundSessionId, campaignData?.sessions, campaignResult?.rounds]);

  // Fetch detailed review when active round changes (for Technical / Behavior)
  const loadRoundDetail = useCallback(async () => {
    if (!activeRoundSessionId || !activeSessionObj) return;
    const roundType = activeSessionObj.interviewRoundType || activeSessionObj.roundType;
    if (['Code', 'Coding'].includes(roundType)) {
      setRoundReview(null);
      setIsRoundLoading(false);
      return;
    }

    setIsRoundLoading(true);
    setRoundError('');
    try {
      let reviewData;
      if (roundType === 'Technical') {
        const [res, state] = await Promise.all([
          technicalInterviewApi.getResult(activeRoundSessionId),
          technicalInterviewApi.getSession(activeRoundSessionId),
        ]);
        reviewData = normalizeTechnicalReview(res, state);
      } else if (['Behavior', 'Behavioral'].includes(roundType)) {
        const [res, state] = await Promise.all([
          behavioralInterviewApi.getResult(activeRoundSessionId),
          behavioralInterviewApi.getState(activeRoundSessionId),
        ]);
        reviewData = normalizeBehaviorReview(res, state);
      }
      setRoundReview(reviewData);
      setSelectedQuestionIndex(0);
    } catch (err) {
      setRoundError(copy.review.loadError);
    } finally {
      setIsRoundLoading(false);
    }
  }, [activeRoundSessionId, activeSessionObj, copy.review.loadError]);

  useEffect(() => {
    loadRoundDetail();
  }, [loadRoundDetail]);

  if (isLoading) {
    return (
      <UserLayout>
        <div className="py-20 flex flex-col items-center justify-center gap-3">
          <Spinner size="lg" label={copy.history.loading} />
          <p className="text-xs text-text-muted font-medium">{copy.history.loading}</p>
        </div>
      </UserLayout>
    );
  }

  if (error || (!campaignData && !campaignResult)) {
    return (
      <UserLayout>
        <div className="max-w-4xl mx-auto py-12">
          <Alert variant="error" title={copy.history.loadTitle}>
            <div className="flex items-center justify-between gap-4 w-full">
              <span>{error || copy.history.loadError}</span>
              <Button variant="outline" size="sm" icon={ArrowLeft} onClick={() => navigate(USER_ROUTES.INTERVIEW_HISTORY)}>
                {copy.review.backHistory}
              </Button>
            </div>
          </Alert>
        </div>
      </UserLayout>
    );
  }

  const mode = (campaignData?.mode || campaignResult?.mode || '').toLowerCase().includes('mock') || (campaignData?.sessions?.length === 3)
    ? 'Mock'
    : 'Practice';

  const sessionName = formatInterviewTitle(campaignData || campaignResult, copy);
  const status = campaignData?.status || campaignResult?.status || 'Completed';
  const sConfig = getStatusConfig(status, copy);
  const overallScore = campaignResult?.overallScore ?? campaignData?.overallScore ?? campaignData?.OverallScore;
  const sessionsList = campaignData?.sessions || campaignResult?.rounds || [];
  const selectedQuestion = roundReview?.questions?.[selectedQuestionIndex] || null;

  return (
    <UserLayout>
      <div className="flex flex-col gap-6 pb-16 max-w-7xl mx-auto animate-pageEntrance">
        
        {/* Header Session: Unwrapped Clean Flex Row (Left Info + Right Score) */}
        <div className="flex flex-col gap-3 w-full">
          <button
            type="button"
            onClick={() => navigate(USER_ROUTES.INTERVIEW_HISTORY)}
            className="flex items-center gap-1.5 text-xs font-bold text-text-secondary hover:text-primary transition-colors cursor-pointer w-fit"
          >
            <ArrowLeft size={16} />
            {copy.review.back}
          </button>

          {/* Always Single Horizontal Row: Left Info + Right Overall Score */}
          <div className="flex items-center justify-between gap-4 w-full">
            {/* Left: Session Title, Mode & Status Badges, Timestamp */}
            <div className="flex flex-col gap-1.5 text-left flex-1 min-w-0">
              <div className="flex items-center gap-2.5 flex-wrap">
                <h1 className="text-2xl sm:text-3xl font-extrabold text-text-primary tracking-tight">
                  {sessionName}
                </h1>
                <Badge variant={mode === 'Mock' ? 'secondary' : 'primary'} size="sm" icon={mode === 'Mock' ? Sparkles : Play}>
                  {mode === 'Mock' ? copy.modes.Mock : copy.modes.Practice}
                </Badge>
                <Badge variant={sConfig.variant} size="sm">
                  {sConfig.label}
                </Badge>
              </div>

              {formatDate(campaignData?.startedAt || campaignData?.createdAt) && (
                <div className="flex items-center gap-2 text-xs text-text-secondary flex-wrap">
                  <Clock size={13} className="text-text-muted" />
                  <span>{formatDate(campaignData?.startedAt || campaignData?.createdAt)}</span>
                  {campaignData?.language && (
                    <span className="px-1.5 py-0.5 bg-surface-muted border border-border rounded text-[10px] uppercase font-bold text-text-secondary">
                      {campaignData.language}
                    </span>
                  )}
                </div>
              )}
            </div>

            {/* Right: Level 1 Overall Score Badge on SAME horizontal row */}
            <div className="flex flex-col items-center justify-center px-4 py-2 bg-primary-xlight/60 border border-primary/20 rounded-xl shrink-0 ml-auto">
              <span className="text-[10px] uppercase font-extrabold text-primary-dark tracking-wider">
                {copy.history.overallScore}
              </span>
              <span className="text-2xl sm:text-3xl font-black text-primary">
                {formatScore(overallScore, 10) || copy.history.noScore}
              </span>
            </div>
          </div>
        </div>

        {/* Khối "CÁC VÒNG PHỎNG VẤN TRONG BUỔI NÀY" Container */}
        <section className="flex flex-col gap-3">
          <h3 className="text-xs font-bold uppercase tracking-wider text-text-muted">
            {copy.history.sessionRounds}
          </h3>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            {sessionsList.map((s) => {
              const rType = s.interviewRoundType || s.roundType;
              const rConfig = getRoundConfig(rType, copy);
              const isSelected = s.interviewSessionId === activeRoundSessionId;

              const roundResultObj = (campaignResult?.rounds || []).find((r) => r.interviewSessionId === s.interviewSessionId);
              const roundScore = roundResultObj?.score ?? s.score;

              return (
                <button
                  key={s.interviewSessionId}
                  type="button"
                  onClick={() => setActiveRoundSessionId(s.interviewSessionId)}
                  className={`
                    p-4 rounded-xl border text-left transition-all duration-300 cursor-pointer flex flex-col justify-between gap-3 shadow-xs
                    ${isSelected
                      ? 'border-[1.5px] border-primary bg-[#F0F7FF] shadow-sm ring-1 ring-primary/20'
                      : 'border-border bg-surface hover:border-border-strong hover:bg-surface-2'
                    }
                  `}
                >
                  <div className="flex items-center justify-between gap-2 w-full">
                    <Badge variant={rConfig.variant} size="sm" icon={rConfig.icon}>
                      {rConfig.label}
                    </Badge>
                    {getStatusConfig(s.status, copy).label && (
                      <span className="text-[11px] font-semibold text-text-muted">
                        {getStatusConfig(s.status, copy).label}
                      </span>
                    )}
                  </div>

                  <div className="flex items-end justify-between w-full pt-2 border-t border-border/50">
                    <span className="text-xs font-semibold text-text-secondary">{copy.history.totalScore}</span>
                    <span className="text-lg font-extrabold text-primary">
                      {formatScore(roundScore, 10) || copy.history.noScore}
                    </span>
                  </div>
                </button>
              );
            })}
          </div>
        </section>

        {/* Selected Round Review Area */}
        {activeSessionObj && (
          <section className="flex flex-col gap-4">

            {/* CODING COMPACT REVIEW */}
            {['Code', 'Coding'].includes(activeSessionObj.interviewRoundType || activeSessionObj.roundType) ? (
              <Card variant="default" className="p-6 border border-border rounded-xl shadow-xs flex flex-col gap-4">
                <div className="flex items-center justify-between">
                  <h3 className="text-xs font-bold uppercase tracking-wider text-text-muted">
                    {copy.history.round}: {copy.rounds.Code}
                  </h3>
                  <span className="text-xs text-text-secondary">
                    {copy.history.totalScore}: <strong className="text-primary text-sm font-bold">{formatScore(activeSessionObj.score, 10) || '10/10'}</strong>
                  </span>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                  {(activeSessionObj.codingQuestions || []).length > 0 ? (
                    activeSessionObj.codingQuestions.map((cq, idx) => (
                      <div key={cq.codingQuestionId || idx} className="p-4 bg-surface-2 rounded-xl border border-border flex flex-col gap-2">
                        <div className="flex items-center justify-between">
                          <span className="text-xs font-bold text-text-muted uppercase">Q{idx + 1}</span>
                          <span className="text-xs font-extrabold text-primary">{formatScore(cq.score, 10) || '10/10'}</span>
                        </div>
                        <h4 className="text-sm font-bold text-text-primary line-clamp-1">{cq.title || `Coding question #${cq.codingQuestionId}`}</h4>
                        <div className="text-xs text-text-secondary font-semibold mt-1">
                          Testcase pass: <strong className="text-success-dark font-bold">{cq.passedTestCases}/{cq.totalTestCases}</strong>
                        </div>
                      </div>
                    ))
                  ) : (
                    <div className="col-span-full p-6 bg-surface-muted rounded-xl text-center text-xs text-text-secondary">
                      Chưa có câu hỏi lập trình nào được ghi nhận cho vòng này.
                    </div>
                  )}
                </div>
              </Card>
            ) : isRoundLoading ? (
              <div className="py-12 flex flex-col items-center justify-center gap-3">
                <Spinner size="md" label={copy.review.loading} />
              </div>
            ) : roundError ? (
              <Alert variant="error" title={copy.review.loadTitle}>
                {roundError}
              </Alert>
            ) : !roundReview?.questions?.length ? (
              <EmptyState
                icon={FileText}
                title={copy.review.emptyTitle}
                description={copy.review.emptyDescription}
              />
            ) : (
              /* SPLIT-VIEW BÊN DƯỚI (CHIA 2 CỘT: CỘT TRÁI 30% / CỘT PHẢI 70%) */
              <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
                
                {/* Cột Trái: Danh sách câu hỏi (30% / lg:col-span-4) */}
                <aside className="lg:col-span-4 flex flex-col gap-3">
                  <div className="p-4 bg-surface border border-border rounded-xl shadow-xs flex flex-col gap-3">
                    <div className="flex items-center justify-between border-b border-border pb-2.5">
                      <h4 className="text-xs font-bold uppercase tracking-wider text-text-muted">
                        {copy.review.questionList}
                      </h4>
                      <span className="px-2 py-0.5 bg-surface-2 border border-border rounded-full text-xs font-bold text-text-primary">
                        ({selectedQuestionIndex + 1}/{roundReview.questions.length})
                      </span>
                    </div>

                    <div className="flex flex-col gap-2">
                      {roundReview.questions.map((q, index) => {
                        const isQSelected = index === selectedQuestionIndex;
                        return (
                          <button
                            key={q.id || index}
                            type="button"
                            onClick={() => setSelectedQuestionIndex(index)}
                            className={`
                              p-3.5 rounded-xl border text-left transition-all cursor-pointer flex flex-col gap-1.5 relative overflow-hidden
                              ${isQSelected
                                ? 'border-primary bg-[#F0F7FF] border-l-4 border-l-primary shadow-xs'
                                : 'border-border bg-surface hover:bg-surface-2'
                              }
                            `}
                          >
                            <div className="flex items-center justify-between text-xs">
                              <span className="font-extrabold text-primary">Q{q.order || index + 1}</span>
                              {formatScore(q.score, q.maxScore) && (
                                <span className="font-bold text-text-primary">{formatScore(q.score, q.maxScore)}</span>
                              )}
                            </div>
                            <p className="text-xs text-text-secondary line-clamp-2 font-medium leading-snug">
                              {q.question || copy.review.missingQuestion}
                            </p>
                          </button>
                        );
                      })}
                    </div>
                  </div>
                </aside>

                {/* Cột Phải: Nội dung chi tiết câu hỏi & Đánh giá (70% / lg:col-span-8) */}
                <article className="lg:col-span-8 p-6 bg-surface border border-border rounded-xl shadow-xs flex flex-col gap-6">
                  
                  {/* Header câu hỏi & Score */}
                  <div className="flex items-start justify-between gap-4 pb-4 border-b border-border">
                    <div className="flex flex-col gap-1.5 flex-1">
                      <span className="text-[10px] font-extrabold uppercase tracking-wider text-text-muted">
                        {selectedQuestion.questionType === 'MAIN' ? copy.review.mainQuestion : selectedQuestion.questionType}
                      </span>
                      <h3 className="text-base sm:text-lg font-bold text-text-primary leading-snug">
                        {selectedQuestion.question}
                      </h3>
                      {selectedQuestion.skill && (
                        <span className="text-xs font-semibold text-secondary mt-1">
                          {copy.review.skill.replace('{{skill}}', selectedQuestion.skill)}
                        </span>
                      )}
                    </div>

                    {formatScore(selectedQuestion.score, selectedQuestion.maxScore) && (
                      <div className="flex flex-col items-center px-4 py-2 bg-[#F0F7FF] border border-primary/20 rounded-xl shrink-0">
                        <span className="text-[9px] uppercase font-extrabold text-primary-dark tracking-wider">Điểm</span>
                        <span className="text-lg font-extrabold text-primary">
                          {formatScore(selectedQuestion.score, selectedQuestion.maxScore)}
                        </span>
                      </div>
                    )}
                  </div>

                  {/* Transcript câu trả lời Sub-box */}
                  <div className="flex flex-col gap-2.5 p-4.5 bg-[#F8FAFC] rounded-xl border border-border">
                    <h4 className="text-xs font-bold uppercase tracking-wider text-text-secondary flex items-center gap-2">
                      <MessageSquareText size={16} className="text-primary" /> {copy.review.transcript}
                    </h4>
                    <p className="text-xs text-text-primary leading-relaxed whitespace-pre-wrap font-mono">
                      {selectedQuestion.transcript || copy.review.missingTranscript}
                    </p>
                  </div>

                  {/* AI Feedback Summary */}
                  {selectedQuestion.feedbackSummary && (
                    <div className="p-4 bg-primary-xlight/20 rounded-xl border border-primary/20 flex flex-col gap-1.5">
                      <h4 className="text-xs font-bold uppercase tracking-wider text-primary-dark flex items-center gap-1.5">
                        <ClipboardCheck size={15} /> {copy.review.aiFeedback}
                      </h4>
                      <p className="text-xs text-text-primary leading-relaxed">
                        {selectedQuestion.feedbackSummary}
                      </p>
                    </div>
                  )}

                  {/* Rubric Breakdown */}
                  {selectedQuestion.dimensions?.length > 0 && (
                    <div className="flex flex-col gap-3">
                      <h4 className="text-xs font-bold uppercase tracking-wider text-text-muted flex items-center gap-1.5">
                        <Target size={15} /> {copy.review.rubric}
                      </h4>
                      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                        {selectedQuestion.dimensions.map((dim, idx) => (
                          <div key={dim.rubricCode || idx} className="p-3.5 bg-surface-muted rounded-xl border border-border flex flex-col gap-1 text-xs">
                            <div className="flex items-center justify-between">
                              <strong className="text-text-primary font-bold">{dim.name || dim.rubricCode}</strong>
                              {formatScore(dim.score, dim.maxScore) && (
                                <span className="font-extrabold text-primary">{formatScore(dim.score, dim.maxScore)}</span>
                              )}
                            </div>
                            {dim.evidence?.length > 0 && (
                              <p className="text-[11px] text-text-secondary mt-1">{dim.evidence.join(' ')}</p>
                            )}
                          </div>
                        ))}
                      </div>
                    </div>
                  )}

                  {/* Strengths, Improvements & AI Practice Tips */}
                  <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                    <FeedbackList icon={CheckCircle2} title={copy.review.strengths} items={selectedQuestion.strengths} tone="positive" />
                    <FeedbackList icon={Target} title={copy.review.improvements} items={selectedQuestion.missingPoints} tone="focus" />
                    <FeedbackList icon={Lightbulb} title={copy.review.practiceTips} items={selectedQuestion.suggestions} tone="next" />
                  </div>

                  {/* Navigation Footer */}
                  <div className="flex items-center justify-between pt-4 border-t border-border mt-2">
                    <Button
                      variant="outline"
                      size="sm"
                      icon={ChevronLeft}
                      disabled={selectedQuestionIndex === 0}
                      onClick={() => setSelectedQuestionIndex((prev) => prev - 1)}
                    >
                      {copy.review.previous}
                    </Button>
                    <span className="text-xs font-semibold text-text-muted">
                      {selectedQuestionIndex + 1} / {roundReview.questions.length}
                    </span>
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={selectedQuestionIndex === roundReview.questions.length - 1}
                      onClick={() => setSelectedQuestionIndex((prev) => prev + 1)}
                    >
                      {copy.review.next}
                      <ChevronRight size={16} />
                    </Button>
                  </div>
                </article>
              </div>
            )}
          </section>
        )}
      </div>
    </UserLayout>
  );
}

export default CampaignInterviewResultPage;
