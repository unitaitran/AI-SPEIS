import React, { useState, useEffect, useRef } from 'react';
import Editor from '@monaco-editor/react';
import { codingService } from '../../../services/codingService';
import interviewSessionService from '../../../services/InterviewSessionService';
import { navigate } from '../../../routes/navigation';
import { getCampaignResultPath, getInterviewRoomPath } from '../../../routes/routePaths';
import { getActiveInterviewContext, getNextOpenSession, saveActiveInterviewContext } from '../../../utils/interviewContext';
import notify from '../../../utils/notification';
import UserLayout from '../../../layouts/user/UserLayout';
import '../../../styles/user/CodingInterviewPage.css';

// Mainstream interview languages category helper
const getLanguageCategory = (name) => {
  const lower = (name || '').toLowerCase().trim();
  if (lower.includes('python')) return 'python';
  if (lower.includes('javascript') || lower.includes('js')) return 'javascript';
  if (lower.includes('java') && !lower.includes('script')) return 'java';
  if (lower.includes('c++') || lower.includes('cpp')) return 'cpp';
  if (lower.includes('c#') || lower.includes('csharp')) return 'csharp';
  if (lower.startsWith('c ') || lower === 'c') return 'c';
  return null;
};

const CodingInterviewPage = ({ sessionId }) => {
  const [questions, setQuestions] = useState([]);
  const [currentQuestionIndex, setCurrentQuestionIndex] = useState(0);
  const [languages, setLanguages] = useState([]);
  const [selectedLanguage, setSelectedLanguage] = useState(null);

  // Save user written code per question & language: key = `${questionId}_${languageId}`
  const [userCodes, setUserCodes] = useState({});
  const [code, setCode] = useState('');

  const [isRunning, setIsRunning] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [runResult, setRunResult] = useState(null);
  const [submissionResult, setSubmissionResult] = useState(null);

  const [submittedQuestionIds, setSubmittedQuestionIds] = useState(() => new Set());
  const [isCompleting, setIsCompleting] = useState(false);
  const [activeRightTab, setActiveRightTab] = useState('console'); // 'console' | 'samples'

  // Resizable Panes State & Refs
  const [leftWidth, setLeftWidth] = useState(40); // Left pane width in %
  const [consoleHeight, setConsoleHeight] = useState(260); // Console height in px
  const isDraggingHRef = useRef(false);
  const isDraggingVRef = useRef(false);
  const splitPaneRef = useRef(null);
  const rightPaneRef = useRef(null);

  const editorRef = useRef(null);

  // Drag handler for horizontal splitter (Left/Right panes)
  const handleHorizontalMouseDown = (e) => {
    e.preventDefault();
    isDraggingHRef.current = true;
    document.body.style.cursor = 'col-resize';
    document.body.style.userSelect = 'none';

    const onMouseMove = (moveEvent) => {
      if (!isDraggingHRef.current || !splitPaneRef.current) return;
      const rect = splitPaneRef.current.getBoundingClientRect();
      const newWidth = ((moveEvent.clientX - rect.left) / rect.width) * 100;
      if (newWidth >= 20 && newWidth <= 75) {
        setLeftWidth(newWidth);
      }
    };

    const onMouseUp = () => {
      isDraggingHRef.current = false;
      document.body.style.cursor = '';
      document.body.style.userSelect = '';
      window.removeEventListener('mousemove', onMouseMove);
      window.removeEventListener('mouseup', onMouseUp);
    };

    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', onMouseUp);
  };

  // Drag handler for vertical splitter (Editor/Console panels)
  const handleVerticalMouseDown = (e) => {
    e.preventDefault();
    isDraggingVRef.current = true;
    document.body.style.cursor = 'row-resize';
    document.body.style.userSelect = 'none';

    const onMouseMove = (moveEvent) => {
      if (!isDraggingVRef.current || !rightPaneRef.current) return;
      const rect = rightPaneRef.current.getBoundingClientRect();
      const newHeight = rect.bottom - moveEvent.clientY;
      if (newHeight >= 80 && newHeight <= rect.height - 120) {
        setConsoleHeight(newHeight);
      }
    };

    const onMouseUp = () => {
      isDraggingVRef.current = false;
      document.body.style.cursor = '';
      document.body.style.userSelect = '';
      window.removeEventListener('mousemove', onMouseMove);
      window.removeEventListener('mouseup', onMouseUp);
    };

    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', onMouseUp);
  };

  useEffect(() => {
    const fetchInitialData = async () => {
      try {
        const langRes = await codingService.getLanguages();
        if (langRes && langRes.length > 0) {
          // Filter only mainstream interview languages: C, C#, Python, Java, JavaScript, C++
          const categoryMap = {};
          langRes.forEach((lang) => {
            const cat = getLanguageCategory(lang.name);
            if (cat) {
              if (!categoryMap[cat] || lang.id > categoryMap[cat].id) {
                categoryMap[cat] = lang;
              }
            }
          });

          const desiredOrder = ['c', 'csharp', 'python', 'java', 'javascript', 'cpp'];
          const filteredLangs = desiredOrder
            .map((cat) => categoryMap[cat])
            .filter(Boolean);

          const finalLangs = filteredLangs.length > 0 ? filteredLangs : langRes;
          setLanguages(finalLangs);
          setSelectedLanguage(finalLangs[0]);
        }

        if (sessionId) {
          try {
            const startedCampaign = await interviewSessionService.startSession(sessionId);
            if (startedCampaign?.sessions) {
              const currentContext = getActiveInterviewContext();
              saveActiveInterviewContext({
                campaign: startedCampaign,
                activeSessionId: parseInt(sessionId, 10),
                configurationKey: currentContext?.configurationKey || null,
              });
            }
          } catch (startErr) {
            // Ignore if session is already active or started
            console.log('Coding session start check:', startErr?.message || startErr);
          }

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
        notify.error('Failed to load coding interview data');
        console.error(err);
      }
    };
    fetchInitialData();
  }, [sessionId]);

  const currentQuestion = questions[currentQuestionIndex];
  const getQuestionId = (q) => q?.codingQuestionId ?? q?.id;
  const currentQId = getQuestionId(currentQuestion);

  const canComplete = questions.length > 0
    && questions.every((q) => submittedQuestionIds.has(getQuestionId(q)));

  // Generate fallback starter code template based on question signature and language
  const getFallbackStarterCode = (q, lang) => {
    if (!q || !lang) return '';
    const langName = lang.name || '';
    const lowerLang = langName.toLowerCase();
    const sig = q.functionSignature;
    const fnName = q.functionName || 'solution';

    if (sig) {
      return `// ${sig}\n// Write your solution below\n`;
    }

    if (lowerLang.includes('python')) {
      return `def ${fnName}(*args):\n    # Write your code here\n    pass\n`;
    }
    if (lowerLang.includes('javascript') || lowerLang.includes('js')) {
      return `function ${fnName}(...args) {\n  // Write your code here\n}\n\nmodule.exports.${fnName} = ${fnName};\n`;
    }
    if (lowerLang.includes('c#') || lowerLang.includes('csharp')) {
      return `using System;\nusing System.Collections.Generic;\n\npublic class Solution\n{\n    public void ${fnName}()\n    {\n        // Write your code here\n    }\n}\n`;
    }
    if (lowerLang.includes('java') && !lowerLang.includes('script')) {
      return `import java.util.*;\n\npublic class Solution {\n    public void ${fnName}() {\n        // Write your code here\n    }\n}\n`;
    }
    if (lowerLang.includes('c++') || lowerLang.includes('cpp')) {
      return `#include <iostream>\n#include <vector>\nusing namespace std;\n\nclass Solution {\npublic:\n    void ${fnName}() {\n        // Write your code here\n    }\n};\n`;
    }
    if (lowerLang.startsWith('c ') || lowerLang === 'c') {
      return `#include <stdio.h>\n#include <stdlib.h>\n\nvoid ${fnName}() {\n    // Write your code here\n}\n`;
    }

    return `// Write your ${langName} code here\n`;
  };

  // Sync code whenever currentQuestion or selectedLanguage changes
  useEffect(() => {
    if (!currentQId || !selectedLanguage) return;

    const codeKey = `${currentQId}_${selectedLanguage.id}`;
    if (userCodes[codeKey] !== undefined) {
      setCode(userCodes[codeKey]);
    } else {
      // Check templates from DB
      const template = currentQuestion?.templates?.find(t => t.languageId === selectedLanguage.id);
      const initialCode = template ? template.templateCode : getFallbackStarterCode(currentQuestion, selectedLanguage);

      setCode(initialCode);
      setUserCodes(prev => ({ ...prev, [codeKey]: initialCode }));
    }

    // Reset temporary run result when switching question
    setRunResult(null);
    setSubmissionResult(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentQuestionIndex, selectedLanguage?.id]);

  // Handle Monaco code changes and persist to userCodes state
  const handleCodeChange = (newValue) => {
    const val = newValue ?? '';
    setCode(val);
    if (currentQId && selectedLanguage) {
      const codeKey = `${currentQId}_${selectedLanguage.id}`;
      setUserCodes(prev => ({ ...prev, [codeKey]: val }));
    }
  };

  const handleEditorDidMount = (editor) => {
    editorRef.current = editor;
  };

  const handleLanguageChange = (e) => {
    const langId = parseInt(e.target.value, 10);
    const lang = languages.find(l => l.id === langId);
    if (lang) {
      setSelectedLanguage(lang);
    }
  };

  // 1. RUN CODE (Chạy Thử đối với Sample Test Cases)
  const handleRunCode = async () => {
    if (!currentQuestion || !selectedLanguage) return;

    setIsRunning(true);
    setRunResult(null);
    setActiveRightTab('console');

    try {
      const payload = {
        interviewSessionId: 0, // Test Run
        codingQuestionId: currentQId,
        languageId: selectedLanguage.id,
        sourceCode: code,
        isTestRun: true
      };

      const res = await codingService.submitCode(payload);
      setRunResult(res);
      if (res.status === 'Accepted') {
        notify.success(`Chạy thử hoàn tất: All ${res.passedTestCases}/${res.totalTestCases} sample test cases PASSED!`);
      } else {
        notify.info(`Chạy thử xong: ${res.passedTestCases}/${res.totalTestCases} sample test cases đã đạt (${res.status})`);
      }
    } catch (err) {
      notify.error(err.message || 'Lỗi khi chạy thử code');
    } finally {
      setIsRunning(false);
    }
  };

  // 2. SUBMIT CODE (Nộp Bài chính thức)
  const handleSubmitCode = async () => {
    if (!currentQuestion || !selectedLanguage) return;

    setIsSubmitting(true);
    setSubmissionResult(null);
    setActiveRightTab('console');

    try {
      const payload = {
        interviewSessionId: parseInt(sessionId, 10),
        codingQuestionId: currentQId,
        languageId: selectedLanguage.id,
        sourceCode: code,
        isTestRun: false
      };

      const res = await codingService.submitCode(payload);
      setSubmissionResult(res);
      setSubmittedQuestionIds((prev) => new Set(prev).add(currentQId));

      if (res.status === 'Accepted') {
        notify.success('Nộp bài thành công! Tất cả test cases đều vượt qua.');
      } else {
        notify.warning(`Đã nộp bài. Trạng thái: ${res.status} (${res.passedTestCases}/${res.totalTestCases} passed)`);
      }
    } catch (err) {
      notify.error(err.message || 'Lỗi khi nộp bài');
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
      notify.error(err.message || 'Lỗi khi hoàn thành vòng Coding');
    } finally {
      setIsCompleting(false);
    }
  };

  if (!questions.length) {
    return <div className="coding-interview-loading">Đang tải phiên phỏng vấn Coding...</div>;
  }

  // Map language name to Monaco language string
  const getMonacoLanguage = (langName) => {
    const lower = (langName || '').toLowerCase();
    if (lower.includes('c++') || lower.includes('cpp')) return 'cpp';
    if (lower.includes('c#') || lower.includes('csharp')) return 'csharp';
    if (lower.startsWith('c ') || lower === 'c') return 'c';
    if (lower.includes('python')) return 'python';
    if (lower.includes('java') && !lower.includes('script')) return 'java';
    if (lower.includes('javascript') || lower.includes('js')) return 'javascript';
    if (lower.includes('typescript') || lower.includes('ts')) return 'typescript';
    if (lower.includes('go')) return 'go';
    if (lower.includes('ruby')) return 'ruby';
    if (lower.includes('rust')) return 'rust';
    if (lower.includes('php')) return 'php';
    return 'plaintext';
  };

  const activeResult = submissionResult || runResult;
  const isSubmission = !!submissionResult;

  return (
    <UserLayout compactSidebar immersive>
      <div className="coding-interview-container">
        {/* HEADER NAVBAR */}
        <div className="coding-header">
          <div className="coding-title-badge">
            <h2>Coding Interview</h2>
            {currentQuestion?.jobRole && (
              <span className="job-role-pill">{currentQuestion.jobRole}</span>
            )}
          </div>

          <div className="coding-header-actions">
            {questions.length > 1 && (
              <div className="question-nav">
                <button
                  disabled={currentQuestionIndex === 0}
                  onClick={() => setCurrentQuestionIndex(prev => prev - 1)}
                >
                  ◀ Câu trước
                </button>
                <span className="question-counter">
                  Câu {currentQuestionIndex + 1} / {questions.length}
                  {submittedQuestionIds.has(currentQId) && (
                    <span className="submitted-check" title="Đã nộp bài"> ✓</span>
                  )}
                </span>
                <button
                  disabled={currentQuestionIndex === questions.length - 1}
                  onClick={() => setCurrentQuestionIndex(prev => prev + 1)}
                >
                  Câu tiếp ▶
                </button>
              </div>
            )}
            <button
              className="btn-finish-coding"
              type="button"
              disabled={!canComplete || isCompleting}
              onClick={handleCompleteRound}
            >
              {isCompleting ? 'Đang hoàn tất...' : 'Hoàn thành bài test'}
            </button>
          </div>
        </div>

        {/* SPLIT PANE CONTENT */}
        <div className="coding-split-pane" ref={splitPaneRef}>
          {/* LEFT PANE: PROBLEM DESCRIPTION & DETAILS */}
          <div className="coding-left-pane" style={{ width: `${leftWidth}%`, flex: `0 0 ${leftWidth}%` }}>
            <div className="question-details">
            <h3>{currentQuestion.title}</h3>

            <div className="meta-tags">
              {currentQuestion.difficulty && (
                <span className={`difficulty ${currentQuestion.difficulty.toLowerCase()}`}>
                  {currentQuestion.difficulty}
                </span>
              )}
              {currentQuestion.skill && (
                <span className="skill-tag">{currentQuestion.skill}</span>
              )}
              {currentQuestion.subskill && (
                <span className="subskill-tag">{currentQuestion.subskill}</span>
              )}
            </div>

            {/* TIME & SPACE COMPLEXITY HINTS */}
            {(currentQuestion.expectedTimeComplexity || currentQuestion.expectedSpaceComplexity) && (
              <div className="complexity-box">
                {currentQuestion.expectedTimeComplexity && (
                  <span>⏱ <strong>Time:</strong> {currentQuestion.expectedTimeComplexity}</span>
                )}
                {currentQuestion.expectedSpaceComplexity && (
                  <span>💾 <strong>Space:</strong> {currentQuestion.expectedSpaceComplexity}</span>
                )}
              </div>
            )}

            {/* FUNCTION SIGNATURE HINT */}
            {currentQuestion.functionSignature && (
              <div className="function-sig-box">
                <span className="sig-label">Function Signature:</span>
                <code>{currentQuestion.functionSignature}</code>
              </div>
            )}

            <div className="markdown-content">
              <h4>Mô tả bài toán (Problem Statement)</h4>
              <p>{currentQuestion.description}</p>

              {currentQuestion.inputDescription && (
                <>
                  <h4>Đầu vào (Input Description)</h4>
                  <p>{currentQuestion.inputDescription}</p>
                </>
              )}

              {currentQuestion.outputDescription && (
                <>
                  <h4>Đầu ra (Output Description)</h4>
                  <p>{currentQuestion.outputDescription}</p>
                </>
              )}

              {currentQuestion.constraints && (
                <>
                  <h4>Ràng buộc (Constraints)</h4>
                  <pre>{currentQuestion.constraints}</pre>
                </>
              )}

              {currentQuestion.examples && (
                <>
                  <h4>Ví dụ mẫu (Examples)</h4>
                  <pre>{currentQuestion.examples}</pre>
                </>
              )}
            </div>
          </div>
        </div>

        {/* HORIZONTAL RESIZER */}
        <div
          className="resizer-horizontal"
          onMouseDown={handleHorizontalMouseDown}
          title="Kéo sang trái/phải để thay đổi kích thước màn hình"
        />

        {/* RIGHT PANE: EDITOR + CONTROL TOOLBAR + TEST CONSOLE */}
        <div className="coding-right-pane" ref={rightPaneRef} style={{ width: `${100 - leftWidth}%` }}>
            <div className="editor-toolbar">
              <div className="language-selector">
                <label htmlFor="language-select">Ngôn ngữ:</label>
                <select
                  id="language-select"
                  value={selectedLanguage?.id || ''}
                  onChange={handleLanguageChange}
                >
                  {languages.map(lang => (
                    <option key={lang.id} value={lang.id}>{lang.name}</option>
                  ))}
                </select>
              </div>

              <div className="editor-actions">
                {/* BUTTON 1: RUN CODE (CHẠY THỬ) */}
                <button
                  className="btn-run-code"
                  onClick={handleRunCode}
                  disabled={isRunning || isSubmitting}
                  title="Chạy thử code trên sample test cases mà không lưu điểm"
                >
                  {isRunning ? '⏳ Đang chạy...' : '▶ Chạy thử (Run Code)'}
                </button>

                {/* BUTTON 2: SUBMIT CODE (NỘP BÀI) */}
                <button
                  className="btn-submit"
                  onClick={handleSubmitCode}
                  disabled={isRunning || isSubmitting}
                  title="Nộp bài chấm điểm trên tất cả test cases"
                >
                  {isSubmitting ? '⏳ Đang nộp...' : '✓ Nộp bài (Submit)'}
                </button>
              </div>
            </div>

            {/* MONACO CODE EDITOR */}
            <div className="editor-container">
              <Editor
                height="100%"
                language={getMonacoLanguage(selectedLanguage?.name)}
                theme="vs-dark"
                value={code}
                onChange={handleCodeChange}
                onMount={handleEditorDidMount}
                options={{
                  minimap: { enabled: false },
                  fontSize: 14,
                  wordWrap: 'on',
                  automaticLayout: true,
                  scrollBeyondLastLine: false
                }}
              />
            </div>

            {/* VERTICAL RESIZER */}
            <div
              className="resizer-vertical"
              onMouseDown={handleVerticalMouseDown}
              title="Kéo lên/xuống để thay đổi chiều cao console"
            />

            {/* OUTPUT & TEST RESULTS CONSOLE PANEL */}
            <div className="submission-result-panel" style={{ height: `${consoleHeight}px` }}>
            <div className="panel-tabs">
              <button
                className={`tab-btn ${activeRightTab === 'console' ? 'active' : ''}`}
                onClick={() => setActiveRightTab('console')}
              >
                💻 Kết quả thực thi {activeResult ? `(${isSubmission ? 'Nộp bài' : 'Chạy thử'})` : ''}
              </button>
              <button
                className={`tab-btn ${activeRightTab === 'samples' ? 'active' : ''}`}
                onClick={() => setActiveRightTab('samples')}
              >
                🧪 Sample Test Cases ({currentQuestion?.sampleTestCases?.length || 0})
              </button>
            </div>

            {activeRightTab === 'console' && (
              <div className="tab-content">
                {!activeResult ? (
                  <div className="empty-console-message">
                    Nhấn <strong>▶ Chạy thử (Run Code)</strong> để kiểm tra code trên Sample Test Cases hoặc <strong>✓ Nộp bài (Submit)</strong> để gửi kết quả chính thức.
                  </div>
                ) : (
                  <>
                    {/* STATS BAR */}
                    <div className="result-stats">
                      <span className={activeResult.status === 'Accepted' ? 'status-accepted' : 'status-error'}>
                        {isSubmission ? 'Nộp bài: ' : 'Chạy thử: '} {activeResult.status || 'Unknown'}
                      </span>
                      <span>Đạt: <strong>{activeResult.passedTestCases} / {activeResult.totalTestCases}</strong> test cases</span>
                      <span>Thời gian: <strong>{activeResult.maxTimeMs} ms</strong></span>
                      <span>Bộ nhớ: <strong>{activeResult.maxMemoryKb} KB</strong></span>
                    </div>

                    {/* TOP-LEVEL COMPILATION / RUNTIME ERROR LOG BOX */}
                    {(activeResult.compileOutput || activeResult.stderr) && (
                      <div className="top-error-box">
                        <div className="top-error-header">
                          ⚠️ <strong>Lỗi Biên Dịch / Log Hệ Thống (Compilation & System Errors):</strong>
                        </div>
                        <pre>{activeResult.compileOutput || activeResult.stderr}</pre>
                      </div>
                    )}

                    {/* DETAILED TEST CASE CARDS */}
                    <div className="test-cases-results">
                      {activeResult.testCaseResults && activeResult.testCaseResults.map((tc, idx) => {
                        const passed = tc.status === 'Accepted';
                        return (
                          <div key={tc.testCaseId || idx} className={`test-case-card ${passed ? 'passed' : 'failed'}`}>
                            <div className="test-case-header">
                              <h5>Test Case #{idx + 1} {tc.isSample ? '(Sample)' : '(Hidden)'}</h5>
                              <span className={`status-tag ${passed ? 'passed' : 'failed'}`}>
                                {passed ? 'PASSED ✓' : `${tc.status} ✕`}
                              </span>
                            </div>

                            <div className="execution-details">
                              {/* INPUT */}
                              {tc.input && (
                                <div className="io-row">
                                  <span>Input:</span>
                                  <code className="input-code">{tc.input}</code>
                                </div>
                              )}

                              {/* EXPECTED OUTPUT */}
                              {tc.expectedOutput !== null && tc.expectedOutput !== undefined && (
                                <div className="io-row">
                                  <span>Expected Output:</span>
                                  <code className="expected-code">{tc.expectedOutput}</code>
                                </div>
                              )}

                              {/* ACTUAL OUTPUT */}
                              {tc.actualOutput !== null && tc.actualOutput !== undefined && (
                                <div className="io-row">
                                  <span>Your Output:</span>
                                  <code className={passed ? 'actual-passed' : 'actual-failed'}>
                                    {tc.actualOutput !== '' ? tc.actualOutput : '(Empty Output)'}
                                  </code>
                                </div>
                              )}

                              {/* STDERR / TRACEBACK FOR THIS TESTCASE */}
                              {(tc.stderr || tc.compileOutput) && (
                                <div className="error-output">
                                  <strong>Traceback / Stderr:</strong>
                                  <pre>{tc.stderr || tc.compileOutput}</pre>
                                </div>
                              )}

                              <div className="metrics-row">
                                <span>Time: {tc.timeMs}ms</span>
                                <span>Memory: {tc.memoryKb}KB</span>
                              </div>
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  </>
                )}
              </div>
            )}

            {activeRightTab === 'samples' && (
              <div className="tab-content sample-cases-tab">
                {currentQuestion.sampleTestCases && currentQuestion.sampleTestCases.length > 0 ? (
                  currentQuestion.sampleTestCases.map((stc, idx) => (
                    <div key={stc.testCaseId || idx} className="sample-case-box">
                      <h5>Sample Test Case #{idx + 1}</h5>
                      <div className="io-pair">
                        <div>
                          <strong>Input:</strong>
                          <pre>{stc.input || '(Empty)'}</pre>
                        </div>
                        <div>
                          <strong>Expected Output:</strong>
                          <pre>{stc.expectedOutput}</pre>
                        </div>
                      </div>
                    </div>
                  ))
                ) : (
                  <p className="no-samples">Không có sample test cases hiển thị.</p>
                )}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  </UserLayout>

  );
};

export default CodingInterviewPage;
