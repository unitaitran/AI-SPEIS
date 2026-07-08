function TestimonialsSection({ testimonials = [], t }) {
  return (
    <section className="home-section home-section--surface" id="testimonials">
      <div className="home-section-shell">
        <div className="home-section-heading">
          <span className="home-kicker">{t('sections.testimonials.kicker')}</span>
          <h2>{t('sections.testimonials.title')}</h2>
        </div>

        <div className="home-testimonials-grid">
          {testimonials.map((item, index) => (
            <article className="home-card home-card--testimonial" key={item.name || index}>
              <div className="home-card__avatar" aria-hidden="true">
                {item.name?.charAt(0)}
              </div>
              <div>
                <h3>{item.name}</h3>
                <p className="home-card__meta">{item.role}</p>
                <p>{item.text}</p>
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}

export default TestimonialsSection;
