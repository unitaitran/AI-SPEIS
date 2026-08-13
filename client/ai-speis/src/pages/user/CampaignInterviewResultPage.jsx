import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  AlertCircle,
  ArrowLeft,
  CheckCircle2,
  ChevronDown,
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
  RefreshCw,
} from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';
import interviewSessionService from '../../services/InterviewSessionService';
import technicalV2InterviewApi from '../../services/technicalV2InterviewApi';
import behavioralInterviewApi from '../../services/behavioralInterviewApi';
import { normalizeTechnicalV2Review } from '../../features/technicalInterview/technicalV2InterviewResult';
import { getInterviewHistoryCopy, formatInterviewTitle } from '../../features/interviewHistory/interviewHistoryCopy';

// UI Primitives
import Card from '../../components/UI/Card';
import Button from '../../components/UI/Button';
import Badge from '../../components/UI/Badge';
import Spinner from '../../components/UI/Spinner';
import Alert from '../../components/UI/Alert';
import EmptyState from '../../components/UI/EmptyState';

const formatDate = (value, locale = 'vi-VN') => {
  if (!value) return null;
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  return new Intl.DateTimeFormat(locale, {
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

const formatWeight = (weight) => new Intl.NumberFormat(undefined, { style: 'percent', maximumFractionDigits: 0 }).format(Number(weight) || 0);
const technicalCriterionLabel = (code, t) => t(`technicalRoom.rubric.${String(code || '').toUpperCase()}`, { defaultValue: code || '' });
const openSingleQuestionInterview = (question, roundType, originalSessionId) => {
  sessionStorage.setItem('ai-speis:single-question-interview', JSON.stringify({
    questionId: question.questionId,
    question: question.question,
    roundType,
    originalSessionId,
    language: 'vi',
  }));
  navigate(USER_ROUTES.SINGLE_QUESTION_INTERVIEW);
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

const getBehaviouralMissingPoints = (question, roundImprovements = []) => {
  const normaliseItems = (items) => {
    const flatten = (item) => {
      if (typeof item !== 'string' || !item.trim()) return [];
      const value = item.trim();
      if (!value.startsWith('[') || !value.endsWith(']')) return [value];
      try {
        const parsed = JSON.parse(value);
        return Array.isArray(parsed) ? parsed.flatMap(flatten) : [];
      } catch {
        return value.slice(1, -1)
          .split(/\r?\n/)
          .map((line) => line.trim().replace(/^["',\s]+|["',\s]+$/g, ''))
          .filter(Boolean);
      }
    };
    return [...new Set((Array.isArray(items) ? items : []).flatMap(flatten))];
  };
  const rubricLabels = new Set((question?.dimensions || [])
    .flatMap((dimension) => [dimension?.name, dimension?.rubricCode])
    .filter(Boolean)
    .map((label) => String(label).trim().toLowerCase()));
  const removeRubricLabels = (items) => items.filter((item) => !rubricLabels.has(item.toLowerCase()));
  const savedPoints = removeRubricLabels(normaliseItems(question?.missingPoints));
  if (savedPoints.length) return savedPoints;

  const rubricGaps = removeRubricLabels(normaliseItems((question?.dimensions || []).flatMap((dimension) => dimension?.missingEvidence || [])));
  return rubricGaps.length ? rubricGaps : removeRubricLabels(normaliseItems(roundImprovements));
};

const normalizeBehaviorReview = (result, state) => {
  const answers = (state?.transcript || []).filter((entry) => String(entry.role).toLowerCase() === 'candidate');
  const roundFeedback = result?.summary?.overallBehavioralAssessment || result?.summary?.executiveSummary || '';
  const roundImprovements = result?.summary?.weaknesses?.length
    ? result.summary.weaknesses
    : result?.summary?.competencyGaps || [];
  return {
    overallScore: result?.overallScore,
    maxScore: result?.maxScore,
    questions: (result?.mainQuestions || []).map((question) => {
      const subQuestions = (question.subQuestions || []).map((subQ, subIndex) => ({
        id: subQ.sessionQuestionId || subQ.questionId || subIndex,
        attemptId: subQ.sessionQuestionId || subIndex,
        questionOrder: subQ.mainQuestionIndex || subIndex + 1,
        question: subQ.question || '',
        questionType: subQ.questionType || 'SUB',
        skill: subQ.skill || question.skill || '',
        score: Number.isFinite(Number(subQ.score)) ? Number(subQ.score) : 0,
        maxScore: 10,
        dimensions: subQ.dimensions || [],
        strengths: subQ.strengths || [],
        missingPoints: getBehaviouralMissingPoints(subQ, roundImprovements),
        transcript: subQ.answerTranscript || answers.find((ans) => ans.sessionQuestionId === subQ.sessionQuestionId)?.content || '',
        answerTranscript: subQ.answerTranscript || answers.find((ans) => ans.sessionQuestionId === subQ.sessionQuestionId)?.content || '',
      }));

      const mainTranscript = question.answerTranscript
        || answers.find((answer) => answer.sessionQuestionId === question.sessionQuestionId)?.content
        || '';

      return {
        id: question.sessionQuestionId,
        questionId: question.questionId || null,
        order: question.mainQuestionIndex,
        question: question.question,
        questionType: 'MAIN',
        skill: question.skill,
        score: question.score,
        maxScore: result?.maxScore || 10,
        dimensions: question.dimensions || [],
        strengths: question.strengths || [],
        missingPoints: getBehaviouralMissingPoints(question, roundImprovements),
        transcript: mainTranscript,
        feedbackSummary: roundFeedback,
        suggestions: result?.summary?.recommendationsForImprovement || [],
        subQuestions,
        adaptiveHistory: subQuestions,
      };
    }),
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

function CollapsibleCriterionCard({ dim, isTechnicalV2Review, technicalCriterionLabel, copy, t }) {
  const [isOpen, setIsOpen] = useState(false);
  const title = isTechnicalV2Review ? technicalCriterionLabel(dim.rubricCode, t) : (dim.name || dim.rubricCode);
  const hasDetails = dim.evidence?.length > 0 || dim.strengths?.length > 0;

  return (
    <div
      onClick={() => hasDetails && setIsOpen((prev) => !prev)}
      className={`
        p-3.5 bg-surface-muted rounded-xl border border-border flex flex-col gap-1 text-xs transition-all duration-200
        ${hasDetails ? 'cursor-pointer hover:border-primary/50 hover:bg-surface-2' : ''}
      `}
    >
      <div className="flex items-center justify-between gap-2 select-none">
        <div className="flex items-center gap-1.5 min-w-0">
          <strong className="text-text-primary font-bold line-clamp-1">{title}</strong>
          {hasDetails && (
            <ChevronDown size={14} className={`text-text-muted transition-transform duration-200 shrink-0 ${isOpen ? 'rotate-180' : ''}`} />
          )}
        </div>
        {formatScore(dim.score, dim.maxScore) && (
          <span className="font-extrabold text-primary shrink-0">{formatScore(dim.score, dim.maxScore)}</span>
        )}
      </div>

      {isTechnicalV2Review && (
        <span className="text-[11px] text-text-secondary">
          {t('technicalRoom.result.weight', { weight: formatWeight(dim.weight) })}
        </span>
      )}

      {isOpen && (
        <div className="flex flex-col gap-1 mt-2 pt-2 border-t border-border/60">
          {dim.evidence?.length > 0 && (
            <p className="text-[11px] text-text-secondary leading-relaxed">
              <strong>{isTechnicalV2Review ? t('technicalRoom.result.evidence') : copy?.review?.evidence || 'Evidence:'}</strong>{' '}
              {dim.evidence.join(' ')}
            </p>
          )}
          {isTechnicalV2Review && dim.strengths?.length > 0 && (
            <p className="text-[11px] text-text-secondary leading-relaxed">
              <strong>{t('technicalRoom.result.strengths')}</strong> {dim.strengths.join(' ')}
            </p>
          )}
        </div>
      )}
    </div>
  );
};

function CampaignInterviewResultPage({ campaignId }) {
  const { i18n, t } = useTranslation('interview');
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
  const [expandedQuestions, setExpandedQuestions] = useState({});

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
        const res = await technicalV2InterviewApi.getResult(activeRoundSessionId);
        reviewData = normalizeTechnicalV2Review(res);
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

  const flatQuestionList = useMemo(() => {
    if (!roundReview?.questions) return [];
    const list = [];
    roundReview.questions.forEach((q, mainIdx) => {
      list.push({
        ...q,
        key: `main-${q.id || mainIdx}`,
        displayIndex: `Q${q.order || mainIdx + 1}`,
        isSubQuestion: false,
        mainIndex: mainIdx,
        questionLabel: copy.review.mainQuestion || 'MAIN QUESTION',
      });
      (q.subQuestions || []).forEach((subQ, subIdx) => {
        list.push({
          ...subQ,
          key: `sub-${subQ.id || subIdx}`,
          displayIndex: `Q${q.order || mainIdx + 1}.${subIdx + 1}`,
          isSubQuestion: true,
          mainIndex: mainIdx,
          parentQuestion: q,
          questionLabel: subQ.questionType === 'Clarification' ? copy.review.clarificationQuestion : copy.review.followUpQuestion,
        });
      });
    });
    return list;
  }, [roundReview?.questions, copy.review.mainQuestion]);

  const competencyMetrics = useMemo(() => {
    if (!campaignResult && !campaignData) return null;

    const rounds = campaignResult?.rounds || campaignData?.sessions || [];

    // 1. Coding score
    const codingRound = rounds.find((r) => ['Code', 'Coding'].includes(r.interviewRoundType || r.roundType));
    const codingScore = Math.min(10, Math.max(0, Number(codingRound?.score ?? 0)));

    // 2. Technical round overall & fallback
    const techRound = rounds.find((r) => (r.interviewRoundType || r.roundType) === 'Technical');
    const techOverall = Math.min(10, Math.max(0, Number(techRound?.score ?? 0)));

    // 3. Behavioral round overall & fallback
    const behaRound = rounds.find((r) => ['Behavior', 'Behavioral'].includes(r.interviewRoundType || r.roundType));
    const behaOverall = Math.min(10, Math.max(0, Number(behaRound?.score ?? 0)));

    let techAccuracy = techOverall;
    let techDepth = techOverall;
    let techApp = techOverall;
    let techReasoning = techOverall;
    let techComm = techOverall;

    let behaComm = behaOverall;
    let behaAction = behaOverall;

    if (roundReview?.questions?.length) {
      let accSum = 0, accCount = 0;
      let depthSum = 0, depthCount = 0;
      let appSum = 0, appCount = 0;
      let reasSum = 0, reasCount = 0;
      let commSum = 0, commCount = 0;
      let actionSum = 0, actionCount = 0;

      roundReview.questions.forEach((q) => {
        (q.dimensions || []).forEach((d) => {
          const code = String(d.rubricCode || d.name || '').toUpperCase();
          const s = Number(d.score || 0);
          if (code.includes('ACCURACY')) { accSum += s; accCount++; }
          else if (code.includes('DEPTH') || code.includes('COMPETENCY')) { depthSum += s; depthCount++; }
          else if (code.includes('APPLICATION') || code.includes('APP')) { appSum += s; appCount++; }
          else if (code.includes('REASONING') || code.includes('REASON') || code.includes('RESULT')) { reasSum += s; reasCount++; }
          else if (code.includes('COMMUNICATION') || code.includes('COMM')) { commSum += s; commCount++; }
          else if (code.includes('ACTION')) { actionSum += s; actionCount++; }
        });

        (q.subQuestions || []).forEach((sq) => {
          (sq.dimensions || []).forEach((d) => {
            const code = String(d.rubricCode || d.name || '').toUpperCase();
            const s = Number(d.score || 0);
            if (code.includes('ACCURACY')) { accSum += s; accCount++; }
            else if (code.includes('DEPTH') || code.includes('COMPETENCY')) { depthSum += s; depthCount++; }
            else if (code.includes('APPLICATION') || code.includes('APP')) { appSum += s; appCount++; }
            else if (code.includes('REASONING') || code.includes('REASON') || code.includes('RESULT')) { reasSum += s; reasCount++; }
            else if (code.includes('COMMUNICATION') || code.includes('COMM')) { commSum += s; commCount++; }
            else if (code.includes('ACTION')) { actionSum += s; actionCount++; }
          });
        });
      });

      if (accCount > 0) techAccuracy = accSum / accCount;
      if (depthCount > 0) techDepth = depthSum / depthCount;
      if (appCount > 0) techApp = appSum / appCount;
      if (reasCount > 0) techReasoning = reasSum / reasCount;

      if (roundReview.runtimeVersion === 'V2') {
        if (commCount > 0) techComm = commSum / commCount;
      } else {
        if (commCount > 0) behaComm = commSum / commCount;
        if (actionCount > 0) behaAction = actionSum / actionCount;
      }
    }

    // Formulas:
    // 1. Professional Knowledge = 35% Accuracy + 25% Depth + 15% Application + 25% Coding
    const profKnowledge = (0.35 * techAccuracy) + (0.25 * techDepth) + (0.15 * techApp) + (0.25 * codingScore);

    // 2. Communication Skills = 40% Technical Communication + 60% Behavioral Communication
    const commSkills = (0.40 * techComm) + (0.60 * behaComm);

    // 3. CV Understanding = 30% Application + 30% Reasoning + 40% Action
    const cvUnderstanding = (0.30 * techApp) + (0.30 * techReasoning) + (0.40 * behaAction);

    // 4. Problem Solving = 35% Coding + 35% Depth + 30% Reasoning
    const problemSolving = (0.35 * codingScore) + (0.35 * techDepth) + (0.30 * techReasoning);

    return [
      {
        title: copy.history.profKnowledge,
        labelVi: copy.history.profKnowledgeSub,
        score: profKnowledge.toFixed(1),
        formula: '35% Accuracy + 25% Depth + 15% App + 25% Coding',
        color: 'from-blue-50/80 to-indigo-50/80 border-blue-200 dark:from-blue-950/40 dark:to-indigo-950/40 dark:border-blue-800 text-blue-900 dark:text-blue-100',
        badgeColor: 'bg-blue-600 text-white',
      },
      {
        title: copy.history.commSkills,
        labelVi: copy.history.commSkillsSub,
        score: commSkills.toFixed(1),
        formula: '40% Tech Comm + 60% Beha Comm',
        color: 'from-purple-50/80 to-pink-50/80 border-purple-200 dark:from-purple-950/40 dark:to-pink-950/40 dark:border-purple-800 text-purple-900 dark:text-purple-100',
        badgeColor: 'bg-purple-600 text-white',
      },
      {
        title: copy.history.cvUnderstanding,
        labelVi: copy.history.cvUnderstandingSub,
        score: cvUnderstanding.toFixed(1),
        formula: '30% App + 30% Reasoning + 40% Action',
        color: 'from-amber-50/80 to-orange-50/80 border-amber-200 dark:from-amber-950/40 dark:to-orange-950/40 dark:border-amber-800 text-amber-900 dark:text-amber-100',
        badgeColor: 'bg-amber-600 text-white',
      },
      {
        title: copy.history.problemSolving,
        labelVi: copy.history.problemSolvingSub,
        score: problemSolving.toFixed(1),
        formula: '35% Coding + 35% Depth + 30% Reasoning',
        color: 'from-emerald-50/80 to-teal-50/80 border-emerald-200 dark:from-emerald-950/40 dark:to-teal-950/40 dark:border-emerald-800 text-emerald-900 dark:text-emerald-100',
        badgeColor: 'bg-emerald-600 text-white',
      },
    ];
  }, [campaignResult, campaignData, roundReview]);

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

  const selectedQuestion = flatQuestionList[selectedQuestionIndex] || flatQuestionList[0] || roundReview?.questions?.[0] || null;
  const isTechnicalV2Review = roundReview?.runtimeVersion === 'V2';

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

              {formatDate(campaignData?.startedAt || campaignData?.createdAt, (i18n.resolvedLanguage || i18n.language)?.startsWith('en') ? 'en-US' : 'vi-VN') && (
                <div className="flex items-center gap-2 text-xs text-text-secondary flex-wrap">
                  <Clock size={13} className="text-text-muted" />
                  <span>{formatDate(campaignData?.startedAt || campaignData?.createdAt, (i18n.resolvedLanguage || i18n.language)?.startsWith('en') ? 'en-US' : 'vi-VN')}</span>
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

        {/* Dashboard 4 Chỉ số Đánh giá Cuối cùng (Competency Benchmark Metrics) - Only shown in Real / Mock Interview mode */}
        {mode === 'Mock' && competencyMetrics && (
          <section className="flex flex-col gap-3">
            <h3 className="text-xs font-bold uppercase tracking-wider text-text-muted flex items-center gap-1.5">
              <Target size={15} /> {copy.history.benchmarksTitle}
            </h3>
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
              {competencyMetrics.map((metric) => (
                <div key={metric.title} className={`p-4 rounded-xl border bg-gradient-to-br ${metric.color} flex flex-col justify-between gap-3 shadow-xs`}>
                  <div className="flex items-start justify-between gap-2">
                    <div className="flex flex-col">
                      <span className="text-xs font-extrabold tracking-tight">{metric.title}</span>
                      <span className="text-[11px] font-medium opacity-80">{metric.labelVi}</span>
                    </div>
                    <span className={`px-2.5 py-1 rounded-lg text-sm font-black shadow-2xs ${metric.badgeColor}`}>
                      {metric.score}/10
                    </span>
                  </div>
                  <div className="text-[10px] opacity-75 font-mono border-t border-current/15 pt-2 leading-relaxed" title={metric.formula}>
                    {metric.formula}
                  </div>
                </div>
              ))}
            </div>
          </section>
        )}

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
                      {copy.review.noCodingQuestions}
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
                        ({selectedQuestionIndex + 1}/{flatQuestionList.length})
                      </span>
                    </div>

                    <div className="flex flex-col gap-2">
                      {roundReview.questions.map((q, mainIdx) => {
                        const mainFlatIndex = flatQuestionList.findIndex((item) => item.key === `main-${q.id || mainIdx}`);
                        const isMainSelected = selectedQuestionIndex === mainFlatIndex;
                        const subQuestions = q.subQuestions || [];
                        const hasSub = subQuestions.length > 0;

                        const selectedItem = flatQuestionList[selectedQuestionIndex];
                        const isParentOfSelected = selectedItem?.isSubQuestion && selectedItem?.mainIndex === mainIdx;
                        const isExpanded = expandedQuestions[mainIdx] || isParentOfSelected;

                        return (
                          <div key={q.id || mainIdx} className="flex flex-col gap-1.5">
                            {/* Main Question Card Q1, Q2... */}
                            <button
                              type="button"
                              onClick={() => {
                                setSelectedQuestionIndex(mainFlatIndex !== -1 ? mainFlatIndex : 0);
                                if (hasSub) {
                                  setExpandedQuestions((prev) => ({ ...prev, [mainIdx]: !prev[mainIdx] }));
                                }
                              }}
                              className={`
                                p-3.5 rounded-xl border text-left transition-all cursor-pointer flex flex-col gap-1.5 relative overflow-hidden group
                                ${isMainSelected
                                  ? 'border-primary bg-[#F0F7FF] border-l-4 border-l-primary shadow-xs'
                                  : 'border-border bg-surface hover:bg-surface-2'
                                }
                              `}
                            >
                              <div className="flex items-center justify-between text-xs">
                                <div className="flex items-center gap-1.5">
                                  <span className="font-extrabold text-primary">Q{q.order || mainIdx + 1}</span>
                                  {hasSub && (
                                    <span className="px-1.5 py-0.5 bg-primary/10 text-primary text-[10px] font-bold rounded-full flex items-center gap-1">
                                      +{subQuestions.length}
                                      <ChevronDown size={12} className={`transition-transform duration-200 ${isExpanded ? 'rotate-180' : ''}`} />
                                    </span>
                                  )}
                                </div>
                                {formatScore(q.score, q.maxScore) && (
                                  <span className="font-bold text-text-primary">{formatScore(q.score, q.maxScore)}</span>
                                )}
                              </div>
                              <p className="text-xs text-text-secondary line-clamp-2 font-medium leading-snug">
                                {q.question || copy.review.missingQuestion}
                              </p>
                            </button>

                            {/* Sub-questions Dropdown List (Collapsed by default, expands on click) */}
                            {hasSub && isExpanded && (
                              <div className="pl-3 flex flex-col gap-1 border-l-2 border-primary/20 ml-2 animate-fadeIn">
                                {subQuestions.map((subQ, subIdx) => {
                                  const subFlatIndex = flatQuestionList.findIndex((item) => item.key === `sub-${subQ.id || subIdx}`);
                                  const isSubSelected = selectedQuestionIndex === subFlatIndex;

                                  return (
                                    <button
                                      key={subQ.id || subIdx}
                                      type="button"
                                      onClick={(e) => {
                                        e.stopPropagation();
                                        setSelectedQuestionIndex(subFlatIndex !== -1 ? subFlatIndex : mainFlatIndex);
                                      }}
                                      className={`
                                        p-2.5 rounded-lg border text-left transition-all cursor-pointer flex flex-col gap-1 text-xs
                                        ${isSubSelected
                                          ? 'border-primary/80 bg-primary-xlight/40 font-semibold shadow-2xs'
                                          : 'border-border/60 bg-surface-2 hover:bg-surface-muted'
                                        }
                                      `}
                                    >
                                      <div className="flex items-center justify-between">
                                        <span className={`px-1.5 py-0.5 text-[9px] font-bold rounded uppercase ${
                                          subQ.questionType === 'Clarification'
                                            ? 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300'
                                            : 'bg-blue-100 text-blue-800 dark:bg-blue-900/40 dark:text-blue-300'
                                        }`}>
                                          {subQ.questionType === 'Clarification' ? copy.review.clarificationBadge : copy.review.followUpBadge}
                                        </span>
                                        <span className="font-extrabold text-primary">{formatScore(subQ.score, subQ.maxScore)}</span>
                                      </div>
                                      <p className="text-[11px] text-text-secondary line-clamp-1 font-medium">
                                        {subQ.question}
                                      </p>
                                    </button>
                                  );
                                })}
                              </div>
                            )}
                          </div>
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
                        <span className="text-[9px] uppercase font-extrabold text-primary-dark tracking-wider">{copy.review.scoreLabel}</span>
                        <span className="text-lg font-extrabold text-primary">
                          {formatScore(selectedQuestion.score, selectedQuestion.maxScore)}
                        </span>
                        {selectedQuestion.questionId && ['Technical', 'Behavior', 'Behavioral'].includes(activeSessionObj?.interviewRoundType || activeSessionObj?.roundType) && <Button variant="outline" size="sm" icon={RefreshCw} onClick={() => openSingleQuestionInterview(selectedQuestion, activeSessionObj.interviewRoundType || activeSessionObj.roundType, activeRoundSessionId)}>{copy.review.retryQuestion}</Button>}
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
                          <CollapsibleCriterionCard
                            key={dim.rubricCode || idx}
                            dim={dim}
                            isTechnicalV2Review={isTechnicalV2Review}
                            technicalCriterionLabel={technicalCriterionLabel}
                            copy={copy}
                            t={t}
                          />
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
                      {selectedQuestionIndex + 1} / {flatQuestionList.length}
                    </span>
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={selectedQuestionIndex === flatQuestionList.length - 1}
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
