import enHomepage from './en/homepage.json';
import viHomepage from './vi/homepage.json';

describe('homepage translation resources', () => {
  test('English homepage includes all keys used by the landing page', () => {
    expect(enHomepage).toBeDefined();
    expect(enHomepage.hero?.tag).toBeTruthy();
    expect(enHomepage.hero?.cards).toHaveLength(3);
    expect(enHomepage.mascot?.status).toBeTruthy();
    expect(enHomepage.mascot?.note).toBeTruthy();
    expect(enHomepage.sections?.cta?.kicker).toBeTruthy();
    expect(enHomepage.sections?.cta?.title).toBeTruthy();
    expect(enHomepage.sections?.cta?.text).toBeTruthy();
    expect(enHomepage.sections?.pricing?.free).toBeTruthy();
    expect(enHomepage.footer?.social).toBeTruthy();
  });

  test('Vietnamese homepage includes all keys used by the landing page', () => {
    expect(viHomepage).toBeDefined();
    expect(viHomepage.hero?.tag).toBeTruthy();
    expect(viHomepage.hero?.cards).toHaveLength(3);
    expect(viHomepage.mascot?.status).toBeTruthy();
    expect(viHomepage.mascot?.note).toBeTruthy();
    expect(viHomepage.sections?.cta?.kicker).toBeTruthy();
    expect(viHomepage.sections?.cta?.title).toBeTruthy();
    expect(viHomepage.sections?.cta?.text).toBeTruthy();
    expect(viHomepage.sections?.pricing?.free).toBeTruthy();
    expect(viHomepage.footer?.social).toBeTruthy();
  });
});
