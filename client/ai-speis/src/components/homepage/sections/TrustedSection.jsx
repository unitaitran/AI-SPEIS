const brands = [
  { id: 'northstar', accent: 'accent-a', shape: 'diamond' },
  { id: 'vertex', accent: 'accent-b', shape: 'square' },
  { id: 'aether', accent: 'accent-c', shape: 'circle' },
  { id: 'inline', accent: 'accent-d', shape: 'wave' },
  { id: 'delta', accent: 'accent-e', shape: 'hex' },
];

function BrandMark({ shape }) {
  switch (shape) {
    case 'diamond':
      return (
        <svg viewBox="0 0 64 64" aria-hidden="true">
          <path d="M32 8L56 32L32 56L8 32Z" />
        </svg>
      );
    case 'square':
      return (
        <svg viewBox="0 0 64 64" aria-hidden="true">
          <rect x="14" y="14" width="36" height="36" rx="8" />
        </svg>
      );
    case 'circle':
      return (
        <svg viewBox="0 0 64 64" aria-hidden="true">
          <circle cx="32" cy="32" r="20" />
        </svg>
      );
    case 'wave':
      return (
        <svg viewBox="0 0 64 64" aria-hidden="true">
          <path d="M10 36c8-10 16-10 24 0s16 10 24 0" />
        </svg>
      );
    case 'hex':
    default:
      return (
        <svg viewBox="0 0 64 64" aria-hidden="true">
          <path d="M32 8L50 18v28L32 56L14 46V18Z" />
        </svg>
      );
  }
}

function TrustedSection({ t }) {
  return (
    <section className="home-trusted" aria-label={t('aria.highlights')}>
      <div className="home-section-shell home-trusted__inner">
        <span className="home-kicker home-trusted__kicker">{t('trusted.kicker')}</span>
        <div className="home-trusted__logos">
          {brands.map((brand) => (
            <div key={brand.id} className={`home-trusted__logo home-trusted__logo--${brand.accent}`} aria-hidden="true">
              <BrandMark shape={brand.shape} />
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}

export default TrustedSection;
