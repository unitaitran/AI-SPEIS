import React from 'react';
import { FileText } from 'lucide-react';

const TechnicalTranscriptToggle = React.forwardRef(function TechnicalTranscriptToggle({
  isOpen,
  onClick,
  t,
}, ref) {
  return (
    <button
      ref={ref}
      type="button"
      className="technical-transcript-toggle"
      onClick={onClick}
      aria-controls="technical-transcript-panel"
      aria-expanded={isOpen}
    >
      <FileText size={18} aria-hidden="true" />
      {t('room.transcript')}
    </button>
  );
});

export default TechnicalTranscriptToggle;
