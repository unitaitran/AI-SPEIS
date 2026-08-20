import React, { useState } from 'react';
import { Bot, CheckCircle2, Code2, FileText, Mic, Play, Sparkles, Terminal, Zap } from 'lucide-react';

function InteractiveSimulator({ t }) {
  const [activeTab, setActiveTab] = useState('cv'); // 'cv' | 'voice' | 'coding'
  const [isSimulatingVoice, setIsSimulatingVoice] = useState(true);

  return (
    <section className="home-section home-simulator-section" id="demo">
      <div className="home-section-shell">
        <div className="home-section-heading text-center mx-auto">
          <span className="home-kicker">
            <Sparkles size={14} className="mr-1" />
            {t('simulator.badge', 'INTERACTIVE SIMULATOR')}
          </span>
          <h2>{t('simulator.title', 'Trải nghiệm trước tính năng của AI-SPEIS')}</h2>
          <p>{t('simulator.subtitle', 'Bấm chọn các tab dưới đây để khám phá giao diện phòng luyện phỏng vấn thông minh.')}</p>
        </div>

        {/* SIMULATOR CONTAINER */}
        <div className="simulator-window">
          {/* WINDOW HEADER */}
          <div className="simulator-window__header">
            <div className="simulator-dots">
              <span className="dot dot-red" />
              <span className="dot dot-yellow" />
              <span className="dot dot-green" />
            </div>
            <div className="simulator-tabs">
              <button
                className={`simulator-tab ${activeTab === 'cv' ? 'is-active' : ''}`}
                onClick={() => setActiveTab('cv')}
              >
                <FileText size={15} />
                <span>{t('simulator.tabs.cv', '1. Cá nhân hóa CV & JD')}</span>
              </button>
              <button
                className={`simulator-tab ${activeTab === 'voice' ? 'is-active' : ''}`}
                onClick={() => setActiveTab('voice')}
              >
                <Mic size={15} />
                <span>{t('simulator.tabs.voice', '2. Phỏng vấn Voice AI')}</span>
              </button>
              <button
                className={`simulator-tab ${activeTab === 'coding' ? 'is-active' : ''}`}
                onClick={() => setActiveTab('coding')}
              >
                <Code2 size={15} />
                <span>{t('simulator.tabs.coding', '3. Coding Sandbox')}</span>
              </button>
            </div>
            <div className="simulator-badge-live">
              <span className="live-pulse" />
              <span>LIVE DEMO</span>
            </div>
          </div>

          {/* WINDOW BODY */}
          <div className="simulator-window__body">
            {/* TAB 1: CV & JD PARSER DEMO */}
            {activeTab === 'cv' && (
              <div className="simulator-content cv-demo-panel">
                <div className="cv-demo-grid">
                  <div className="cv-card-box">
                    <div className="cv-card-header">
                      <FileText className="text-primary-dark" size={20} />
                      <div>
                        <h4>Senior_Frontend_Developer_Resume.pdf</h4>
                        <span className="text-xs text-secondary">Uploaded • 1.2 MB</span>
                      </div>
                      <span className="status-badge-success ml-auto">✓ Parsed</span>
                    </div>
                    <div className="cv-skills-tags">
                      <span className="skill-chip">React.js</span>
                      <span className="skill-chip">TypeScript</span>
                      <span className="skill-chip">TailwindCSS</span>
                      <span className="skill-chip">REST API</span>
                      <span className="skill-chip">Jest / RTL</span>
                    </div>
                  </div>

                  <div className="cv-analysis-box">
                    <div className="match-score-bar">
                      <div className="flex justify-between items-center mb-2">
                        <span className="font-bold text-sm">Độ tương thích CV & JD target (Frontend Engineer)</span>
                        <span className="font-extrabold text-primary-dark text-base">92% Match</span>
                      </div>
                      <div className="progress-track">
                        <div className="progress-fill" style={{ width: '92%' }} />
                      </div>
                    </div>

                    <div className="analysis-insights">
                      <div className="insight-item insight-strength">
                        <CheckCircle2 size={16} />
                        <span><strong>Điểm mạnh nổi bật:</strong> Thành thạo React Core Hooks, TypeScript Typings và UI Component Architecture.</span>
                      </div>
                      <div className="insight-item insight-gap">
                        <Zap size={16} />
                        <span><strong>Gợi ý tập trung luyện tập:</strong> Xử lý tối ưu hóa Re-render (useMemo, useCallback) & Micro-frontends.</span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* TAB 2: VOICE AI INTERVIEW DEMO */}
            {activeTab === 'voice' && (
              <div className="simulator-content voice-demo-panel">
                <div className="voice-studio-grid">
                  <div className="ai-interviewer-card">
                    <div className="avatar-glow-ring">
                      <Bot size={36} className="text-primary-dark" />
                    </div>
                    <div className="speech-bubble-ai">
                      <p>
                        "Bạn có thể giải thích cách bạn áp dụng <code>useMemo</code> và <code>useCallback</code> để tối ưu hóa hiệu năng khi ứng dụng React có danh sách lớn hàng ngàn dữ liệu không?"
                      </p>
                    </div>
                  </div>

                  <div className="candidate-voice-card">
                    <div className="voice-control-bar">
                      <button
                        className={`mic-btn ${isSimulatingVoice ? 'is-recording' : ''}`}
                        onClick={() => setIsSimulatingVoice(!isSimulatingVoice)}
                      >
                        <Mic size={20} />
                      </button>
                      <div className="audio-wave-container">
                        <span className={`wave-bar ${isSimulatingVoice ? 'anim-1' : ''}`} />
                        <span className={`wave-bar ${isSimulatingVoice ? 'anim-2' : ''}`} />
                        <span className={`wave-bar ${isSimulatingVoice ? 'anim-3' : ''}`} />
                        <span className={`wave-bar ${isSimulatingVoice ? 'anim-4' : ''}`} />
                        <span className={`wave-bar ${isSimulatingVoice ? 'anim-5' : ''}`} />
                        <span className={`wave-bar ${isSimulatingVoice ? 'anim-2' : ''}`} />
                        <span className={`wave-bar ${isSimulatingVoice ? 'anim-1' : ''}`} />
                      </div>
                      <span className="recording-status-text">
                        {isSimulatingVoice ? 'Đang ghi âm câu trả lời...' : 'Nhấn vào Mic để nói'}
                      </span>
                    </div>

                    <div className="live-transcription">
                      <span className="text-xs uppercase font-bold text-secondary">Real-time Speech-To-Text Stream:</span>
                      <p className="transcription-text">
                        "Em sử dụng <code>useMemo</code> để meomoize kết quả tính toán mảng dữ liệu phức tạp, và <code>useCallback</code> để giữ reference hàm callback truyền xuống component con..."
                      </p>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* TAB 3: CODING SANDBOX DEMO */}
            {activeTab === 'coding' && (
              <div className="simulator-content coding-demo-panel">
                <div className="code-editor-header">
                  <span className="code-lang-tag">JavaScript (Node.js 18)</span>
                  <span className="code-fn-tag">function twoSum(nums, target)</span>
                  <button className="btn-run-demo">
                    <Play size={14} />
                    <span>Run Judge0 Test</span>
                  </button>
                </div>
                <div className="code-editor-mock">
                  <pre className="code-content">
{`function twoSum(nums, target) {
  const map = new Map();
  for (let i = 0; i < nums.length; i++) {
    const diff = target - nums[i];
    if (map.has(diff)) {
      return [map.get(diff), i];
    }
    map.set(nums[i], i);
  }
  return [];
}`}
                  </pre>
                </div>
                <div className="code-console-mock">
                  <div className="console-title">
                    <Terminal size={14} />
                    <span>Judge0 Sandbox Output:</span>
                  </div>
                  <div className="console-result accepted">
                    <span className="badge-pass">PASSED</span>
                    <span>Test Case 1: [2, 7, 11, 15], target = 9 ➔ [0, 1] (Time: 18ms, Memory: 41.2 MB)</span>
                  </div>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </section>
  );
}

export default InteractiveSimulator;
