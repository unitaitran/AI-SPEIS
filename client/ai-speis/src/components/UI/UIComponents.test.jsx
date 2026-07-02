import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import Button from './Button';

describe('shared UI components', () => {
  it('renders a button with the expected accessible role', () => {
    render(<Button>Continue</Button>);
    expect(screen.getByRole('button', { name: /continue/i })).toBeInTheDocument();
  });

  it('renders a disabled button state', () => {
    render(<Button disabled>Disabled</Button>);
    expect(screen.getByRole('button', { name: /disabled/i })).toBeDisabled();
  });
});
