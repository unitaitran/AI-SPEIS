import { describe, expect, it } from 'vitest';
import { getDefaultRouteForRole } from './routes/auth';

describe('routing helpers', () => {
  it('maps roles to the correct destinations', () => {
    expect(getDefaultRouteForRole('user')).toBe('/user/dashboard');
    expect(getDefaultRouteForRole('ADMIN')).toBe('/admin/dashboard');
    expect(getDefaultRouteForRole('unknown')).toBe('/#login');
  });
});
