function WorkflowSection({ flowSteps = [], t }) {
  return (
    <section className="home-section" id="flow">
      <div className="home-section-shell">
        <div className="home-section-heading home-section-heading--compact">
          <span className="home-kicker">{t('sections.flow.kicker')}</span>
          <h2>{t('sections.flow.title')}</h2>
          <p>{t('sections.flow.text')}</p>
        </div>

        <div className="home-workflow-list">
          {flowSteps.map((step, index) => (
            <article className="home-workflow-item" key={step.title || index}>
              <div className="home-workflow-item__step">0{index + 1}</div>
              <div className="home-workflow-item__body">
                <h3>{step.title}</h3>
                <p>{step.text}</p>
              </div>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}

export default WorkflowSection;
