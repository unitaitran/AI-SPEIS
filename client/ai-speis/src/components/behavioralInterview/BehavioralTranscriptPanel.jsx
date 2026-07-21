import React, { useEffect, useRef, useState } from 'react';
import { ArrowDown, MessageSquareText, X } from 'lucide-react';

function BehavioralTranscriptPanel({
  isOpen,
  messages,
  draftTranscript,
  sttStatus,
  onClose,
  t,
}) {
  const scrollRef = useRef(null);
  const [isAtLatest, setIsAtLatest] = useState(true);

  const scrollToLatest = (behavior = 'smooth') => {
    const node = scrollRef.current;
    if (!node) return;
    node.scrollTo({ top: node.scrollHeight, behavior });
    setIsAtLatest(true);
  };

  useEffect(() => {
    if (isOpen && isAtLatest) scrollToLatest('smooth');
  }, [draftTranscript, isAtLatest, isOpen, messages]);

  const handleScroll = () => {
    const node = scrollRef.current;
    if (!node) return;
    setIsAtLatest(node.scrollHeight - node.scrollTop - node.clientHeight < 72);
  };

  if (!isOpen) return null;

  return (
    <aside className="behavior-transcript" aria-label={t('transcriptTitle')}>
      <header className="behavior-transcript__header">
        <div>
          <h2>{t('transcriptTitle')}</h2>
          <p>{t('transcriptDescription')}</p>
        </div>
        <button type="button" onClick={onClose} aria-label={t('closeTranscript')} title={t('closeTranscript')}>
          <X size={20} />
        </button>
      </header>

      <div
        ref={scrollRef}
        className="behavior-transcript__messages"
        onScroll={handleScroll}
        aria-live="off"
      >
        {messages.length === 0 && !draftTranscript && sttStatus !== 'PROCESSING' ? (
          <div className="behavior-transcript__empty">
            <MessageSquareText size={28} aria-hidden="true" />
            <p>{t('transcriptEmpty')}</p>
          </div>
        ) : null}

        {messages.map((message) => (
          <article
            key={message.id}
            className={`behavior-message behavior-message--${message.speaker}`}
          >
            <div className="behavior-message__meta">
              <strong>{message.speaker === 'interviewer' ? t('interviewer') : t('candidate')}</strong>
              {message.questionType && message.questionType !== 'Main' ? (
                <span>{t(`questionType.${message.questionType}`)}</span>
              ) : null}
            </div>
            <p>{message.content}</p>
          </article>
        ))}

        {draftTranscript ? (
          <article className="behavior-message behavior-message--candidate behavior-message--draft">
            <div className="behavior-message__meta">
              <strong>{t('candidate')}</strong>
              <span>{t('draft')}</span>
            </div>
            <p>{draftTranscript}</p>
            {sttStatus === 'PROCESSING' ? <small>{t('transcribing')}</small> : null}
          </article>
        ) : null}
        {!draftTranscript && sttStatus === 'PROCESSING' ? (
          <div className="behavior-message behavior-message--candidate behavior-message--draft" role="status">
            <div className="behavior-message__meta"><strong>{t('candidate')}</strong></div>
            <small>{t('transcribing')}</small>
          </div>
        ) : null}
      </div>

      {!isAtLatest ? (
        <button type="button" className="behavior-transcript__latest" onClick={() => scrollToLatest()}>
          <ArrowDown size={16} />
          {t('jumpToLatest')}
        </button>
      ) : null}
    </aside>
  );
}

export default BehavioralTranscriptPanel;
