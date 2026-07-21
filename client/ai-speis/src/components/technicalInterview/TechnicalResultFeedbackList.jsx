import React from 'react';

function TechnicalResultFeedbackList({ title, items }) {
  if (!Array.isArray(items) || items.length === 0) return null;
  return (
    <div className="technical-feedback-list">
      <h4>{title}</h4>
      <ul>
        {items.map((item, index) => <li key={`${index}-${item}`}>{item}</li>)}
      </ul>
    </div>
  );
}

export default TechnicalResultFeedbackList;

