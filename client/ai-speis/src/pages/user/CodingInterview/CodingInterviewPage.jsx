import React, { useState, useEffect, useRef } from 'react';
import Editor from '@monaco-editor/react';
import { codingService } from '../../../services/codingService';
import interviewSessionService from '../../../services/InterviewSessionService';
import { navigate } from '../../../routes/navigation';
import { getCampaignResultPath, getInterviewRoomPath } from '../../../routes/routePaths';
import { getActiveInterviewContext, getNextOpenSession, saveActiveInterviewContext } from '../../../utils/interviewContext';
import notify from '../../../utils/notification';
import { useTranslation } from 'react-i18next';
import '../../../styles/user/CodingInterviewPage.css';

const CodingInterviewPage = ({ sessionId }) => {
  const { t } = useTranslation('interview');
  const [questions, setQuestions] = useState([]);
  const [currentQuestionIndex, setCurrentQuestionIndex] = useState(0);
  const [languages, setLanguages] = useState([]);
  const [selectedLanguage, setSelectedLanguage] = useState(null);
  const [code, setCode] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submissionResult, setSubmissionResult] = useState(null);
  const [submittedQuestionIds, setSubmittedQuestionIds] = useState(() => new Set());
  const [isCompleting, setIsCompleting] = useState(false);
  
  const editorRef = useRef(null);

  useEffect(() => {
    const fetchInitialData = async () => {
      try {
        const langRes = await codingService.getLanguages();
        if (langRes && langRes.length > 0) {
          setLanguages(langRes);
          setSelectedLanguage(langRes[0]);
        }

        if (sessionId) {
          const qRes = await codingService.getQuestions(sessionId);
          if (qRes && qRes.length > 0) {
            setQuestions(qRes);
            const historyResults = await Promise.allSettled(qRes.map((question) => (
              codingService.getSubmissionHistory(
                sessionId,
                question.codingQuestionId ?? question.id,
              )
            )));
            setSubmittedQuestionIds(new Set(historyResults.flatMap((history, index) => (
              history.status === 'fulfilled' && history.value?.length
                ? [qRes[index].codingQuestionId ?? qRes[index].id]
                : []
            ))));
          }
        }
      } catch (err) {
        notify.error(t('coding.loadFailed'));
        console.error(err);
      }
    };
    fetchInitialData();
  }, [sessionId]);

  const currentQuestion = questions[currentQuestionIndex];
  const getQuestionId = (question) => question?.codingQuestionId ?? question?.id;
  const canComplete = questions.length > 0
    && questions.every((question) => submittedQuestionIds.has(getQuestionId(question)));

  // Update starter code when question or language changes
  useEffect(() => {
    if (currentQuestion && selectedLanguage) {
      const template = currentQuestion.templates?.find(t => t.languageId === selectedLanguage.id);
      if (template) {
        setCode(template.templateCode);
      } else {
        setCode(`// ${t('coding.writeCodePrompt', { language: selectedLanguage.name })}\n`);
      }
    }
  }, [currentQuestion, selectedLanguage, t]);

  const handleEditorDidMount = (editor, monaco) => {
    editorRef.current = editor;
  };

  const handleLanguageChange = (e) => {
    const langId = parseInt(e.target.value, 10);
    const lang = languages.find(l => l.id === langId);
    setSelectedLanguage(lang);
  };

  const handleSubmit = async () => {
    if (!currentQuestion || !selectedLanguage) return;
    
    setIsSubmitting(true);
    setSubmissionResult(null);
    try {
      const payload = {
        interviewSessionId: parseInt(sessionId, 10),
        codingQuestionId: getQuestionId(currentQuestion),
        languageId: selectedLanguage.id,
        sourceCode: code
      };
      const res = await codingService.submitCode(payload);
      setSubmissionResult(res);
      setSubmittedQuestionIds((previous) => new Set(previous).add(getQuestionId(currentQuestion)));
      notify.success(t('coding.submitSuccess'));
    } catch (err) {
      notify.error(err.message || t('coding.submitFailed'));
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleCompleteRound = async () => {
    if (!canComplete || isCompleting) return;
    setIsCompleting(true);
    try {
      const campaign = await interviewSessionService.completeSession(sessionId);
      const nextSession = getNextOpenSession(campaign, sessionId);
      const currentContext = getActiveInterviewContext();
      saveActiveInterviewContext({
        campaign,
        activeSessionId: nextSession?.status === 'Active' ? nextSession.interviewSessionId : null,
        configurationKey: currentContext?.configurationKey || null,
      });
      navigate(nextSession
        ? getInterviewRoomPath(nextSession.interviewSessionId)
        : getCampaignResultPath(campaign.interviewCampaignId), { replace: true });
    } catch (err) {
      notify.error(err.message || t('coding.completeFailed'));
    } finally {
      setIsCompleting(false);
    }
  };

  if (!questions.length) {
    return <div className="coding-interview-loading">{t('coding.loading')}</div>;
  }

  // Monaco editor expects language name in lowercase (e.g., 'javascript', 'python', 'java', 'csharp', 'cpp')
  const getMonacoLanguage = (langName) => {
    const lower = (langName || '').toLowerCase();
    if (lower.includes('c++') || lower.includes('cpp')) return 'cpp';
    if (lower.includes('c#') || lower.includes('csharp')) return 'csharp';
    if (lower.includes('python')) return 'python';
    if (lower.includes('java') && !lower.includes('script')) return 'java';
    if (lower.includes('javascript') || lower.includes('js')) return 'javascript';
    if (lower.includes('typescript') || lower.includes('ts')) return 'typescript';
    if (lower.includes('go')) return 'go';
    if (lower.includes('ruby')) return 'ruby';
    if (lower.includes('rust')) return 'rust';
    if (lower.includes('php')) return 'php';
    if (lower.includes('swift')) return 'swift';
    if (lower.includes('kotlin')) return 'kotlin';
    return 'plaintext';
  };

  return (
    <div className="coding-interview-container">
      <div className="coding-header">
        <h2>{t('coding.title')}</h2>
        <div className="coding-header-actions">
          {questions.length > 1 && (
            <div className="question-nav">
              <button 
                disabled={currentQuestionIndex === 0}
                onClick={() => setCurrentQuestionIndex(prev => prev - 1)}
              >
                {t('coding.previousQuestion')}
              </button>
              <span>{currentQuestionIndex + 1} / {questions.length}</span>
              <button 
                disabled={currentQuestionIndex === questions.length - 1}
                onClick={() => setCurrentQuestionIndex(prev => prev + 1)}
              >
                {t('coding.nextQuestion')}
              </button>
            </div>
          )}
          <button
            className="btn-finish-coding"
            type="button"
            disabled={!canComplete || isCompleting}
            onClick={handleCompleteRound}
          >
            {isCompleting ? t('coding.finalizing') : t('coding.finishRound')}
          </button>
        </div>
      </div>
      
      <div className="coding-split-pane">
        <div className="coding-left-pane">
          <div className="question-details">
            <h3>{currentQuestion.title}</h3>
            
            <div className="meta-tags">
              <span className={`difficulty ${currentQuestion.difficulty?.toLowerCase()}`}>
                {currentQuestion.difficulty}
              </span>
              {currentQuestion.skill && (
                <span className="skill-tag">{currentQuestion.skill}</span>
              )}
            </div>

            <div className="markdown-content">
              <h4>{t('coding.problemStatement')}</h4>
              <p>{currentQuestion.description}</p>
              
              {currentQuestion.inputDescription && (
                <>
                  <h4>{t('coding.inputDescription')}</h4>
                  <p>{currentQuestion.inputDescription}</p>
                </>
              )}
              
              {currentQuestion.outputDescription && (
                <>
                  <h4>{t('coding.outputDescription')}</h4>
                  <p>{currentQuestion.outputDescription}</p>
                </>
              )}

              {currentQuestion.constraints && (
                <>
                  <h4>{t('coding.constraints')}</h4>
                  <pre>{currentQuestion.constraints}</pre>
                </>
              )}

              {currentQuestion.examples && (
                <>
                  <h4>{t('coding.examples')}</h4>
                  <pre>{currentQuestion.examples}</pre>
                </>
              )}
            </div>
          </div>
        </div>
        
        <div className="coding-right-pane">
          <div className="editor-toolbar">
            <select value={selectedLanguage?.id || ''} onChange={handleLanguageChange}>
              {languages.map(lang => (
                <option key={lang.id} value={lang.id}>{lang.name}</option>
              ))}
            </select>
            
            <button 
              className="btn-submit" 
              onClick={handleSubmit}
              disabled={isSubmitting}
            >
              {isSubmitting ? t('coding.running') : t('coding.runAndSubmit')}
            </button>
          </div>
          
          <div className="editor-container">
            <Editor
              height="100%"
              language={getMonacoLanguage(selectedLanguage?.name)}
              theme="vs-dark"
              value={code}
              onChange={(value) => setCode(value)}
              onMount={handleEditorDidMount}
              options={{
                minimap: { enabled: false },
                fontSize: 14,
                wordWrap: 'on'
              }}
            />
          </div>

          {submissionResult && (
            <div className="submission-result-panel">
              <h4>{t('coding.submissionResults')}</h4>
              <div className="result-stats">
                <span className={submissionResult.status === 'Accepted' ? 'status-accepted' : 'status-error'}>
                  {submissionResult.status || t('common.unknown')}
                </span>
                <span>{t('coding.passed', { passed: submissionResult.passedTestCases, total: submissionResult.totalTestCases })}</span>
                <span>{t('coding.time', { value: submissionResult.maxTimeMs })}</span>
                <span>{t('coding.memory', { value: submissionResult.maxMemoryKb })}</span>
              </div>
              
              <div className="test-cases-results">
                {submissionResult.testCaseResults && submissionResult.testCaseResults.map((tc, idx) => {
                  const passed = tc.status === 'Accepted';
                  return (
                  <div key={tc.testCaseId || idx} className={`test-case-card ${passed ? 'passed' : 'failed'}`}>
                    <h5>{t('coding.testCase', { index: idx + 1 })} {passed ? '✓' : '✕'}</h5>
                    {tc.stderr || tc.compileOutput ? (
                      <div className="error-output">
                        <strong>{t('coding.error')}</strong> <pre>{tc.stderr || tc.compileOutput}</pre>
                      </div>
                    ) : (
                      <div className="execution-details">
                        <p><strong>{t('coding.status')}</strong> {tc.status}</p>
                        <p><strong>{t('coding.timeLabel')}</strong> {tc.timeMs}ms</p>
                        <p><strong>{t('coding.memoryLabel')}</strong> {tc.memoryKb}KB</p>
                      </div>
                    )}
                  </div>
                );})}
              </div>
              
              {submissionResult.compileOutput && (
                <div className="compile-output">
                  <h5>{t('coding.compilationOutput')}</h5>
                  <pre>{submissionResult.compileOutput}</pre>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default CodingInterviewPage;
