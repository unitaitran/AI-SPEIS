import { render, screen } from '@testing-library/react';
import HomePage from './HomePage';
import i18n from '../../i18n';

describe('HomePage', () => {
  beforeEach(() => {
    i18n.changeLanguage('en');
  });
  test('renders the main hero heading and key sections', () => {
    const t = (key, fallback) => fallback || key;
    render(<HomePage currentHash="#hero" onToggleLanguage={() => {}} t={t} i18n={i18n} />);

    expect(screen.getByRole('heading', { name: /Bứt Phá Phỏng Vấn IT/i })).toBeInTheDocument();
  });
});
