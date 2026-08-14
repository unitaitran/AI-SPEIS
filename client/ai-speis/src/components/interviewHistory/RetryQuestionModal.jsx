import React, { useState } from 'react';
import { LoaderCircle, X } from 'lucide-react';
import singleQuestionRetryApi from '../../services/singleQuestionRetryApi';
import './RetryQuestionModal.css';

export default function RetryQuestionModal({ question, originalSessionId, roundType = 'Technical', onClose }) {
  const [transcript, setTranscript] = useState('');
  const [result, setResult] = useState(null);
  const [error, setError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const submit = async (event) => {
    event.preventDefault();
    if (!transcript.trim()) return setError('Vui lòng nhập câu trả lời.');
    setSubmitting(true); setError('');
    try { setResult(await singleQuestionRetryApi.retryQuestion({ questionId: question.questionId, originalSessionId, roundType, transcript: transcript.trim() })); }
    catch (e) { setError(e.message || 'Không thể đánh giá câu trả lời.'); }
    finally { setSubmitting(false); }
  };
  return <div className="retry-modal-backdrop" role="dialog" aria-modal="true"><div className="retry-modal">
    <button type="button" className="retry-modal-close" onClick={onClose} aria-label="Đóng"><X size={20} /></button>
    <h2>Thử lại câu hỏi</h2><p className="retry-modal-question">{question.question}</p>
    {!result ? <form onSubmit={submit}><textarea value={transcript} onChange={(e) => setTranscript(e.target.value)} rows={7} placeholder="Nhập câu trả lời của bạn..." disabled={submitting} />{error && <p className="retry-modal-error">{error}</p>}<button type="submit" disabled={submitting}>{submitting ? <><LoaderCircle size={16} className="retry-spin" /> Đang đánh giá...</> : 'Gửi câu trả lời'}</button></form> : <div className="retry-modal-result"><p className="retry-modal-score">Điểm mới: <strong>{result.score ?? '-'} / {result.maxScore ?? 10}</strong>{question.score != null && <span>Điểm cũ: {question.score} / {question.maxScore ?? 10}</span>}</p>{result.dimensions?.map((d) => <div className="retry-dimension" key={d.rubricCode}><b>{d.name || d.rubricCode}: {d.score}</b>{d.evidence?.length ? <p>Evidence: {d.evidence.join(' ')}</p> : null}{d.missingEvidence?.length ? <p>Thiếu: {d.missingEvidence.join(' ')}</p> : null}</div>)}{result.strengths?.length ? <p><b>Điểm mạnh:</b> {result.strengths.join(' ')}</p> : null}{result.missingPoints?.length ? <p><b>Điểm cần cải thiện:</b> {result.missingPoints.join(' ')}</p> : null}<button type="button" onClick={onClose}>Đóng</button></div>}
  </div></div>;
}
