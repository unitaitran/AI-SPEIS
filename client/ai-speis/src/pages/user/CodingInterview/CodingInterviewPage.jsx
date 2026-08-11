import React, { useState, useEffect, useRef, Component } from 'react';
import Editor, { loader } from '@monaco-editor/react';
import { codingService } from '../../../services/codingService';
import interviewSessionService from '../../../services/InterviewSessionService';
import { navigate } from '../../../routes/navigation';
import { getCampaignResultPath, getInterviewRoomPath } from '../../../routes/routePaths';
import { getActiveInterviewContext, getNextOpenSession, saveActiveInterviewContext } from '../../../utils/interviewContext';
import notify from '../../../utils/notification';
import UserLayout from '../../../layouts/user/UserLayout';
import EndSessionConfirmDialog from '../../../components/technicalInterview/EndSessionConfirmDialog';
import EvaluatingAnalysisModal from '../../../components/interviewRoom/EvaluatingAnalysisModal';
import '../../../styles/user/CodingInterviewPage.css';

// Configure reliable CDN path for Monaco Editor
loader.config({
  paths: {
    vs: 'https://cdn.jsdelivr.net/npm/monaco-editor@0.43.0/min/vs'
  }
});

// Helper to safely format JSON or Code strings
const safeFormatJson = (val) => {
  if (val === null || val === undefined) return '';
  if (typeof val === 'object') return JSON.stringify(val, null, 2);
  try {
    const parsed = JSON.parse(val);
    if (typeof parsed === 'object') return JSON.stringify(parsed, null, 2);
    return String(parsed);
  } catch {
    return String(val);
  }
};

// Helper to safely parse example data structure
const parseExamplesList = (examplesRaw) => {
  if (!examplesRaw) return [];
  if (Array.isArray(examplesRaw)) return examplesRaw;
  if (typeof examplesRaw === 'object') return [examplesRaw];
  try {
    const parsed = JSON.parse(examplesRaw);
    if (Array.isArray(parsed)) return parsed;
    if (typeof parsed === 'object') return [parsed];
    return [{ input: String(parsed) }];
  } catch {
    return [{ rawText: String(examplesRaw) }];
  }
};

// Error boundary to prevent uncaught Monaco CDN network errors from crashing React app
class MonacoErrorBoundary extends Component {
  constructor(props) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError() {
    return { hasError: true };
  }

  componentDidCatch(error, errorInfo) {
    console.warn('Monaco Editor failed to load from CDN. Using fallback textarea:', error, errorInfo);
  }

  render() {
    if (this.state.hasError) {
      return (
        <div className="monaco-fallback-container">
          <div className="monaco-fallback-warning">
            ⚠️ Monaco Editor CDN load error. Fallback Text Editor active.
          </div>
          <textarea
            className="monaco-fallback-textarea"
            value={this.props.value || ''}
            onChange={(e) => this.props.onChange(e.target.value)}
            placeholder="Write your solution code here..."
          />
        </div>
      );
    }
    return this.props.children;
  }
}

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
  const [isEndConfirmOpen, setIsEndConfirmOpen] = useState(false);
  const [activeRightTab, setActiveRightTab] = useState('console'); // 'console' | 'samples'

  // Resizable Panes State & Refs
  const [leftWidth, setLeftWidth] = useState(40); // Left pane width in %
  const [consoleHeight, setConsoleHeight] = useState(280); // Console height in px
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
      return `// Signature: ${sig}\n// Write your solution below\n`;
    }

    if (lowerLang.includes('python')) {
      return `def ${fnName}(*args):\n    # Write your Python code here\n    pass\n`;
    }
    if (lowerLang.includes('javascript') || lowerLang.includes('js')) {
      return `function ${fnName}(...args) {\n  // Write your JavaScript code here\n}\n`;
    }
    if (lowerLang.includes('c#') || lowerLang.includes('csharp')) {
      return `using System;\nusing System.Collections.Generic;\n\npublic class Solution\n{\n    public object ${fnName}(params object[] args)\n    {\n        // Write your C# solution here\n        return null;\n    }\n}\n`;
    }
    if (lowerLang.includes('java') && !lowerLang.includes('script')) {
      return `import java.util.*;\n\npublic class Solution {\n    public Object ${fnName}(Object... args) {\n        // Write your Java solution here\n        return null;\n    }\n}\n`;
    }
    if (lowerLang.includes('c++') || lowerLang.includes('cpp')) {
      return `#include <iostream>\n#include <vector>\n#include <string>\nusing namespace std;\n\nclass Solution {\npublic:\n    void ${fnName}() {\n        // Write your C++ solution here\n    }\n};\n`;
    }
    if (lowerLang.startsWith('c ') || lowerLang === 'c') {
      return `#include <stdio.h>\n#include <stdlib.h>\n\nvoid ${fnName}() {\n    // Write your C solution here\n}\n`;
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
    if (isCompleting) return;
    setIsCompleting(true);
    try {
      // Auto-submit code for any unsubmitted question if candidate entered code
      if (questions && questions.length > 0 && selectedLanguage) {
        for (const q of questions) {
          const qId = getQuestionId(q);
          if (!submittedQuestionIds.has(qId)) {
            const codeKey = `${qId}_${selectedLanguage.id}`;
            const writtenCode = userCodes[codeKey] || (qId === currentQId ? code : '');
            if (writtenCode && writtenCode.trim().length > 10) {
              try {
                await codingService.submitCode({
                  interviewSessionId: parseInt(sessionId, 10),
                  codingQuestionId: qId,
                  languageId: selectedLanguage.id,
                  sourceCode: writtenCode,
                  isTestRun: false,
                });
              } catch {
                // Best-effort auto-submit before finishing round
              }
            }
          }
        }
      }

      const campaign = await interviewSessionService.completeSession(sessionId);
      const nextSession = getNextOpenSession(campaign, sessionId);
      const currentContext = getActiveInterviewContext();
      saveActiveInterviewContext({
        campaign,
        activeSessionId: nextSession?.status === 'Active' ? nextSession.interviewSessionId : null,
        configurationKey: currentContext?.configurationKey || null,
      });
      setIsEndConfirmOpen(false);
      navigate(nextSession?.status === 'Active'
        ? getInterviewRoomPath(nextSession.interviewSessionId)
        : getCampaignResultPath(campaign?.interviewCampaignId || currentContext?.campaign?.interviewCampaignId), { replace: true });
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
    <UserLayout hideSidebar immersive>
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
              disabled={isCompleting}
              onClick={() => setIsEndConfirmOpen(true)}
            >
              {isCompleting ? 'Đang hoàn tất...' : 'Kết thúc phỏng vấn'}
            </button>
          </div>
        </div>

        {/* SPLIT PANE CONTENT */}
        <div className="coding-split-pane" ref={splitPaneRef}>
          {/* LEFT PANE: PROBLEM DESCRIPTION & DETAILS */}
          <div className="coding-left-pane" style={{ width: `${leftWidth}%`, flex: `0 0 ${leftWidth}%` }}>
            <div className="question-details">
              <h3 className="problem-title">{currentQuestion.title}</h3>

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
                    <div className="complexity-pill">
                      <span className="pill-icon">⏱</span>
                      <span className="pill-label">Time:</span>
                      <code className="pill-value">{currentQuestion.expectedTimeComplexity}</code>
                    </div>
                  )}
                  {currentQuestion.expectedSpaceComplexity && (
                    <div className="complexity-pill">
                      <span className="pill-icon">💾</span>
                      <span className="pill-label">Space:</span>
                      <code className="pill-value">{currentQuestion.expectedSpaceComplexity}</code>
                    </div>
                  )}
                </div>
              )}

              {/* FUNCTION SIGNATURE CARD */}
              {currentQuestion.functionSignature && (
                <div className="signature-card">
                  <div className="sig-header">
                    <span className="sig-badge font-mono">FUNCTION SIGNATURE</span>
                  </div>
                  <pre className="sig-code"><code>{currentQuestion.functionSignature}</code></pre>
                </div>
              )}

              {/* MARKDOWN DESCRIPTION SECTIONS */}
              <div className="problem-content">
                {currentQuestion.description && (
                  <div className="desc-section">
                    <h4><span className="sec-icon">📘</span> Mô tả bài toán</h4>
                    <p className="desc-text">{currentQuestion.description}</p>
                  </div>
                )}

                {currentQuestion.inputDescription && (
                  <div className="desc-section">
                    <h4><span className="sec-icon">📥</span> Đầu vào (Input)</h4>
                    <p className="desc-text">{currentQuestion.inputDescription}</p>
                  </div>
                )}

                {currentQuestion.outputDescription && (
                  <div className="desc-section">
                    <h4><span className="sec-icon">📤</span> Đầu ra (Output)</h4>
                    <p className="desc-text">{currentQuestion.outputDescription}</p>
                  </div>
                )}

                {currentQuestion.constraints && (
                  <div className="desc-section">
                    <h4><span className="sec-icon">⚠️</span> Ràng buộc (Constraints)</h4>
                    <div className="code-block-wrapper">
                      <pre className="code-block">{currentQuestion.constraints}</pre>
                    </div>
                  </div>
                )}

                {currentQuestion.examples && (
                  <div className="desc-section">
                    <h4><span className="sec-icon">💡</span> Ví dụ mẫu (Examples)</h4>
                    <div className="examples-list">
                      {parseExamplesList(currentQuestion.examples).map((ex, idx) => (
                        <div key={idx} className="example-card">
                          <div className="example-header">
                            <span className="example-num">Ví dụ {idx + 1}</span>
                          </div>
                          {ex.input !== undefined && (
                            <div className="example-field">
                              <span className="field-lbl">Input:</span>
                              <pre className="code-block small">{safeFormatJson(ex.input)}</pre>
                            </div>
                          )}
                          {ex.output !== undefined && (
                            <div className="example-field">
                              <span className="field-lbl">Output:</span>
                              <pre className="code-block small highlight">{safeFormatJson(ex.output)}</pre>
                            </div>
                          )}
                          {ex.explanation && (
                            <div className="example-explanation">
                              <span className="exp-lbl">Giải thích:</span> {ex.explanation}
                            </div>
                          )}
                          {ex.rawText && (
                            <pre className="code-block">{ex.rawText}</pre>
                          )}
                        </div>
                      ))}
                    </div>
                  </div>
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

            {/* MONACO CODE EDITOR WITH ERROR BOUNDARY & LOADING FALLBACK */}
            <div className="editor-container">
              <MonacoErrorBoundary value={code} onChange={handleCodeChange}>
                <Editor
                  height="100%"
                  language={getMonacoLanguage(selectedLanguage?.name)}
                  theme="vs-dark"
                  value={code}
                  onChange={handleCodeChange}
                  onMount={handleEditorDidMount}
                  loading={<div className="coding-editor-loading">⚡ Loading Editor...</div>}
                  options={{
                    minimap: { enabled: false },
                    fontSize: 14,
                    wordWrap: 'on',
                    automaticLayout: true,
                    scrollBeyondLastLine: false
                  }}
                />
              </MonacoErrorBoundary>
            </div>

            {/* VERTICAL RESIZER */}
            <div
              className="resizer-vertical"
              onMouseDown={handleVerticalMouseDown}
              title="Kéo lên/xuống để thay đổi kích thước bảng Console"
            />

            {/* BOTTOM TEST CONSOLE / SAMPLE TEST CASES PANEL */}
            <div className="console-panel" style={{ height: `${consoleHeight}px` }}>
              <div className="console-tabs">
                <button
                  className={`tab-item ${activeRightTab === 'console' ? 'active' : ''}`}
                  onClick={() => setActiveRightTab('console')}
                >
                  💻 Kết quả thực thi
                </button>
                <button
                  className={`tab-item ${activeRightTab === 'samples' ? 'active' : ''}`}
                  onClick={() => setActiveRightTab('samples')}
                >
                  🧪 Sample Test Cases ({currentQuestion?.sampleTestCases?.length || 0})
                </button>
              </div>

              <div className="console-content">
                {/* TAB 1: CONSOLE EXECUTION RESULT */}
                {activeRightTab === 'console' && (
                  <div className="console-result-view">
                    {!activeResult ? (
                      <div className="empty-console-card">
                        <div className="empty-icon">🚀</div>
                        <p>Nhấn <strong>▶ Chạy thử (Run Code)</strong> để kiểm tra code trên Sample Test Cases hoặc <strong>✓ Nộp bài (Submit)</strong> để gửi kết quả chính thức.</p>
                      </div>
                    ) : (
                      <div className="result-details">
                        {/* STATUS BANNER CARD */}
                        <div className={`status-banner ${activeResult.status?.toLowerCase().replace(/[^a-z]/g, '') || 'error'}`}>
                          <div className="status-main">
                            <span className="status-icon">
                              {activeResult.status === 'Accepted' ? '✓' : '✗'}
                            </span>
                            <div className="status-info">
                              <h4 className="status-title">Status: {activeResult.status}</h4>
                              <span className="passed-counter">
                                ({activeResult.passedTestCases}/{activeResult.totalTestCases} Test Cases Passed)
                              </span>
                            </div>
                          </div>
                          {isSubmission && <span className="official-submit-badge">Official Submit</span>}
                        </div>

                        {/* COMPILATION / RUNTIME ERRORS */}
                        {activeResult.compileOutput && (
                          <div className="error-box-card">
                            <div className="box-header">
                              <span className="box-icon">🔴</span> Compile Output:
                            </div>
                            <pre className="error-content">{activeResult.compileOutput}</pre>
                          </div>
                        )}

                        {activeResult.stderr && (
                          <div className="error-box-card">
                            <div className="box-header">
                              <span className="box-icon">⚠️</span> Standard Error (stderr):
                            </div>
                            <pre className="error-content">{activeResult.stderr}</pre>
                          </div>
                        )}

                        {activeResult.message && (
                          <div className="error-box-card">
                            <div className="box-header">
                              <span className="box-icon">ℹ️</span> System Message:
                            </div>
                            <pre className="error-content">{activeResult.message}</pre>
                          </div>
                        )}

                        {activeResult.stdout && (
                          <div className="stdout-box-card">
                            <div className="box-header">
                              <span className="box-icon">💬</span> Standard Output (stdout):
                            </div>
                            <pre className="stdout-content">{activeResult.stdout}</pre>
                          </div>
                        )}

                        {/* DETAILED TEST CASE RESULTS */}
                        {activeResult.testCaseResults && activeResult.testCaseResults.length > 0 && (
                          <div className="testcase-breakdown">
                            <h4 className="breakdown-title">Chi tiết từng Test Case:</h4>
                            <div className="testcase-grid">
                              {activeResult.testCaseResults.map((tc, idx) => {
                                const isPassed = tc.passed !== undefined ? tc.passed : (tc.status === 'Accepted');
                                return (
                                  <div key={idx} className={`testcase-card ${isPassed ? 'passed' : 'failed'}`}>
                                    <div className="tc-header">
                                      <span className="tc-num">Test Case #{idx + 1}</span>
                                      <div className="tc-meta">
                                        <span className={`tc-badge ${isPassed ? 'passed' : 'failed'}`}>
                                          {isPassed ? '✓ PASSED' : '✗ FAILED'}
                                        </span>
                                        {tc.executionTimeMs != null && (
                                          <span className="tc-time">({tc.executionTimeMs} ms)</span>
                                        )}
                                      </div>
                                    </div>
                                    <div className="tc-body">
                                      {tc.input && (
                                        <div className="tc-field">
                                          <span className="field-lbl">Input:</span>
                                          <pre className="code-block input-code">{safeFormatJson(tc.input)}</pre>
                                        </div>
                                      )}
                                      <div className="tc-field">
                                        <span className="field-lbl">Expected Output:</span>
                                        <pre className="code-block expected-code">{safeFormatJson(tc.expectedOutput)}</pre>
                                      </div>
                                      <div className="tc-field">
                                        <span className="field-lbl">Actual Output:</span>
                                        <pre className={`code-block ${isPassed ? 'actual-passed' : 'actual-failed'}`}>
                                          {safeFormatJson(tc.actualOutput || 'N/A')}
                                        </pre>
                                      </div>
                                      {(tc.stderr || tc.errorMessage || tc.compileOutput) && (
                                        <div className="tc-field tc-error">
                                          <span className="field-lbl text-danger">Error / Stderr:</span>
                                          <pre className="error-content">{tc.stderr || tc.errorMessage || tc.compileOutput}</pre>
                                        </div>
                                      )}
                                    </div>
                                  </div>
                                );
                              })}
                            </div>
                          </div>
                        )}
                      </div>
                    )}
                  </div>
                )}

                {/* TAB 2: SAMPLE TEST CASES PREVIEW */}
                {activeRightTab === 'samples' && (
                  <div className="samples-view">
                    {currentQuestion?.sampleTestCases?.length ? (
                      currentQuestion.sampleTestCases.map((tc, idx) => (
                        <div key={idx} className="sample-tc-card">
                          <div className="sample-card-header">
                            <h5>Sample Case #{idx + 1}</h5>
                          </div>
                          <div className="sample-card-body">
                            {tc.inputData && (
                              <div className="tc-field">
                                <span className="field-lbl">Input:</span>
                                <pre className="code-block input-code">{safeFormatJson(tc.inputData)}</pre>
                              </div>
                            )}
                            <div className="tc-field">
                              <span className="field-lbl">Expected Output:</span>
                              <pre className="code-block expected-code">{safeFormatJson(tc.expectedOutput)}</pre>
                            </div>
                          </div>
                        </div>
                      ))
                    ) : (
                      <div className="empty-console-card">Không có sample test cases cho bài tập này.</div>
                    )}
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>

        {/* CONFIRM FINISH CODING SESSION DIALOG */}
        <EndSessionConfirmDialog
          isOpen={isEndConfirmOpen}
          isSubmitting={isCompleting}
          canComplete={canComplete}
          onClose={() => setIsEndConfirmOpen(false)}
          onConfirm={handleCompleteRound}
        />

        {/* EVALUATING ANALYSIS MODAL */}
        <EvaluatingAnalysisModal isOpen={isCompleting} />
      </div>
    </UserLayout>
  );
};

export default CodingInterviewPage;
