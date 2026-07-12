import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  AlertCircle,
  ArrowLeft,
  ArrowRight,
  BriefcaseBusiness,
  Check,
  FileText,
  Info,
  Loader2,
  Settings2,
} from 'lucide-react';
import InterviewProgressStepper from '../../components/user/InterviewProgressStepper/InterviewProgressStepper';
import UserLayout from '../../layouts/user/UserLayout';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';
import cvService from '../../services/CVService';
import jdService from '../../services/JDService';
import interviewSessionService from '../../services/InterviewSessionService';
import { getInterviewSetupDraft, saveActiveInterviewContext } from '../../utils/interviewContext';
import '../../styles/user/InterviewSetupPage.css';

const FILE_STATUS_BY_VALUE = {
  0: 'Pending',
  1: 'Processing',
  2: 'ConfirmationRequired',
  3: 'Confirmed',
  4: 'Failed',
  5: 'AnalysisFailed',
  6: 'Archived',
};

const CV_READY_STATUSES = new Set(['Confirmed']);
const JD_READY_STATUSES = new Set(['ConfirmationRequired', 'Confirmed']);
const DURATION_OPTIONS = [10, 15, 20];

const normalizeStatus = (status) => (
  typeof status === 'number' ? FILE_STATUS_BY_VALUE[status] || 'Unknown' : status
);

const unwrapJdData = (response) => response?.data || response;

const formatRoundName = (round) => {
  switch (round) {
    case 'Behavior':
      return 'Behavioral';
    case 'Technical':
      return 'Technical';
    case 'Code':
      return 'Coding';
    default:
      return round;
  }
};

const formatInterviewType = (rounds) => {
  if (!rounds?.length) return 'Chưa xác định';
  return rounds.map(formatRoundName).join(' + ');
};

function InterviewSetupPage() {
  const [activeCv, setActiveCv] = useState(null);
  const [jdOptions, setJdOptions] = useState([]);
  const [selectedJdId, setSelectedJdId] = useState('');
  const [language, setLanguage] = useState('en');
  const [durationMinutes, setDurationMinutes] = useState(10);
  const [mode] = useState(() => getInterviewSetupDraft()?.mode || '');
  const [includeCoding, setIncludeCoding] = useState(false);
  const [practiceRounds, setPracticeRounds] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [loadError, setLoadError] = useState('');
  const [submitError, setSubmitError] = useState('');
  const hasValidMode = mode === 'Practice' || mode === 'RealTest';

  const loadSetupData = useCallback(async () => {
    setIsLoading(true);
    setLoadError('');
    setSubmitError('');

    try {
      const [cvHistory, jdHistory] = await Promise.all([
        cvService.getMyCVHistory(1, 20),
        jdService.getMyJDHistory(1, 20),
      ]);

      const readyCv = (cvHistory?.items || []).find((cv) => (
        CV_READY_STATUSES.has(normalizeStatus(cv.status))
      ));
      const readyJds = (jdHistory?.items || []).filter((jd) => (
        JD_READY_STATUSES.has(normalizeStatus(jd.status))
      ));

      setActiveCv(readyCv || null);

      const resolvedJds = await Promise.all(readyJds.map(async (jd) => {
        try {
          const [parsedResponse, availableTypes] = await Promise.all([
            jdService.getParsedData(jd.jdFileId),
            interviewSessionService.getAvailableTypes(jd.jdFileId),
          ]);

          return {
            file: jd,
            parsed: unwrapJdData(parsedResponse),
            availableTypes,
          };
        } catch {
          return null;
        }
      }));

      const availableJds = resolvedJds.filter(Boolean);

      if (readyJds.length > 0 && availableJds.length === 0) {
        throw new Error('Không thể tải dữ liệu phân tích hoặc cấu hình vòng phỏng vấn của Job Description.');
      }

      setJdOptions(availableJds);
      setSelectedJdId((currentId) => {
        const stillAvailable = availableJds.some(({ file }) => String(file.jdFileId) === currentId);
        return stillAvailable ? currentId : String(availableJds[0]?.file.jdFileId || '');
      });
    } catch (error) {
      setActiveCv(null);
      setJdOptions([]);
      setSelectedJdId('');
      setLoadError(error.message || 'Không thể tải dữ liệu thiết lập phỏng vấn.');
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!hasValidMode) {
      navigate(USER_ROUTES.INTERVIEW_MODE, { replace: true });
      return;
    }

    loadSetupData();
  }, [hasValidMode, loadSetupData]);

  const selectedJd = useMemo(() => (
    jdOptions.find(({ file }) => String(file.jdFileId) === selectedJdId) || null
  ), [jdOptions, selectedJdId]);

  const baseRounds = selectedJd?.availableTypes?.availableRounds || [];
  const hasOptionalCoding = Boolean(selectedJd?.availableTypes?.hasOptionalCoding);
  const selectablePracticeRounds = useMemo(() => {
    if (!hasOptionalCoding || baseRounds.includes('Code')) return baseRounds;
    return [...baseRounds, 'Code'];
  }, [baseRounds, hasOptionalCoding]);
  const selectablePracticeRoundsKey = selectablePracticeRounds.join('|');

  useEffect(() => {
    setIncludeCoding(false);
    setPracticeRounds(selectablePracticeRounds);
  }, [selectedJdId, selectablePracticeRoundsKey]);

  const realTestRounds = useMemo(() => {
    if (!includeCoding || baseRounds.includes('Code')) return baseRounds;
    return [...baseRounds, 'Code'];
  }, [baseRounds, includeCoding]);

  const configuredRounds = mode === 'Practice' ? practiceRounds : realTestRounds;

  const jobTitle = selectedJd?.parsed?.jobTitle
    || selectedJd?.availableTypes?.roleTarget
    || selectedJd?.file?.fileName
    || 'Chưa xác định';
  const experienceLevel = selectedJd?.parsed?.experienceLevel || 'Chưa xác định';
  const difficulty = selectedJd?.availableTypes?.difficulty || 'Chưa xác định';
  const interviewType = formatInterviewType(configuredRounds);
  const languageLabel = language === 'en' ? 'Tiếng Anh' : 'Tiếng Việt';
  const modeLabel = mode === 'RealTest' ? 'Thực chiến' : 'Luyện tập';
  const hasRequiredSources = Boolean(activeCv && selectedJd);
  const hasValidRoundSelection = mode !== 'Practice' || practiceRounds.length > 0;
  const canSubmit = hasRequiredSources
    && language
    && mode
    && durationMinutes
    && hasValidRoundSelection
    && !isSubmitting;

  const availabilityMessage = !activeCv
    ? 'Bạn cần một CV đã phân tích và xác nhận để tạo buổi phỏng vấn.'
    : jdOptions.length === 0
      ? 'Bạn cần một Job Description đã phân tích để cấu hình buổi phỏng vấn.'
      : '';

  const handleSubmit = async (event) => {
    event.preventDefault();
    setSubmitError('');

    if (!activeCv || !selectedJd) {
      setSubmitError('Vui lòng chuẩn bị CV và Job Description hợp lệ trước khi tiếp tục.');
      return;
    }

    if (!language || !mode || !durationMinutes) {
      setSubmitError('Vui lòng hoàn tất các trường bắt buộc.');
      return;
    }

    if (mode === 'Practice' && practiceRounds.length === 0) {
      setSubmitError('Chế độ Luyện tập yêu cầu chọn ít nhất một vòng phỏng vấn.');
      return;
    }

    setIsSubmitting(true);

    try {
      const campaign = await interviewSessionService.createSession({
        CVFileId: activeCv.cvFileId,
        JDFileId: selectedJd.file.jdFileId,
        IncludeCoding: includeCoding,
        SelectedRounds: mode === 'Practice' ? practiceRounds : [],
        Language: language,
        Mode: mode,
        DurationMinutes: durationMinutes,
      });
      saveActiveInterviewContext({ campaign, activeSessionId: null });
      navigate(USER_ROUTES.DEVICE_CHECK);
    } catch (error) {
      setSubmitError(error.message || 'Không thể tạo buổi phỏng vấn. Vui lòng thử lại.');
    } finally {
      setIsSubmitting(false);
    }
  };

  const togglePracticeRound = (round) => {
    setPracticeRounds((currentRounds) => (
      currentRounds.includes(round)
        ? currentRounds.filter((currentRound) => currentRound !== round)
        : [...currentRounds, round]
    ));
  };

  const handleBack = () => {
    navigate(USER_ROUTES.INTERVIEW_MODE);
  };

  if (!hasValidMode) return null;

  return (
    <UserLayout>
      <div className="setup-page animate-pageEntrance">
        <header className="setup-page-header">
          <div>
            <span className="setup-eyebrow">AI Mock Interview</span>
            <h1>Thiết lập buổi phỏng vấn</h1>
            <p>Kiểm tra thông tin và chọn cấu hình phù hợp trước khi bắt đầu.</p>
          </div>
        </header>

        <InterviewProgressStepper activeStep={1} />

        {isLoading ? (
          <section className="setup-loading-card" aria-live="polite">
            <Loader2 size={28} className="setup-spin" />
            <div>
              <h2>Đang tải cấu hình</h2>
              <p>AI-SPEIS đang đọc CV, Job Description và các vòng phỏng vấn khả dụng.</p>
            </div>
          </section>
        ) : loadError ? (
          <section className="setup-state-card setup-state-card--error" role="alert">
            <AlertCircle size={24} />
            <div>
              <h2>Không thể tải dữ liệu thiết lập</h2>
              <p>{loadError}</p>
              <button type="button" onClick={loadSetupData}>Thử lại</button>
            </div>
          </section>
        ) : (
          <div className="setup-layout">
            <form id="interview-setup-form" className="setup-form-column" onSubmit={handleSubmit} noValidate>
              {availabilityMessage && (
                <div className="setup-inline-alert setup-inline-alert--warning" role="alert">
                  <AlertCircle size={20} />
                  <div>
                    <strong>Chưa đủ dữ liệu đầu vào</strong>
                    <span>{availabilityMessage}</span>
                  </div>
                  <button type="button" onClick={() => navigate(USER_ROUTES.CV)}>
                    Quản lý CV/JD
                  </button>
                </div>
              )}

              <section className="setup-card" aria-labelledby="basic-information-title">
                <div className="setup-section-heading">
                  <span className="setup-section-icon" aria-hidden="true">
                    <BriefcaseBusiness size={22} />
                  </span>
                  <div>
                    <h2 id="basic-information-title">Thông tin cơ bản</h2>
                    <p>Dữ liệu vị trí được lấy từ Job Description đã phân tích.</p>
                  </div>
                </div>

                <div className="setup-source-strip" aria-label="Hồ sơ sử dụng">
                  <span className="setup-source-item">
                    <FileText size={16} />
                    <span className="setup-source-content">
                      <strong>CV:</strong>
                      <span className="setup-source-name">{activeCv?.fileName || 'Chưa có CV phù hợp'}</span>
                    </span>
                  </span>
                  <span className="setup-source-item">
                    <BriefcaseBusiness size={16} />
                    <span className="setup-source-content">
                      <strong>JD:</strong>
                      <span className="setup-source-name">{selectedJd?.file?.fileName || 'Chưa có JD phù hợp'}</span>
                    </span>
                  </span>
                </div>

                <div className="setup-fields-grid">
                  <div className="setup-field">
                    <label htmlFor="setup-job-position">Vị trí ứng tuyển</label>
                    <div className="setup-control-wrap">
                      <select
                        id="setup-job-position"
                        value={selectedJdId}
                        onChange={(event) => setSelectedJdId(event.target.value)}
                        required
                        disabled={jdOptions.length === 0}
                        aria-invalid={jdOptions.length === 0}
                        aria-describedby={jdOptions.length === 0 ? 'setup-job-position-error' : undefined}
                      >
                        {jdOptions.length === 0 && <option value="">Chưa có vị trí phù hợp</option>}
                        {jdOptions.map(({ file, parsed, availableTypes }) => (
                          <option key={file.jdFileId} value={file.jdFileId}>
                            {parsed?.jobTitle || availableTypes?.roleTarget || file.fileName}
                          </option>
                        ))}
                      </select>
                    </div>
                    {jdOptions.length === 0 && (
                      <span id="setup-job-position-error" className="setup-field-error">
                        Hãy phân tích và xác nhận một Job Description trước.
                      </span>
                    )}
                  </div>

                  <div className="setup-field">
                    <label htmlFor="setup-experience-level">Cấp độ</label>
                    <input
                      id="setup-experience-level"
                      type="text"
                      value={experienceLevel}
                      readOnly
                      aria-readonly="true"
                    />
                    <span className="setup-field-meta">Độ khó hệ thống: <strong>{difficulty}</strong></span>
                  </div>

                  <div className="setup-field">
                    <label htmlFor="setup-interview-type">Loại phỏng vấn</label>
                    {mode === 'Practice' ? (
                      <input
                        id="setup-interview-type"
                        type="text"
                        value={formatInterviewType(practiceRounds)}
                        readOnly
                        aria-readonly="true"
                      />
                    ) : (
                      <div className="setup-control-wrap">
                        <select
                          id="setup-interview-type"
                          value={includeCoding ? 'with-coding' : 'standard'}
                          onChange={(event) => setIncludeCoding(event.target.value === 'with-coding')}
                          disabled={!selectedJd}
                        >
                          <option value="standard">{formatInterviewType(baseRounds)}</option>
                          {hasOptionalCoding && (
                            <option value="with-coding">
                              {formatInterviewType([...baseRounds, 'Code'])}
                            </option>
                          )}
                        </select>
                      </div>
                    )}
                  </div>

                  <div className="setup-field">
                    <label htmlFor="setup-language">Ngôn ngữ</label>
                    <div className="setup-control-wrap">
                      <select
                        id="setup-language"
                        value={language}
                        onChange={(event) => setLanguage(event.target.value)}
                        required
                      >
                        <option value="en">Tiếng Anh</option>
                        <option value="vi">Tiếng Việt</option>
                      </select>
                    </div>
                  </div>
                </div>

                <fieldset className="setup-duration-group">
                  <legend>Thời lượng phỏng vấn</legend>
                  <div className="setup-duration-options">
                    {DURATION_OPTIONS.map((duration) => (
                      <label
                        key={duration}
                        className={`setup-duration-option${durationMinutes === duration ? ' setup-duration-option--selected' : ''}`}
                      >
                        <input
                          type="radio"
                          name="duration"
                          value={duration}
                          checked={durationMinutes === duration}
                          onChange={() => setDurationMinutes(duration)}
                        />
                        <span>{duration} phút</span>
                      </label>
                    ))}
                  </div>
                  <p>Thời lượng được lưu cùng campaign và truyền sang phòng phỏng vấn.</p>
                </fieldset>
              </section>

              {mode === 'Practice' && (
                <section className="setup-card" aria-labelledby="practice-rounds-title">
                  <div className="setup-section-heading">
                    <span className="setup-section-icon" aria-hidden="true">
                      <Settings2 size={22} />
                    </span>
                    <div>
                      <h2 id="practice-rounds-title">Vòng luyện tập</h2>
                      <p>Chọn riêng từng vòng muốn luyện trong campaign này.</p>
                    </div>
                  </div>

                  <fieldset
                    className="setup-round-selector"
                    aria-describedby={practiceRounds.length === 0 ? 'setup-round-help setup-round-error' : 'setup-round-help'}
                  >
                    <legend>Chọn vòng muốn luyện tập</legend>
                    <p id="setup-round-help">Bạn có thể luyện một vòng riêng lẻ hoặc kết hợp nhiều vòng.</p>
                    <div className="setup-round-options">
                      {selectablePracticeRounds.map((round) => {
                        const isSelected = practiceRounds.includes(round);
                        return (
                          <label
                            key={round}
                            className={`setup-round-option${isSelected ? ' setup-round-option--selected' : ''}`}
                          >
                            <input
                              type="checkbox"
                              checked={isSelected}
                              onChange={() => togglePracticeRound(round)}
                            />
                            <span className="setup-round-check" aria-hidden="true">
                              {isSelected && <Check size={14} />}
                            </span>
                            <span>{formatRoundName(round)}</span>
                          </label>
                        );
                      })}
                    </div>
                    {practiceRounds.length === 0 && (
                      <span id="setup-round-error" className="setup-field-error" role="alert">
                        Chọn ít nhất một vòng phỏng vấn để tiếp tục.
                      </span>
                    )}
                  </fieldset>
                </section>
              )}
            </form>

            <aside className="setup-summary-card" aria-labelledby="setup-summary-title">
              <h2 id="setup-summary-title">Tóm tắt cấu hình</h2>

              <dl className="setup-summary-list">
                <div>
                  <dt>Vị trí</dt>
                  <dd>{jobTitle}</dd>
                </div>
                <div>
                  <dt>Cấp độ</dt>
                  <dd>{experienceLevel}</dd>
                </div>
                <div>
                  <dt>Độ khó</dt>
                  <dd>{difficulty}</dd>
                </div>
                <div>
                  <dt>Loại</dt>
                  <dd>{interviewType}</dd>
                </div>
                <div>
                  <dt>Ngôn ngữ</dt>
                  <dd>{languageLabel}</dd>
                </div>
                <div>
                  <dt>Thời gian</dt>
                  <dd>{durationMinutes} phút</dd>
                </div>
                <div>
                  <dt>Chế độ</dt>
                  <dd>{modeLabel}</dd>
                </div>
              </dl>

              <div className="setup-note">
                <Info size={20} />
                <div>
                  <strong>Lưu ý</strong>
                  <p>Hệ thống sẽ hỏi làm rõ, hỏi tiếp hoặc chuyển câu dựa trên phần đánh giá câu trả lời trước đó.</p>
                </div>
              </div>

              {submitError && (
                <div className="setup-submit-error" role="alert">
                  <AlertCircle size={18} />
                  <span>{submitError}</span>
                </div>
              )}

              <div className="setup-summary-actions">
                <button
                  type="submit"
                  form="interview-setup-form"
                  className="setup-primary-button"
                  disabled={!canSubmit}
                  aria-busy={isSubmitting}
                >
                  {isSubmitting ? (
                    <>
                      <Loader2 size={20} className="setup-spin" />
                      Đang tạo buổi phỏng vấn
                    </>
                  ) : (
                    <>
                      Tiếp tục
                      <ArrowRight size={20} />
                    </>
                  )}
                </button>
                <button type="button" className="setup-secondary-button" onClick={handleBack} disabled={isSubmitting}>
                  <ArrowLeft size={18} />
                  Quay lại
                </button>
              </div>
            </aside>
          </div>
        )}
      </div>
    </UserLayout>
  );
}

export default InterviewSetupPage;
