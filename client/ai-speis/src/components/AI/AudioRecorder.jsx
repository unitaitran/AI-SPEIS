import React from 'react';
import { Mic, Square, Send } from 'lucide-react';
import Button from '../UI/Button';

export const AudioRecorder = ({
  isRecording = false,
  isProcessing = false,
  durationSeconds = 0,
  onStartRecord,
  onStopRecord,
  onSubmitAnswer,
  disabled = false,
  className = '',
}) => {
  const formatTime = (totalSeconds) => {
    const mins = Math.floor(totalSeconds / 60);
    const secs = totalSeconds % 60;
    return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  };

  return (
    <div className={`flex flex-col items-center gap-3 p-4 bg-surface rounded-xl border border-border shadow-md w-full max-w-md mx-auto ${className}`}>
      {/* Audio Wave Visualizer Indicator */}
      <div className="flex items-center gap-1.5 h-8 my-1">
        {[1, 2, 3, 4, 5, 6, 7].map((bar) => (
          <div
            key={bar}
            className={`
              w-1.5 rounded-full transition-all duration-200
              ${isRecording 
                ? 'bg-error animate-audioWave' 
                : isProcessing 
                ? 'bg-secondary animate-pulse' 
                : 'bg-border h-2'
              }
            `}
            style={{
              animationDelay: isRecording ? `${bar * 0.15}s` : '0s',
              height: isRecording ? `${Math.floor(Math.random() * 20) + 10}px` : undefined,
            }}
          />
        ))}
      </div>

      {/* Recording Duration Timer */}
      <div className="text-sm font-mono font-bold text-text-primary">
        {formatTime(durationSeconds)}
      </div>

      {/* Main Mic Button */}
      <div className="flex items-center gap-3">
        {!isRecording ? (
          <button
            type="button"
            onClick={onStartRecord}
            disabled={disabled || isProcessing}
            className={`
              w-14 h-14 rounded-full bg-error hover:bg-red-700 text-white flex items-center justify-center shadow-lg transition-transform active:scale-95 focus-ring disabled:opacity-50 disabled:cursor-not-allowed
            `}
            aria-label="Bắt đầu ghi âm câu trả lời"
          >
            <Mic size={24} />
          </button>
        ) : (
          <button
            type="button"
            onClick={onStopRecord}
            className="w-14 h-14 rounded-full bg-slate-900 hover:bg-black text-white flex items-center justify-center shadow-lg transition-transform active:scale-95 focus-ring animate-pulse"
            aria-label="Dừng ghi âm"
          >
            <Square size={20} />
          </button>
        )}

        {onSubmitAnswer && (
          <Button
            variant="primary"
            size="md"
            icon={Send}
            onClick={onSubmitAnswer}
            disabled={disabled || isRecording || isProcessing}
            loading={isProcessing}
          >
            Nộp câu trả lời
          </Button>
        )}
      </div>
    </div>
  );
};

export default AudioRecorder;
