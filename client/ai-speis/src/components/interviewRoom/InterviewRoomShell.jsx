import React from 'react';
import { FileText } from 'lucide-react';
import UserLayout from '../../layouts/user/UserLayout';

function InterviewRoomShell({
  children,
  dialog,
  header,
  isTranscriptOpen,
  language,
  mainFlush = false,
  onBeforeNavigate,
  onCloseTranscript,
  onToggleTranscript,
  transcript,
  transcriptCloseLabel,
  transcriptLabel,
}) {
  return (
    <UserLayout compactSidebar immersive onBeforeNavigate={onBeforeNavigate}>
      <div className="technical-page technical-page--room animate-pageEntrance" lang={language}>
        <div className={`technical-interview-workspace${isTranscriptOpen ? ' technical-interview-workspace--transcript-open' : ''}`}>
          {isTranscriptOpen ? (
            <>
              <button
                type="button"
                className="technical-transcript-backdrop"
                onClick={onCloseTranscript}
                aria-label={transcriptCloseLabel}
              />
              {transcript}
            </>
          ) : null}
          <section className="technical-main-stage">
            {header}
            <div className={`technical-main-stage__body${mainFlush ? ' technical-main-stage__body--flush' : ''}`}>
              {children}
            </div>
            <div className="technical-main-stage__actions">
              <button
                type="button"
                className="technical-transcript-toggle"
                onClick={onToggleTranscript}
                aria-expanded={isTranscriptOpen}
              >
                <FileText size={18} aria-hidden="true" />
                {transcriptLabel}
              </button>
            </div>
          </section>
        </div>
        {dialog}
      </div>
    </UserLayout>
  );
}

export default InterviewRoomShell;
