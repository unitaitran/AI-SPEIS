import React from 'react';
import { ChevronDown } from 'lucide-react';
import {
  formatTechnicalWeight,
  groupTechnicalQuestionResults,
} from '../../features/technicalInterview/technicalInterviewResult';
import { TechnicalQuestionType } from '../../features/technicalInterview/technicalInterview.types';
import TechnicalQuestionTypeBadge from './TechnicalQuestionTypeBadge';
import TechnicalResultFeedbackList from './TechnicalResultFeedbackList';

const getQuestionContent = (question) => question.questionContent || question.content || question.question;
const getAnswerContent = (question) => question.answerTranscript || question.transcript || question.answer;

function TechnicalQuestionBreakdown({ questions, t }) {
  const groupedQuestions = groupTechnicalQuestionResults(questions);
  return (
    <section className="technical-result-section technical-card" aria-labelledby="technical-questions-title">
      <div className="technical-section__header">
        <div>
          <p className="technical-section__eyebrow">{t('result.questionsEyebrow')}</p>
          <h2 id="technical-questions-title">{t('result.questionBreakdown')}</h2>
        </div>
      </div>
      {groupedQuestions.length > 0 ? (
        <div className="technical-question-list">
          {groupedQuestions.map((question, index) => {
            let followUpIndex = 0;
            return (
              <details
                className="technical-question-result"
                key={question.attemptId || `${question.mainQuestionIndex}-${index}`}
                open={index === 0}
              >
              <summary>
                <div>
                  <p className="technical-section__eyebrow">
                    {t('result.mainQuestion', { index: question.mainQuestionIndex ?? index + 1 })}
                  </p>
                  <h3>{getQuestionContent(question) || t('result.questionUnavailable')}</h3>
                </div>
                <ChevronDown size={20} aria-hidden="true" />
              </summary>
              <div className="technical-question-result__body">
                <dl className="technical-main-score-grid">
                  <div>
                    <dt>{t('result.initialMainScore')}</dt>
                    <dd>{question.initialMainScore ?? t('result.notAvailable')} / {question.maxScore}</dd>
                  </div>
                  <div>
                    <dt>{t('result.finalMainScore')}</dt>
                    <dd>{question.finalMainScore ?? question.score ?? t('result.notAvailable')} / {question.maxScore}</dd>
                  </div>
                  <div>
                    <dt>{t('result.followUpBonus')}</dt>
                    <dd>+{question.cumulativeFollowUpBonus ?? 0}</dd>
                  </div>
                </dl>
                <div className="technical-question-answer">
                  <h4>{t('result.candidateAnswer')}</h4>
                  <p>{getAnswerContent(question) || t('result.answerUnavailable')}</p>
                </div>
                {question.subQuestionResults?.length > 0 && (
                  <div className="technical-subquestions">
                    {question.subQuestionResults.map((subQuestion, subIndex) => {
                      const type = subQuestion.questionType || subQuestion.type;
                      if (type === TechnicalQuestionType.FOLLOW_UP) followUpIndex += 1;
                      return (
                        <article className="technical-subquestion" key={subQuestion.attemptId || subIndex}>
                          <div className="technical-subquestion__header">
                            <TechnicalQuestionTypeBadge type={type} t={t} />
                            <strong>
                              {type === TechnicalQuestionType.FOLLOW_UP
                                ? t('result.followUpResult', { index: followUpIndex })
                                : t('result.clarificationResult')}
                            </strong>
                          </div>
                          <p><strong>{getQuestionContent(subQuestion)}</strong></p>
                          <p>{getAnswerContent(subQuestion) || t('result.answerUnavailable')}</p>
                          {(subQuestion.rawScore != null || subQuestion.followUpBonus != null) && (
                            <div className="technical-subquestion__metrics">
                              {subQuestion.rawScore != null && (
                                <span>{t('result.subQuestionScore', {
                                  score: subQuestion.rawScore,
                                  maxScore: subQuestion.maxScore,
                                })}</span>
                              )}
                              {subQuestion.followUpBonus != null && (
                                <span>{t('result.appliedBonus', { bonus: subQuestion.followUpBonus })}</span>
                              )}
                            </div>
                          )}
                        </article>
                      );
                    })}
                  </div>
                )}
                {Array.isArray(question.rubricBreakdown || question.dimensionResults)
                  && (question.rubricBreakdown || question.dimensionResults).length > 0 && (
                    <div className="technical-question-rubric">
                      <h4>{t('result.questionRubric')}</h4>
                      <ul>
                        {(question.rubricBreakdown || question.dimensionResults).map((item, rubricIndex) => (
                          <li key={item.rubricCode || item.name || rubricIndex}>
                            <span>
                              {item.name || item.rubricCode}
                              {item.weight != null && (
                                <small>{t('result.weight', { weight: formatTechnicalWeight(item.weight) })}</small>
                              )}
                            </span>
                            <strong>
                              {item.score ?? t('result.notAvailable')}
                              {item.maxScore != null && ` / ${item.maxScore}`}
                              {item.level && <small>{item.level}</small>}
                            </strong>
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}
                <div className="technical-feedback-columns">
                  <TechnicalResultFeedbackList title={t('result.strengths')} items={question.strengths} />
                  <TechnicalResultFeedbackList title={t('result.missingPoints')} items={question.missingPoints || question.missingEvidence} />
                  <TechnicalResultFeedbackList title={t('result.suggestions')} items={question.suggestions || question.improvementSuggestions} />
                </div>
                {question.feedbackSummary && (
                  <p className="technical-question-feedback-summary">{question.feedbackSummary}</p>
                )}
              </div>
              </details>
            );
          })}
        </div>
      ) : (
        <p className="technical-empty-copy">{t('result.noQuestions')}</p>
      )}
    </section>
  );
}

export default TechnicalQuestionBreakdown;
