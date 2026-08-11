import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  AlertCircle,
  ArrowLeft,
  ChevronLeft,
  ChevronRight,
  ClipboardCheck,
  FileText,
  Lightbulb,
  LoaderCircle,
  MessageSquareText,
  RefreshCw,
  Target,
} from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';
import interviewSessionService from '../../services/InterviewSessionService';
import technicalInterviewApi from '../../services/technicalInterviewApi';
import technicalV2InterviewApi from '../../services/technicalV2InterviewApi';
import behavioralInterviewApi from '../../services/behavioralInterviewApi';
import { normalizeTechnicalInterviewResult } from '../../features/technicalInterview/technicalInterviewResult';
import { normalizeTechnicalV2Review } from '../../features/technicalInterview/technicalV2InterviewResult';
import { getInterviewHistoryCopy } from '../../features/interviewHistory/interviewHistoryCopy';
import '../../styles/user/InterviewHistory.css';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES, getCampaignResultPath } from '../../routes/routePaths';



const roundTitle = (round, copy) => ({ Technical: copy.review.technical, Behavior: copy.review.behavioral }[round] || round || copy.review.fallbackRound);

const formatScore = (score, maxScore) => {
  const numericScore = Number(score);
  if (!Number.isFinite(numericScore)) return null;
  const numericMaxScore = Number(maxScore);
  return Number.isFinite(numericMaxScore) && numericMaxScore > 0
    ? `${numericScore.toFixed(2)}/${numericMaxScore}`
    : numericScore.toFixed(2);
};

const formatWeight = (weight) => new Intl.NumberFormat(undefined, { style: 'percent', maximumFractionDigits: 0 }).format(Number(weight) || 0);
const technicalCriterionLabel = (code, t) => t(`technicalRoom.rubric.${String(code || '').toUpperCase()}`, { defaultValue: code || '' });

const normalizeBehaviorReview = (result, state) => {
  const answers = (state?.transcript || []).filter((entry) => String(entry.role).toLowerCase() === 'candidate');
  const roundFeedback = result?.summary?.overallBehavioralAssessment || result?.summary?.executiveSummary || '';
  return {
    overallScore: result?.overallScore,
    maxScore: result?.maxScore,
    finalFeedbackStatus: result?.finalFeedbackStatus,
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
        missingPoints: subQ.missingPoints || [],
        transcript: subQ.answerTranscript || answers.find((ans) => ans.sessionQuestionId === subQ.sessionQuestionId)?.content || '',
        answerTranscript: subQ.answerTranscript || answers.find((ans) => ans.sessionQuestionId === subQ.sessionQuestionId)?.content || '',
      }));

      const mainTranscript = question.answerTranscript
        || answers.find((answer) => answer.sessionQuestionId === question.sessionQuestionId)?.content
        || '';

      return {
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
        transcript: mainTranscript,
        feedbackSummary: roundFeedback,
        suggestions: result?.summary?.recommendationsForImprovement || [],
        subQuestions,
        adaptiveHistory: subQuestions,
      };
    }),
  };
};

const normalizeTechnicalReview = (result) => {
  const normalized = normalizeTechnicalInterviewResult(result);
  return {
    overallScore: normalized?.technicalScore,
    maxScore: normalized?.maxScore,
    finalFeedbackStatus: normalized?.finalFeedbackStatus,
    questions: (normalized?.questionResults || []).map((question) => ({
      id: question.attemptId || question.mainQuestionIndex,
      order: question.mainQuestionIndex,
      question: question.content || question.question,
      questionType: question.questionType,
      skill: question.skill || question.targetSkill,
      score: question.score,
      maxScore: question.maxScore,
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

function ListSection({ icon: Icon, title, items, tone = 'neutral' }) {
  if (!items?.length) return null;
  return <section className={`interview-review-list interview-review-list--${tone}`}><h3><Icon size={18} />{title}</h3><ul>{items.map((item, index) => <li key={`${item}-${index}`}>{item}</li>)}</ul></section>;
}

function InterviewReviewPage({ sessionId }) {
  const { i18n, t } = useTranslation('interview');
  const copy = getInterviewHistoryCopy(i18n.resolvedLanguage || i18n.language);
  const [review, setReview] = useState(null);
  const [session, setSession] = useState(null);
  const [error, setError] = useState('');
  const [isLoading, setIsLoading] = useState(true);
  const [selectedIndex, setSelectedIndex] = useState(0);

  const loadReview = useCallback(async () => {
    if (!sessionId) {
      setError(copy.review.missingId);
      setIsLoading(false);
      return;
    }
    setIsLoading(true);
    setError('');
    try {
      const sessionData = await interviewSessionService.getSession(sessionId);
      if (sessionData?.interviewCampaignId) {
        navigate(getCampaignResultPath(sessionData.interviewCampaignId), { replace: true });
        return;
      }
      const round = sessionData.interviewRoundType;
      let nextReview;
      if (round === 'Technical') {
        try {
          const result = await technicalV2InterviewApi.getResult(sessionId);
          nextReview = normalizeTechnicalV2Review(result);
        } catch (v2Error) {
          if (v2Error?.code !== 'LEGACY_SESSION') throw v2Error;
          const [result, state] = await Promise.all([
            technicalInterviewApi.getResult(sessionId),
            technicalInterviewApi.getSession(sessionId),
          ]);
          nextReview = normalizeTechnicalReview(result, state);
        }
      } else if (round === 'Behavior' || round === 'Behavioral') {
        const [result, state] = await Promise.all([
          behavioralInterviewApi.getResult(sessionId),
          behavioralInterviewApi.getState(sessionId),
        ]);
        nextReview = normalizeBehaviorReview(result, state);
      } else {
        throw new Error('ANSWER_REVIEW_NOT_AVAILABLE');
      }
      setSession(sessionData);
      setReview(nextReview);
      setSelectedIndex(0);
    } catch (loadError) {
      if (loadError?.status === 401 || loadError?.status === 403) setError(copy.review.forbidden);
      else if (loadError?.status === 404) setError(copy.review.notFound);
      else if (loadError?.code === 'REQUEST_TIMEOUT') setError(copy.review.timeout);
      else if (loadError?.message === 'ANSWER_REVIEW_NOT_AVAILABLE') setError(copy.review.unsupported);
      else setError(copy.review.loadError);
    } finally {
      setIsLoading(false);
    }
  }, [copy.review, sessionId]);

  useEffect(() => { loadReview(); }, [loadReview]);

  const selected = review?.questions?.[selectedIndex] || null;
  const isTechnicalV2Review = review?.runtimeVersion === 'V2';
  const reviewTitle = useMemo(() => `${roundTitle(session?.interviewRoundType, copy)} · #${sessionId}`, [copy, session?.interviewRoundType, sessionId]);

  let body;
  if (isLoading) {
    body = <div className="interview-review-state" role="status"><LoaderCircle className="interview-history-spin" size={34} /><p>{copy.review.loading}</p></div>;
  } else if (error) {
    body = <div className="interview-review-state" role="alert"><AlertCircle size={34} /><h2>{copy.review.loadTitle}</h2><p>{error}</p><div><button type="button" className="interview-history-button" onClick={loadReview}><RefreshCw size={17} /> {copy.review.retry}</button><button type="button" className="interview-history-button interview-history-button--secondary" onClick={() => navigate(USER_ROUTES.INTERVIEW_HISTORY)}><ArrowLeft size={17} /> {copy.review.backHistory}</button></div></div>;
  } else if (!review?.questions?.length) {
    body = <div className="interview-review-state"><FileText size={36} /><h2>{copy.review.emptyTitle}</h2><p>{copy.review.emptyDescription}</p><button type="button" className="interview-history-button interview-history-button--secondary" onClick={() => navigate(USER_ROUTES.INTERVIEW_HISTORY)}><ArrowLeft size={17} /> {copy.review.backHistory}</button></div>;
  } else {
    body = (
      <div className="interview-review-layout">
        <aside className="interview-review-questions" aria-label={copy.review.questionList}>
          <header><span>{copy.review.questionList}</span><b>{review.questions.length}</b></header>
          <div>{review.questions.map((question, index) => <button key={question.id} type="button" onClick={() => setSelectedIndex(index)} className={index === selectedIndex ? 'is-selected' : ''} aria-current={index === selectedIndex ? 'true' : undefined}><span>Q{question.order || index + 1}</span><strong>{question.question || copy.review.missingQuestion}</strong>{formatScore(question.score, question.maxScore) ? <em>{formatScore(question.score, question.maxScore)}</em> : <em>{copy.review.waitingEvaluation}</em>}</button>)}</div>
        </aside>
        <article className="interview-review-detail">
          <header className="interview-review-detail__header"><div><span>{selected.questionType === 'MAIN' ? copy.review.mainQuestion : selected.questionType || copy.review.question}</span><h2>{selected.question || copy.review.missingQuestion}</h2>{selected.skill ? <p>{copy.review.skill.replace('{{skill}}', selected.skill)}</p> : null}</div>{formatScore(selected.score, selected.maxScore) ? <div className="interview-review-score"><b>{formatScore(selected.score, selected.maxScore)}</b></div> : null}</header>
          <section className="interview-review-transcript"><h3><MessageSquareText size={18} /> {copy.review.transcript}</h3>{selected.transcript ? <p>{selected.transcript}</p> : <p className="interview-review-empty-copy">{copy.review.missingTranscript}</p>}</section>
          {selected.feedbackSummary ? <section className="interview-review-feedback"><h3><ClipboardCheck size={18} /> {copy.review.aiFeedback}</h3><p>{selected.feedbackSummary}</p></section> : null}
          {selected.dimensions?.length ? <section className="interview-review-rubric"><h3><Target size={18} /> {copy.review.rubric}</h3><div>{selected.dimensions.map((dimension) => <article key={dimension.rubricCode || dimension.name}><div><strong>{isTechnicalV2Review ? technicalCriterionLabel(dimension.rubricCode, t) : (dimension.name || dimension.rubricCode)}</strong>{!isTechnicalV2Review && dimension.level ? <span>{dimension.level}</span> : null}</div>{formatScore(dimension.score, dimension.maxScore) ? <b>{formatScore(dimension.score, dimension.maxScore)}</b> : null}{isTechnicalV2Review ? <small>{t('technicalRoom.result.weight', { weight: formatWeight(dimension.weight) })}</small> : null}{dimension.evidence?.length ? <p><strong>{isTechnicalV2Review ? t('technicalRoom.result.evidence') : ''}</strong>{isTechnicalV2Review ? ' ' : ''}{dimension.evidence.join(' ')}</p> : null}{isTechnicalV2Review && dimension.strengths?.length ? <p><strong>{t('technicalRoom.result.strengths')}</strong> {dimension.strengths.join(' ')}</p> : null}{isTechnicalV2Review && (dimension.gaps?.length || dimension.missingEvidence?.length) ? <p><strong>{t('technicalRoom.result.gaps')}</strong> {(dimension.gaps?.length ? dimension.gaps : dimension.missingEvidence).join(' ')}</p> : null}</article>)}</div></section> : null}
          <div className="interview-review-feedback-grid"><ListSection icon={ClipboardCheck} title={copy.review.strengths} items={selected.strengths} tone="positive" /><ListSection icon={Target} title={copy.review.improvements} items={selected.missingPoints} tone="focus" /><ListSection icon={Lightbulb} title={copy.review.practiceTips} items={selected.suggestions} tone="next" /></div>
          {selected.adaptiveHistory?.length ? <section className="interview-review-followups"><h3>{copy.review.followUps}</h3>{selected.adaptiveHistory.map((item, index) => <article key={item.attemptId || index}><strong>{item.questionType || 'FOLLOW_UP'}</strong><p>{item.question}</p>{item.answerTranscript ? <span>{item.answerTranscript}</span> : <span className="interview-review-empty-copy">{copy.review.missingFollowUpTranscript}</span>}</article>)}</section> : null}
          <footer className="interview-review-navigation"><button type="button" className="interview-history-button interview-history-button--secondary" disabled={selectedIndex === 0} onClick={() => setSelectedIndex((value) => value - 1)}><ChevronLeft size={17} /> {copy.review.previous}</button><span>{selectedIndex + 1}/{review.questions.length}</span><button type="button" className="interview-history-button" disabled={selectedIndex === review.questions.length - 1} onClick={() => setSelectedIndex((value) => value + 1)}>{copy.review.next} <ChevronRight size={17} /></button></footer>
        </article>
      </div>
    );
  }

  return <UserLayout><section className="interview-review-page"><header className="interview-review-page__header"><div><button type="button" className="interview-review-back" onClick={() => navigate(USER_ROUTES.INTERVIEW_HISTORY)}><ArrowLeft size={17} /> {copy.review.back}</button><p>{copy.review.eyebrow}</p><h1>{copy.review.title}</h1><span>{reviewTitle}</span></div>{formatScore(review?.overallScore, review?.maxScore) ? <div className="interview-review-overall"><span>{copy.history.totalScore}</span><strong>{formatScore(review.overallScore, review.maxScore)}</strong></div> : null}</header>{body}</section></UserLayout>;
}

export default InterviewReviewPage;
