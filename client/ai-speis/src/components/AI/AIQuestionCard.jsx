import React from 'react';
import { Volume2, Sparkles, RotateCcw } from 'lucide-react';
import Badge from '../UI/Badge';
import Card from '../UI/Card';

export const AIQuestionCard = ({
  questionNumber,
  totalQuestions,
  questionText,
  roleTag = 'Software Engineer',
  difficulty = 'Medium',
  typeTag = 'Technical',
  provider = 'Gemini AI',
  onReplayAudio,
  isPlayingAudio = false,
  autoPlay = true,
  onToggleAutoPlay,
  className = '',
}) => {
  const difficultyVariants = {
    Easy: 'success',
    Medium: 'warning',
    Hard: 'error',
  };

  return (
    <Card variant="ai" className={`flex flex-col gap-4 p-6 relative overflow-hidden ${className}`}>
      {/* Top Header Controls */}
      <div className="flex items-center justify-between gap-2 border-b border-border/60 pb-3.5">
        <div className="flex items-center gap-2 flex-wrap">
          {questionNumber && totalQuestions && (
            <Badge variant="primary" size="sm">
              Câu {questionNumber} / {totalQuestions}
            </Badge>
          )}
          <Badge variant="neutral" size="sm">
            {roleTag}
          </Badge>
          <Badge variant={difficultyVariants[difficulty] || 'neutral'} size="sm">
            {difficulty}
          </Badge>
          <Badge variant="ai" size="sm" icon={Sparkles}>
            {typeTag}
          </Badge>
        </div>

        {provider && (
          <span className="text-[11px] font-semibold text-secondary bg-secondary-light/60 px-2 py-0.5 rounded-full shrink-0">
            {provider}
          </span>
        )}
      </div>

      {/* Main Question Text */}
      <div className="py-2 text-center my-auto">
        <h2 className="text-xl md:text-2xl font-bold text-text-primary leading-snug">
          {questionText || 'Đang tạo câu hỏi phỏng vấn...'}
        </h2>
      </div>

      {/* Audio Playback Controls */}
      <div className="flex items-center justify-center gap-3 pt-3 border-t border-border/60">
        {onReplayAudio && (
          <button
            type="button"
            onClick={onReplayAudio}
            disabled={isPlayingAudio}
            className={`
              inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-semibold transition-colors focus-ring
              ${isPlayingAudio 
                ? 'bg-primary-light text-primary animate-pulse' 
                : 'bg-surface-muted hover:bg-border text-text-secondary hover:text-text-primary'
              }
            `}
          >
            <Volume2 size={15} />
            <span>{isPlayingAudio ? 'Đang phát...' : 'Phát lại audio'}</span>
          </button>
        )}

        {onToggleAutoPlay && (
          <button
            type="button"
            onClick={onToggleAutoPlay}
            className={`
              inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-semibold transition-colors focus-ring
              ${autoPlay ? 'bg-secondary-light text-secondary' : 'bg-surface-muted text-text-muted'}
            `}
          >
            <RotateCcw size={13} />
            <span>Tự động phát: {autoPlay ? 'Bật' : 'Tắt'}</span>
          </button>
        )}
      </div>
    </Card>
  );
};

export default AIQuestionCard;
