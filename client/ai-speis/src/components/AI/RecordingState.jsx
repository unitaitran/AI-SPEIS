import React from 'react';
import { Mic, Bot, Sparkles, AlertCircle } from 'lucide-react';

export const RecordingState = ({
  status = 'idle', // 'idle' | 'recording' | 'processing' | 'evaluating' | 'error'
  message,
  className = '',
}) => {
  const statusConfigs = {
    idle: {
      icon: Mic,
      text: message || 'Nhấn nút Microphone để bắt đầu trả lời',
      color: 'text-text-secondary bg-surface-muted border-border',
    },
    recording: {
      icon: Mic,
      text: message || 'Đang ghi âm giọng nói của bạn...',
      color: 'text-error bg-error-light border-error/30 animate-pulse',
    },
    processing: {
      icon: Bot,
      text: message || 'AI đang chuyển giọng nói thành văn bản (STT)...',
      color: 'text-primary bg-primary-light border-primary/30',
    },
    evaluating: {
      icon: Sparkles,
      text: message || 'AI đang chấm điểm & tạo phản hồi...',
      color: 'text-secondary bg-secondary-light border-secondary/30 animate-pulse',
    },
    error: {
      icon: AlertCircle,
      text: message || 'Lỗi xử lý âm thanh. Vui lòng thử lại.',
      color: 'text-error bg-error-light border-error/40',
    },
  };

  const current = statusConfigs[status] || statusConfigs.idle;
  const Icon = current.icon;

  return (
    <div className={`inline-flex items-center gap-2 px-3.5 py-2 rounded-full border text-xs font-semibold ${current.color} ${className}`}>
      <Icon size={16} className="shrink-0" />
      <span>{current.text}</span>
    </div>
  );
};

export default RecordingState;
