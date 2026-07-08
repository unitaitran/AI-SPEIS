import { render, screen } from '@testing-library/react';
import HomePage from './HomePage';
import i18n from '../../i18n';

describe('HomePage', () => {
  beforeEach(() => {
    i18n.changeLanguage('en');
  });
  test('renders the main hero heading and key sections', () => {
    render(<HomePage currentHash="#hero" onToggleLanguage={() => {}} />);

    expect(screen.getByRole('heading', { name: /Practice IT interviews/i })).toBeInTheDocument();
    expect(screen.getByText(/AI-SPEIS helps you practice in a safe environment/i)).toBeInTheDocument();
    expect(screen.getByText(/How do I upload my CV/i)).toBeInTheDocument();
  });
});
