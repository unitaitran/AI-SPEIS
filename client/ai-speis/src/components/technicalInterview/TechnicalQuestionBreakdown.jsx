import React from 'react';
import { ChevronDown } from 'lucide-react';
import { groupTechnicalQuestionResults } from '../../features/technicalInterview/technicalInterviewResult';
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
          {groupedQuestions.map((question, index) => (
            <details
              className="technical-question-result"
              key={question.attemptId || `${question.mainQuestionIndex}-${index}`}
              open={index === 0}
            >
              <summary>
                <div>
                  <p className="technical-section__eyebrow">
                    {question.isOrphanSubQuestion
                      ? t('result.additionalQuestion')
                      : t('result.mainQuestion', { index: question.mainQuestionIndex ?? index + 1 })}
                  </p>
                  <h3>{getQuestionContent(question) || t('result.questionUnavailable')}</h3>
                </div>
                <ChevronDown size={20} aria-hidden="true" />
              </summary>
              <div className="technical-question-result__body">
                <div className="technical-question-answer">
                  <h4>{t('result.candidateAnswer')}</h4>
                  <p>{getAnswerContent(question) || t('result.answerUnavailable')}</p>
                </div>
                {question.subQuestionResults?.length > 0 && (
                  <div className="technical-subquestions">
                    {question.subQuestionResults.map((subQuestion, subIndex) => (
                      <article className="technical-subquestion" key={subQuestion.attemptId || subIndex}>
                        <TechnicalQuestionTypeBadge type={subQuestion.questionType || subQuestion.type} t={t} />
                        <p><strong>{getQuestionContent(subQuestion)}</strong></p>
                        <p>{getAnswerContent(subQuestion) || t('result.answerUnavailable')}</p>
                      </article>
                    ))}
                  </div>
                )}
                {Array.isArray(question.rubricBreakdown || question.dimensionResults)
                  && (question.rubricBreakdown || question.dimensionResults).length > 0 && (
                    <div className="technical-question-rubric">
                      <h4>{t('result.questionRubric')}</h4>
                      <ul>
                        {(question.rubricBreakdown || question.dimensionResults).map((item, rubricIndex) => (
                          <li key={item.rubricCode || item.name || rubricIndex}>
                            <span>{item.name || item.rubricCode}</span>
                            <strong>
                              {item.score ?? t('result.notAvailable')}
                              {item.maxScore != null && ` / ${item.maxScore}`}
                            </strong>
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}
                <div className="technical-feedback-columns">
                  <TechnicalResultFeedbackList title={t('result.strengths')} items={question.strengths} />
                  <TechnicalResultFeedbackList title={t('result.missingPoints')} items={question.missingPoints || question.missingEvidence} />
                  <TechnicalResultFeedbackList title={t('result.incorrectClaims')} items={question.incorrectClaims} />
                  <TechnicalResultFeedbackList title={t('result.suggestions')} items={question.suggestions || question.improvementSuggestions} />
                </div>
              </div>
            </details>
          ))}
        </div>
      ) : (
        <p className="technical-empty-copy">{t('result.noQuestions')}</p>
      )}
    </section>
  );
}

export default TechnicalQuestionBreakdown;
