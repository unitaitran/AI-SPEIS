import enHomepage from './en/landing.json';
import viHomepage from './vi/landing.json';

describe('homepage translation resources', () => {
  test('English homepage includes all keys used by the landing page', () => {
    expect(enHomepage).toBeDefined();
    expect(enHomepage.hero?.title).toBeTruthy();
    expect(enHomepage.hero?.metrics).toBeDefined();
    expect(enHomepage.mascot?.status).toBeTruthy();
    expect(enHomepage.mascot?.note).toBeTruthy();
    expect(enHomepage.bento?.title).toBeTruthy();
    expect(enHomepage.pricing?.free).toBeTruthy();
    expect(enHomepage.footer?.rights).toBeTruthy();
  });

  test('Vietnamese homepage includes all keys used by the landing page', () => {
    expect(viHomepage).toBeDefined();
    expect(viHomepage.hero?.title).toBeTruthy();
    expect(viHomepage.hero?.metrics).toBeDefined();
    expect(viHomepage.mascot?.status).toBeTruthy();
    expect(viHomepage.mascot?.note).toBeTruthy();
    expect(viHomepage.bento?.title).toBeTruthy();
    expect(viHomepage.pricing?.free).toBeTruthy();
    expect(viHomepage.footer?.rights).toBeTruthy();
  });
});
