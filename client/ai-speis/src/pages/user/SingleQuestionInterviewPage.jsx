import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  AlertCircle,
  ArrowLeft,
  ArrowRight,
  Bot,
  CheckCircle2,
  FileText,
  Loader2,
  Pause,
  RotateCcw,
  Sparkles,
  ThumbsDown,
  ThumbsUp,
  Volume2,
} from 'lucide-react';
import InterviewRoomShell from '../../components/interviewRoom/InterviewRoomShell';
import InterviewRoomTranscriptPanel from '../../components/interviewRoom/InterviewRoomTranscriptPanel';
import EvaluatingAnalysisModal from '../../components/interviewRoom/EvaluatingAnalysisModal';
import BehavioralRecorderControls from '../../components/behavioralInterview/BehavioralRecorderControls';
import useQuestionAudio from '../../features/technicalInterview/useQuestionAudio';
import useTechnicalRecorder from '../../features/technicalInterview/useTechnicalRecorder';
import singleQuestionRetryApi from '../../services/singleQuestionRetryApi';
import { navigate } from '../../routes/navigation';
import { USER_ROUTES } from '../../routes/routePaths';
import '../../styles/user/BehavioralInterview.css';
import '../../styles/user/TechnicalInterview.css';
import '../../styles/user/SingleQuestionInterview.css';

const STORAGE_KEY = 'ai-speis:single-question-interview';

export default function SingleQuestionInterviewPage() {
  const [interview, setInterview] = useState(null);
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);
  const [submitting, setSubmitting] = useState(false);
  const [transcriptOpen, setTranscriptOpen] = useState(() => (
    typeof window === 'undefined' || window.matchMedia('(min-width: 1025px)').matches
  ));

  const { t: translate } = useTranslation('interview');
  const interviewLanguage = interview?.language || 'vi';

  const t = useCallback((key, options = {}) => translate(`behavioralRoom.${key}`, {
    ...options,
    lng: interviewLanguage,
    defaultValue: options.defaultValue || key,
  }), [interviewLanguage, translate]);

  useEffect(() => {
    try {
      const value = JSON.parse(sessionStorage.getItem(STORAGE_KEY) || 'null');
      if (value?.questionId && value?.question) {
        setInterview(value);
      } else {
        setError('Không tìm thấy dữ liệu câu hỏi cần thử lại.');
      }
    } catch {
      setError('Không thể mở câu hỏi cần thử lại.');
    }
  }, []);

  const questionObject = useMemo(() => (
    interview ? { questionId: interview.questionId, content: interview.question } : null
  ), [interview]);

  const questionAudio = useQuestionAudio({
    question: questionObject,
    sessionId: interview?.originalSessionId || 999999,
    language: interviewLanguage,
    preferenceKey: 'ai-speis:single-question:auto-play-question',
    forceAutoPlay: true,
  });

  const recorder = useTechnicalRecorder(interviewLanguage);

  const leave = () => {
    recorder.cleanup();
    questionAudio.pause();
    sessionStorage.removeItem(STORAGE_KEY);
    navigate(USER_ROUTES.INTERVIEW_HISTORY);
  };

  const submit = async () => {
    const transcript = recorder.transcript?.trim();
    if (!transcript) {
      setError('Hãy ghi âm hoặc nhập nội dung câu trả lời trước khi gửi.');
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      const evalResult = await singleQuestionRetryApi.retryQuestion({
        questionId: interview.questionId,
        originalSessionId: interview.originalSessionId || null,
        roundType: interview.roundType,
        transcript,
      });
      setResult(evalResult);
    } catch (submitError) {
      setError(submitError.message || 'Không thể đánh giá câu trả lời. Vui lòng thử lại.');
    } finally {
      setSubmitting(false);
    }
  };

  const handleRetryQuestion = () => {
    setResult(null);
    setError(null);
    recorder.reset();
  };

  const transcriptItems = useMemo(() => {
    if (!interview) return [];
    const items = [];
    items.push({
      id: `question-${interview.questionId}`,
      role: 'INTERVIEWER',
      content: interview.question,
      statusLabel: interview.roundType || 'Single Question',
    });
    if (recorder.transcript.trim()) {
      items.push({
        id: 'candidate-response',
        role: 'CANDIDATE',
        content: recorder.transcript,
        statusLabel: t('draft'),
      });
    }
    return items;
  }, [interview, recorder.transcript, t]);

  const liveState = useMemo(() => {
    if (recorder.sttStatus === 'PROCESSING') {
      return { icon: Loader2, label: t('transcribing'), tone: 'processing', spin: true };
    }
    if (submitting) {
      return { icon: Loader2, label: t('submittingAnswer'), tone: 'processing', spin: true };
    }
    return null;
  }, [recorder.sttStatus, submitting, t]);

  if (!interview && error) {
    return (
      <InterviewRoomShell language={interviewLanguage} mainFlush>
        <section className="behavior-stage">
          <div className="behavior-room-state behavior-room-state--error" role="alert">
            <span><AlertCircle size={34} /></span>
            <h1>Không thể mở câu hỏi</h1>
            <p>{error}</p>
            <div>
              <button type="button" onClick={leave}>
                <ArrowLeft size={18} /> Quay lại lịch sử
              </button>
            </div>
          </div>
        </section>
      </InterviewRoomShell>
    );
  }

  return (
    <InterviewRoomShell
      language={interviewLanguage}
      mainFlush
      isTranscriptOpen={transcriptOpen}
      onCloseTranscript={() => setTranscriptOpen(false)}
      transcriptCloseLabel={t('closeTranscript')}
      transcriptLabel={t('transcript')}
      transcript={(
        <InterviewRoomTranscriptPanel
          candidateLabel={t('candidate')}
          closeLabel={t('closeTranscript')}
          description={t('transcriptDescription')}
          emptyMessage={t('transcriptEmpty')}
          interviewerLabel={t('interviewer')}
          isOpen={transcriptOpen}
          items={transcriptItems}
          liveState={liveState}
          onClose={() => setTranscriptOpen(false)}
          title={t('transcriptTitle')}
        />
      )}
      dialog={<EvaluatingAnalysisModal isOpen={submitting} />}
    >
      <section className="behavior-stage" aria-label={t('behavioralInterview')}>
        {result ? (
          /* Single Question Evaluation Result View */
          <div className="behavior-completion-stack" style={{ gridRow: '1 / -1', overflowY: 'auto', maxWidth: '840px', margin: '0 auto', width: '100%', padding: '2rem 1rem' }}>
            <section className="behavior-completion" aria-labelledby="single-q-completion-title">
              <div className="behavior-completion__icon"><CheckCircle2 size={42} /></div>
              <span>Single Question Interview</span>
              <h1 id="single-q-completion-title">Đã hoàn thành đánh giá câu hỏi</h1>
              <p>Hệ thống AI-SPEIS đã phân tích và chấm điểm câu trả lời của bạn.</p>

              <dl>
                <div>
                  <dt>Điểm tổng kết</dt>
                  <dd><strong>{result.score ?? '-'} / {result.maxScore ?? 10}</strong></dd>
                </div>
                <div>
                  <dt>Vòng phỏng vấn</dt>
                  <dd>{interview?.roundType || 'Behavioral'}</dd>
                </div>
                <div>
                  <dt>Trạng thái</dt>
                  <dd>Đã đánh giá</dd>
                </div>
              </dl>

              {/* Rubric Dimensions */}
              {Array.isArray(result.dimensions) && result.dimensions.length > 0 ? (
                <div className="single-q-result-dimensions">
                  <h3><Sparkles size={18} /> Phân tích từng tiêu chí đánh giá</h3>
                  <div className="single-q-dimensions-list">
                    {result.dimensions.map((dim) => (
                      <article key={dim.rubricCode || dim.name} className="single-q-dim-card">
                        <div className="single-q-dim-head">
                          <strong>{dim.name || dim.rubricCode}</strong>
                          <span className="single-q-dim-badge">{dim.score} pt</span>
                        </div>
                        {dim.evidence?.length ? (
                          <p className="single-q-evidence pos">
                            <strong><ThumbsUp size={14} /> Bằng chứng:</strong> {dim.evidence.join(' ')}
                          </p>
                        ) : null}
                        {dim.missingEvidence?.length ? (
                          <p className="single-q-evidence neg">
                            <strong><ThumbsDown size={14} /> Điểm chưa đủ / Thiếu:</strong> {dim.missingEvidence.join(' ')}
                          </p>
                        ) : null}
                      </article>
                    ))}
                  </div>
                </div>
              ) : null}

              {/* Strengths & Improvements */}
              {(result.strengths?.length || result.missingPoints?.length) ? (
                <div className="single-q-summary-grid">
                  {result.strengths?.length ? (
                    <div className="single-q-summary-card pos">
                      <h4><ThumbsUp size={16} /> Điểm mạnh</h4>
                      <ul>{result.strengths.map((str, idx) => <li key={idx}>{str}</li>)}</ul>
                    </div>
                  ) : null}
                  {result.missingPoints?.length ? (
                    <div className="single-q-summary-card neg">
                      <h4><ThumbsDown size={16} /> Điểm cần cải thiện</h4>
                      <ul>{result.missingPoints.map((pts, idx) => <li key={idx}>{pts}</li>)}</ul>
                    </div>
                  ) : null}
                </div>
              ) : null}

              <div className="behavior-completion__actions" style={{ marginTop: '2rem' }}>
                <button type="button" onClick={handleRetryQuestion}>
                  <RotateCcw size={18} />
                  Thử lại câu hỏi này
                </button>
                <button type="button" className="behavior-completion__primary" onClick={leave}>
                  Quay lại lịch sử
                  <ArrowRight size={18} />
                </button>
              </div>
            </section>
          </div>
        ) : (
          /* Live Interview Stage */
          <>
            <header className="behavior-stage__topbar">
              <div className="behavior-stage__session">
                <span>{interview?.roundType || 'Behavioral'}</span>
                <strong>Single Question Interview</strong>
                <button
                  type="button"
                  className="technical-transcript-toggle technical-transcript-toggle--topbar"
                  onClick={() => setTranscriptOpen((open) => !open)}
                  aria-expanded={transcriptOpen}
                >
                  <FileText size={15} aria-hidden="true" />
                  {t('transcript')}
                </button>
              </div>
              <div className="behavior-stage__top-actions">
                <button type="button" className="behavior-stage__end" onClick={leave}>
                  <ArrowLeft size={16} /> Quay lại
                </button>
              </div>
            </header>

            <div className="behavior-stage__progress" role="progressbar">
              <span style={{ width: '100%' }} />
            </div>

            <section className="behavior-question" aria-labelledby="behavior-question-text">
              <div className="behavior-question__eyebrow">
                {interview?.roundType || 'Behavior'} · Single question interview
              </div>
              <h1 id="behavior-question-text">{interview?.question}</h1>
              <p className="behavior-question__hint">
                Hãy trả lời như một vòng phỏng vấn thực tế. Kết quả đánh giá sẽ được lưu để bạn xem lại.
              </p>

              <div className={`behavior-interviewer ${recorder.recordingStatus === 'RECORDING' ? 'behavior-interviewer--listening' : ''}`} aria-hidden="true">
                <span className="behavior-interviewer__ring" />
                <span className="behavior-interviewer__ring" />
                <span className="behavior-interviewer__core"><Bot size={28} /></span>
              </div>

              <div className="behavior-audio-controls" aria-label={t('questionAudio')}>
                {questionAudio.status === 'LOADING' ? <Loader2 size={18} className="behavior-spin" /> : null}
                {questionAudio.status === 'READY' ? (
                  <>
                    <button type="button" onClick={questionAudio.isPlaying ? questionAudio.pause : questionAudio.play}>
                      {questionAudio.isPlaying ? <Pause size={17} /> : <Volume2 size={17} />}
                      {questionAudio.isPlaying ? t('pauseQuestion') : t('playQuestion')}
                    </button>
                  </>
                ) : null}
              </div>
            </section>

            <footer className="behavior-stage__controls">
              {error ? (
                <div className="behavior-inline-error" role="alert">
                  <AlertCircle size={18} />
                  <span>{error}</span>
                </div>
              ) : null}
              <BehavioralRecorderControls
                recorder={recorder}
                disabled={submitting}
                isSubmitting={submitting}
                timeLimitSeconds={180}
                remainingSeconds={undefined}
                strategy={{}}
                isAudioPlaying={questionAudio.isPlaying}
                onSubmit={submit}
                t={t}
              />
            </footer>
          </>
        )}
      </section>
    </InterviewRoomShell>
  );
}


