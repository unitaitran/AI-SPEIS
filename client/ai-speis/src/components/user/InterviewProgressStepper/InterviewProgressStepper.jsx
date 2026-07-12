import React from 'react';
import './InterviewProgressStepper.css';

const INTERVIEW_STEPS = [
  'Chế độ',
  'Thiết lập',
  'Kiểm tra thiết bị',
  'Bắt đầu',
  'Đánh giá',
  'Kết quả',
];

function InterviewProgressStepper({ activeStep }) {
  return (
    <div className="interview-progress-scroll">
      <ol className="interview-progress" aria-label="Tiến trình phỏng vấn">
        {INTERVIEW_STEPS.map((step, index) => {
          const isActive = index === activeStep;
          const isCompleted = index < activeStep;

          return (
            <li
              className={`interview-progress-item${isCompleted ? ' interview-progress-item--completed' : ''}`}
              key={step}
              aria-current={isActive ? 'step' : undefined}
            >
              <span className={`interview-progress-number${isActive ? ' interview-progress-number--active' : ''}`}>
                {index + 1}
              </span>
              <span className="interview-progress-label">{step}</span>
            </li>
          );
        })}
      </ol>
    </div>
  );
}

export default InterviewProgressStepper;
