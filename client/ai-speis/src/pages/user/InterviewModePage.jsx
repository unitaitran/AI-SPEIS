import React, { useState } from 'react';
import { ArrowLeft, ArrowRight, Check, Info, Lightbulb, ShieldCheck } from 'lucide-react';
import InterviewProgressStepper from '../../components/user/InterviewProgressStepper/InterviewProgressStepper';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';
import { getInterviewSetupDraft, saveInterviewSetupDraft } from '../../utils/interviewContext';
import '../../styles/user/InterviewModePage.css';

const VALID_MODES = new Set(['Practice', 'RealTest']);

function InterviewModePage() {
  const storedMode = getInterviewSetupDraft()?.mode;
  const [mode, setMode] = useState(VALID_MODES.has(storedMode) ? storedMode : '');
  const [error, setError] = useState('');

  const selectMode = (nextMode) => {
    setMode(nextMode);
    setError('');
  };

  const handleContinue = () => {
    if (!VALID_MODES.has(mode)) {
      setError('Vui lòng chọn một chế độ phỏng vấn trước khi tiếp tục.');
      return;
    }

    const currentDraft = getInterviewSetupDraft() || {};
    saveInterviewSetupDraft({ ...currentDraft, mode });
    navigate(USER_ROUTES.INTERVIEW_SETUP);
  };

  return (
    <UserLayout>
      <div className="interview-mode-page animate-pageEntrance">
        <header className="interview-mode-header">
          <span>AI Mock Interview</span>
          <h1>Chọn chế độ phỏng vấn</h1>
          <p>Chế độ đã chọn sẽ quyết định mức hỗ trợ và cách cấu hình các vòng ở bước tiếp theo.</p>
        </header>

        <InterviewProgressStepper activeStep={0} />

        <section className="interview-mode-panel" aria-labelledby="interview-mode-title">
          <div className="interview-mode-panel-heading">
            <h2 id="interview-mode-title">Bạn muốn luyện tập theo cách nào?</h2>
            <p>Chỉ có thể chọn một chế độ cho mỗi campaign phỏng vấn.</p>
          </div>

          <fieldset className="interview-mode-options">
            <legend className="interview-mode-sr-only">Chọn chế độ phỏng vấn</legend>

            <label className={`interview-mode-card${mode === 'Practice' ? ' interview-mode-card--selected' : ''}`}>
              <input
                type="radio"
                name="interview-mode"
                value="Practice"
                checked={mode === 'Practice'}
                onChange={() => selectMode('Practice')}
              />
              <span className="interview-mode-card-icon" aria-hidden="true"><Lightbulb size={24} /></span>
              <span className="interview-mode-radio" aria-hidden="true">{mode === 'Practice' && <Check size={14} />}</span>
              <strong>Luyện tập</strong>
              <p>Có gợi ý từ AI, linh hoạt hơn và được chọn từng vòng Behavioral, Technical hoặc Coding ở bước Thiết lập.</p>
            </label>

            <label className={`interview-mode-card${mode === 'RealTest' ? ' interview-mode-card--selected' : ''}`}>
              <input
                type="radio"
                name="interview-mode"
                value="RealTest"
                checked={mode === 'RealTest'}
                onChange={() => selectMode('RealTest')}
              />
              <span className="interview-mode-card-icon" aria-hidden="true"><ShieldCheck size={24} /></span>
              <span className="interview-mode-radio" aria-hidden="true">{mode === 'RealTest' && <Check size={14} />}</span>
              <strong>Thực chiến</strong>
              <p>Không gợi ý, dùng các vòng mặc định theo vị trí và đánh giá như một buổi phỏng vấn thật.</p>
            </label>
          </fieldset>

          <div className="interview-mode-note">
            <Info size={20} />
            <p>Bạn có thể quay lại bước này từ màn Thiết lập trước khi campaign được tạo.</p>
          </div>

          {error && <div className="interview-mode-error" role="alert">{error}</div>}

          <div className="interview-mode-actions">
            <button type="button" className="interview-mode-secondary" onClick={() => navigate(USER_ROUTES.DASHBOARD)}>
              <ArrowLeft size={18} />
              Quay lại
            </button>
            <button type="button" className="interview-mode-primary" onClick={handleContinue}>
              Tiếp tục
              <ArrowRight size={20} />
            </button>
          </div>
        </section>
      </div>
    </UserLayout>
  );
}

export default InterviewModePage;
