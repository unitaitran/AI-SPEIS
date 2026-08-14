import React from 'react';
import { Check } from 'lucide-react';

export const InterviewProgress = ({
  steps = ['Chế độ', 'Cấu hình', 'Kiểm tra mic', 'Phỏng vấn', 'Đánh giá', 'Kết quả'],
  currentStep = 1, // 1-indexed
  onStepClick,
  className = '',
}) => {
  return (
    <div className={`w-full overflow-x-auto py-2 ${className}`}>
      <div className="flex items-center justify-between min-w-[500px] px-2">
        {steps.map((step, idx) => {
          const stepNumber = idx + 1;
          const isCompleted = stepNumber < currentStep;
          const isCurrent = stepNumber === currentStep;

          return (
            <React.Fragment key={idx}>
              <div 
                className={`flex flex-col items-center gap-1.5 cursor-pointer ${onStepClick ? 'hover:opacity-80' : ''}`}
                onClick={() => onStepClick && isCompleted && onStepClick(stepNumber)}
              >
                <div
                  className={`
                    w-8 h-8 rounded-full flex items-center justify-center text-xs font-bold transition-all duration-200
                    ${isCompleted 
                      ? 'bg-success text-white' 
                      : isCurrent 
                      ? 'bg-primary text-white ring-4 ring-primary-light animate-pulse' 
                      : 'bg-surface-muted border border-border text-text-muted'
                    }
                  `}
                >
                  {isCompleted ? <Check size={16} /> : stepNumber}
                </div>
                <span 
                  className={`
                    text-xs font-semibold whitespace-nowrap
                    ${isCurrent ? 'text-primary font-bold' : isCompleted ? 'text-text-primary' : 'text-text-muted'}
                  `}
                >
                  {step}
                </span>
              </div>

              {idx < steps.length - 1 && (
                <div 
                  className={`
                    flex-1 h-0.5 mx-2 transition-all duration-300
                    ${stepNumber < currentStep ? 'bg-success' : 'bg-border'}
                  `} 
                />
              )}
            </React.Fragment>
          );
        })}
      </div>
    </div>
  );
};

export default InterviewProgress;
