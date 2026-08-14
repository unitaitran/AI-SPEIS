import React from 'react';
import { useTranslation } from 'react-i18next';
import { Loader2, Sparkles } from 'lucide-react';

function EvaluatingAnalysisModal({
  isOpen,
  title,
  description,
}) {
  const { t } = useTranslation('interview');
  if (!isOpen) return null;

  const displayTitle = title || t('device.evaluatingAnalysisTitle', { defaultValue: 'Hệ thống đang đánh giá và phân tích' });
  const displayDescription = description || t('device.evaluatingAnalysisDescription', { defaultValue: 'Vui lòng chờ trong giây lát, hệ thống đang tổng hợp và phân tích kết quả phỏng vấn của bạn...' });

  return (
    <div
      className="behavior-dialog-backdrop"
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 9999,
        display: 'grid',
        placeItems: 'center',
        background: 'rgba(15, 23, 42, 0.65)',
        backdropFilter: 'blur(6px)',
        padding: '1rem',
      }}
      role="presentation"
    >
      <section
        className="behavior-dialog"
        style={{
          width: 'min(440px, 100%)',
          padding: '2rem 1.5rem',
          borderRadius: '16px',
          background: '#ffffff',
          boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.25)',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          textAlign: 'center',
          gap: '1rem',
        }}
        role="dialog"
        aria-modal="true"
      >
        <div
          style={{
            position: 'relative',
            width: '64px',
            height: '64px',
            borderRadius: '50%',
            background: '#e0f2fe',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            color: '#0284c7',
          }}
        >
          <Loader2 size={36} className="behavior-spin" />
          <Sparkles
            size={18}
            style={{
              position: 'absolute',
              top: '4px',
              right: '4px',
              color: '#f59e0b',
            }}
          />
        </div>
        <div>
          <h3 style={{ margin: '0 0 0.5rem', fontSize: '1.25rem', fontWeight: '700', color: '#0f172a' }}>
            {displayTitle}
          </h3>
          <p style={{ margin: 0, fontSize: '0.875rem', color: '#475569', lineHeight: '1.5' }}>
            {displayDescription}
          </p>
        </div>
      </section>
    </div>
  );
}

export default EvaluatingAnalysisModal;
