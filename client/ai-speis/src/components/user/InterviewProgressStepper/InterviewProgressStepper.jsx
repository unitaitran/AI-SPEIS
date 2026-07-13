import React from 'react';
import { useTranslation } from 'react-i18next';
import './InterviewProgressStepper.css';

function InterviewProgressStepper({ activeStep, language }) {
  const { t } = useTranslation('interview');
  const translate = (key) => t(key, language ? { lng: language } : undefined);
  const steps = [
    translate('progress.mode'),
    translate('progress.setup'),
    translate('progress.device'),
    translate('progress.start'),
    translate('progress.evaluation'),
    translate('progress.result'),
  ];

  return (
    <div className="interview-progress-scroll">
      <ol className="interview-progress" aria-label={translate('progress.aria')}>
        {steps.map((step, index) => {
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
