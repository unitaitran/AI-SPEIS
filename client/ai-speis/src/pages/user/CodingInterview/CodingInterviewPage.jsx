import React, { useState, useEffect, useRef } from 'react';
import Editor from '@monaco-editor/react';
import { codingService } from '../../../services/codingService';
import notify from '../../../utils/notification';
import '../../../styles/user/CodingInterviewPage.css';

const CodingInterviewPage = ({ sessionId }) => {
  const [questions, setQuestions] = useState([]);
  const [currentQuestionIndex, setCurrentQuestionIndex] = useState(0);
  const [languages, setLanguages] = useState([]);
  const [selectedLanguage, setSelectedLanguage] = useState(null);
  const [code, setCode] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submissionResult, setSubmissionResult] = useState(null);
  
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

  // Update starter code when question or language changes
  useEffect(() => {
    if (currentQuestion && selectedLanguage) {
      const template = currentQuestion.templates?.find(t => t.languageId === selectedLanguage.id);
      if (template) {
        setCode(template.templateCode);
      } else {
        setCode(`// Please write your ${selectedLanguage.name} code here\n`);
      }
    }
  }, [currentQuestion, selectedLanguage]);

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
        sessionId: parseInt(sessionId, 10),
        questionId: currentQuestion.id,
        languageId: selectedLanguage.id,
        sourceCode: code
      };
      const res = await codingService.submitCode(payload);
      setSubmissionResult(res);
      notify.success('Code submitted successfully');
    } catch (err) {
      notify.error(err.message || 'Failed to submit code');
    } finally {
      setIsSubmitting(false);
    }
  };

  if (!questions.length) {
    return <div className="coding-interview-loading">Loading interview...</div>;
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
        <h2>Coding Interview</h2>
        <div className="coding-header-actions">
          {questions.length > 1 && (
            <div className="question-nav">
              <button 
                disabled={currentQuestionIndex === 0}
                onClick={() => setCurrentQuestionIndex(prev => prev - 1)}
              >
                Prev Question
              </button>
              <span>{currentQuestionIndex + 1} / {questions.length}</span>
              <button 
                disabled={currentQuestionIndex === questions.length - 1}
                onClick={() => setCurrentQuestionIndex(prev => prev + 1)}
              >
                Next Question
              </button>
            </div>
          )}
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
              <h4>Problem Statement</h4>
              <p>{currentQuestion.description}</p>
              
              {currentQuestion.inputDescription && (
                <>
                  <h4>Input Description</h4>
                  <p>{currentQuestion.inputDescription}</p>
                </>
              )}
              
              {currentQuestion.outputDescription && (
                <>
                  <h4>Output Description</h4>
                  <p>{currentQuestion.outputDescription}</p>
                </>
              )}

              {currentQuestion.constraints && (
                <>
                  <h4>Constraints</h4>
                  <pre>{currentQuestion.constraints}</pre>
                </>
              )}

              {currentQuestion.examples && (
                <>
                  <h4>Examples</h4>
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
              {isSubmitting ? 'Running...' : 'Run & Submit'}
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
              <h4>Submission Results</h4>
              <div className="result-stats">
                <span className={submissionResult.status?.description === 'Accepted' ? 'status-accepted' : 'status-error'}>
                  {submissionResult.status?.description || 'Unknown'}
                </span>
                <span>Time: {submissionResult.time}s</span>
                <span>Memory: {submissionResult.memory}KB</span>
              </div>
              
              <div className="test-cases-results">
                {submissionResult.testCaseResults && submissionResult.testCaseResults.map((tc, idx) => (
                  <div key={idx} className={`test-case-card ${tc.passed ? 'passed' : 'failed'}`}>
                    <h5>Test Case {idx + 1} {tc.passed ? '✅' : '❌'}</h5>
                    {tc.error ? (
                      <div className="error-output">
                        <strong>Error:</strong> <pre>{tc.error}</pre>
                      </div>
                    ) : (
                      <div className="execution-details">
                        <p><strong>Time:</strong> {tc.time}s</p>
                        <p><strong>Memory:</strong> {tc.memory}KB</p>
                      </div>
                    )}
                  </div>
                ))}
              </div>
              
              {submissionResult.compileOutput && (
                <div className="compile-output">
                  <h5>Compilation Output</h5>
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
