import React, { useEffect, useRef } from 'react';
import { Bot, UserRound, X } from 'lucide-react';

const NEAR_BOTTOM_THRESHOLD = 80;

function InterviewRoomTranscriptPanel({
  candidateLabel,
  children,
  closeLabel,
  description,
  emptyMessage,
  interviewerLabel,
  isOpen,
  items = [],
  liveState,
  onClose,
  title,
}) {
  const closeButtonRef = useRef(null);
  const listRef = useRef(null);
  const isNearBottomRef = useRef(true);

  useEffect(() => {
    if (!isOpen) return undefined;
    closeButtonRef.current?.focus({ preventScroll: true });
    const handleKeyDown = (event) => {
      if (event.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  useEffect(() => {
    if (!isOpen || !isNearBottomRef.current) return;
    const list = listRef.current;
    if (list) list.scrollTop = list.scrollHeight;
  }, [isOpen, items, liveState]);

  const handleScroll = () => {
    const list = listRef.current;
    if (!list) return;
    isNearBottomRef.current = list.scrollHeight - list.scrollTop - list.clientHeight
      <= NEAR_BOTTOM_THRESHOLD;
  };

  const LiveIcon = liveState?.icon;

  return (
    <aside className="technical-transcript-panel" aria-label={title}>
      <header className="technical-transcript-panel__header">
        <div>
          <h2>{title}</h2>
          <p>{description}</p>
        </div>
        <button
          ref={closeButtonRef}
          type="button"
          className="technical-icon-button"
          onClick={onClose}
          aria-label={closeLabel}
        >
          <X size={20} aria-hidden="true" />
        </button>
      </header>

      {liveState ? (
        <div className={`technical-transcript-live technical-transcript-live--${liveState.tone || 'processing'}`} role="status">
          {LiveIcon ? <LiveIcon size={17} className={liveState.spin ? 'animate-spin' : undefined} /> : null}
          <span>{liveState.label}</span>
        </div>
      ) : null}

      <div
        ref={listRef}
        className="technical-transcript-panel__list"
        role="log"
        aria-live="polite"
        aria-relevant="additions text"
        onScroll={handleScroll}
      >
        {items.length === 0 ? (
          <div className="technical-transcript-empty">
            <Bot size={24} aria-hidden="true" />
            <p>{emptyMessage}</p>
          </div>
        ) : items.map((item) => {
          const isInterviewer = String(item.role || '').toUpperCase() === 'INTERVIEWER';
          const ItemIcon = isInterviewer ? Bot : UserRound;
          return (
            <article
              key={item.id}
              className={`technical-transcript-item technical-transcript-item--${isInterviewer ? 'interviewer' : 'candidate'}`}
            >
              <div className="technical-transcript-item__speaker">
                <ItemIcon size={16} aria-hidden="true" />
                <strong>{isInterviewer ? interviewerLabel : candidateLabel}</strong>
                {item.statusLabel ? <span>{item.statusLabel}</span> : null}
              </div>
              <p>{item.content}</p>
            </article>
          );
        })}
      </div>

      {children ? <div className="technical-transcript-panel__editor">{children}</div> : null}
    </aside>
  );
}

export default InterviewRoomTranscriptPanel;
