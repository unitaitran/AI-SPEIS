import React from 'react';
import { getTechnicalV2CriterionDefinitions, orderTechnicalV2Dimensions } from '../../features/technicalInterview/technicalV2InterviewResult';

const dimensionLabel = (code, t) => t(`rubric.${code}`, { defaultValue: code });
const formatWeight = (weight) => `${new Intl.NumberFormat(undefined, { style: 'percent', maximumFractionDigits: 0 }).format(Number(weight) || 0)}`;
const performanceBandLabel = (value, t) => value
  ? t(`result.performanceBand.${value}`, { defaultValue: value })
  : t('result.resultReady');
const statusLabel = (namespace, value, fallback, t) => value
  ? t(`result.${namespace}.${String(value).toUpperCase()}`, { defaultValue: value })
  : t(fallback);

function TechnicalV2ResultView({ result, t }) {
  const questions = Array.isArray(result?.mainQuestions) ? result.mainQuestions : [];
  const summary = result?.summary || {};
  const criteria = getTechnicalV2CriterionDefinitions(questions);

  return (
    <div className="technical-v2-result-stack">
      <section className="technical-result-summary technical-card" aria-labelledby="technical-v2-result-title">
        <div className="technical-score-card">
          <span className="technical-score-card__label" id="technical-v2-result-title">{t('result.overallScore')}</span>
          <div className="technical-score-card__value">{Number(result?.overallScore || 0).toFixed(2)}<small> / 10</small></div>
          <div className="technical-score-bar" role="progressbar" aria-valuemin={0} aria-valuemax={10} aria-valuenow={Number(result?.overallScore || 0)}>
            <div className="technical-score-bar__fill" style={{ width: `${Math.min(100, Math.max(0, Number(result?.overallScore || 0) * 10))}%` }} />
          </div>
        </div>
        <div className="technical-summary-details">
          <div className="technical-summary-badges"><span className="technical-result-badge">{performanceBandLabel(result?.performanceBand, t)}</span><span className="technical-result-badge">{statusLabel('feedbackStatus', result?.finalFeedbackStatus, 'result.feedbackPending', t)}</span></div>
          <p className="technical-summary-feedback">{summary.overallTechnicalAssessment || summary.executiveSummary || t('result.noSummary')}</p>
          <p className="technical-summary-level"><strong>{t('result.levelAssessment')}:</strong> {summary.levelAssessment || result?.performanceBand || t('result.resultReady')}</p>
          <div className="technical-feedback-columns">
            <FeedbackList title={t('result.strengths')} items={summary.strengths} />
            <FeedbackList title={t('result.gaps')} items={summary.knowledgeGaps} />
          </div>
        </div>
      </section>

      <section className="technical-result-section technical-card" aria-labelledby="technical-v2-rubric-title">
        <div className="technical-section__header"><div><p className="technical-section__eyebrow">{t('result.rubricEyebrow')}</p><h2 id="technical-v2-rubric-title">{t('result.rubricDimensions')}</h2></div></div>
        <div className="technical-rubric-grid">
          {criteria.length ? criteria.map((criterion) => (
            <article className="technical-rubric-dimension-card" key={criterion.rubricCode}>
              <div className="technical-rubric-dimension-card__header"><h3>{dimensionLabel(criterion.rubricCode, t)}</h3></div>
              <p>{t('result.weight', { weight: formatWeight(criterion.weight) })}</p>
            </article>
          )) : <p className="technical-empty-copy">{t('result.noDimensions')}</p>}
        </div>
      </section>

      <section className="technical-result-section technical-card" aria-labelledby="technical-v2-question-title">
        <div className="technical-section__header"><div><p className="technical-section__eyebrow">{t('result.questionsEyebrow')}</p><h2 id="technical-v2-question-title">{t('result.questionBreakdown')}</h2></div></div>
        <div className="technical-question-list">
          {questions.length ? questions.map((question, index) => (
            <details className="technical-question-result" key={question.sessionQuestionId || index} open={index === 0}>
              <summary><div><p className="technical-section__eyebrow">{t('result.mainQuestion', { index: question.questionOrder || index + 1 })}</p><h3>{question.question || t('result.questionUnavailable')}</h3></div><strong>{Number(question.score || 0).toFixed(2)}/10</strong></summary>
              <div className="technical-question-result__body">
                <p className="technical-question-answer">{question.answerTranscript || t('result.answerUnavailable')}</p>
                <p className="technical-result-badge">{statusLabel('evaluationStatus', question.evaluationStatus, 'result.evaluationPending', t)}</p>
                <QuestionCriteria dimensions={question.dimensions} t={t} />
                <div className="technical-feedback-columns"><FeedbackList title={t('result.strengths')} items={question.strengths} /><FeedbackList title={t('result.gaps')} items={question.missingPoints} /></div>

                {Array.isArray(question.subQuestions) && question.subQuestions.length > 0 ? (
                  <div className="technical-subquestions-wrapper mt-4 border-t pt-3">
                    <h4 className="technical-subquestions-title font-semibold text-sm mb-2 text-slate-700 dark:text-slate-200">
                      {t('result.subQuestionsTitle', { defaultValue: 'Câu hỏi phụ & Làm rõ' })} ({question.subQuestions.length})
                    </h4>
                    <div className="technical-subquestions-list space-y-2">
                      {question.subQuestions.map((subQ, subIndex) => (
                        <details key={subQ.sessionQuestionId || subIndex} className="technical-subquestion-card border rounded-lg p-3 bg-slate-50 dark:bg-slate-800/40">
                          <summary className="font-medium cursor-pointer flex justify-between items-center text-sm">
                            <div className="flex items-center gap-2">
                              <span className={`px-2 py-0.5 text-xs font-semibold rounded ${subQ.questionType === 'Clarification' ? 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300' : 'bg-blue-100 text-blue-800 dark:bg-blue-900/40 dark:text-blue-300'}`}>
                                {subQ.questionType === 'Clarification' ? t('result.clarificationTag', { defaultValue: 'Làm rõ' }) : t('result.followUpTag', { defaultValue: 'Hỏi phụ' })}
                              </span>
                              <span>{subQ.question}</span>
                            </div>
                            <strong>{Number(subQ.score || 0).toFixed(1)}/10</strong>
                          </summary>
                          <div className="technical-subquestion-body mt-3 pt-2 border-t text-sm">
                            <p className="technical-question-answer mb-3"><strong>{t('result.answerTranscriptLabel', { defaultValue: 'Câu trả lời:' })}</strong> {subQ.answerTranscript || t('result.answerUnavailable')}</p>
                            <QuestionCriteria dimensions={subQ.dimensions} t={t} />
                            <div className="technical-feedback-columns">
                              <FeedbackList title={t('result.strengths')} items={subQ.strengths} />
                              <FeedbackList title={t('result.gaps')} items={subQ.missingPoints} />
                            </div>
                          </div>
                        </details>
                      ))}
                    </div>
                  </div>
                ) : null}
              </div>
            </details>
          )) : <p className="technical-empty-copy">{t('result.noQuestions')}</p>}
        </div>
      </section>

      <section className="technical-result-section technical-card" aria-labelledby="technical-v2-final-feedback-title">
        <div className="technical-section__header"><div><p className="technical-section__eyebrow">{t('result.finalFeedbackEyebrow')}</p><h2 id="technical-v2-final-feedback-title">{t('result.finalFeedback')}</h2></div></div>
        <p className="technical-summary-feedback">{summary.executiveSummary || summary.overallTechnicalAssessment || t('result.feedbackUnavailable')}</p>
      </section>

      <section className="technical-result-section technical-card" aria-labelledby="technical-v2-recommendations-title">
        <div className="technical-section__header"><div><p className="technical-section__eyebrow">{t('result.nextStepsEyebrow')}</p><h2 id="technical-v2-recommendations-title">{t('result.recommendations')}</h2></div></div>
        <FeedbackList title={t('result.recommendations')} items={summary.recommendationsForImprovement} />
      </section>
    </div>
  );
}

function QuestionCriteria({ dimensions, t }) {
  const criteria = orderTechnicalV2Dimensions(dimensions);
  if (!criteria.length) return null;

  return (
    <section className="technical-question-criteria" aria-label={t('result.questionCriteria')}>
      <h4>{t('result.questionCriteria')}</h4>
      <div>
        {criteria.map((criterion) => (
          <article className="technical-question-criterion" key={criterion.rubricCode}>
            <div className="technical-question-criterion__header">
              <h5>{dimensionLabel(criterion.rubricCode, t)}</h5>
              <strong>{Number(criterion.score || 0).toFixed(2)}/10</strong>
            </div>
            <p>{t('result.weight', { weight: formatWeight(criterion.weight) })}</p>
            <FeedbackList title={t('result.evidence')} items={criterion.evidence} />
            <FeedbackList title={t('result.strengths')} items={criterion.strengths} />
          </article>
        ))}
      </div>
    </section>
  );
}

function FeedbackList({ title, items }) {
  if (!Array.isArray(items) || items.length === 0) return null;
  return <div className="technical-v2-feedback-list"><h4>{title}</h4><ul>{items.map((item, index) => <li key={`${item}-${index}`}>{item}</li>)}</ul></div>;
}

export default TechnicalV2ResultView;
