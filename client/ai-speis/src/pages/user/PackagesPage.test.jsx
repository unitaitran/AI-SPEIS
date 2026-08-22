import React from 'react';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import PackagesPage from './PackagesPage';
import paymentService from '../../services/PaymentService';

const mockTranslate = (key, fallback) => fallback || key;
let mockPlans;
let mockSubscription;

jest.mock('react-i18next', () => ({
  useTranslation: () => ({ t: mockTranslate }),
}));

jest.mock('../../layouts/user/UserLayout', () => ({ children }) => <div>{children}</div>);

jest.mock('../../services/PaymentService', () => ({
  __esModule: true,
  default: {
    createPayment: jest.fn(),
    verifyPaymentResult: jest.fn(),
  },
}));

jest.mock('../../utils/notification', () => ({
  __esModule: true,
  default: { error: jest.fn(), success: jest.fn() },
}));

jest.mock('../../routes/navigation', () => ({ navigate: jest.fn() }));

describe('PackagesPage subscription transitions', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockPlans = [];
    mockSubscription = { planCode: 'FREE', rewardPoints: 0 };
    localStorage.setItem('token', 'test-token');
    window.matchMedia = jest.fn().mockReturnValue({
      matches: false,
      addEventListener: jest.fn(),
      removeEventListener: jest.fn(),
    });
    window.history.replaceState({}, '', '/user/packages');
    global.fetch = jest.fn(async (url) => ({
      ok: true,
      json: async () => {
        if (String(url).includes('subscription-plans')) return mockPlans;
        if (String(url).includes('subscription/me')) return mockSubscription;
        if (String(url).includes('InterviewSession/quota')) {
          return { planName: mockSubscription.planCode === 'PREMIUM' ? 'Premium' : 'Free' };
        }
        return {};
      },
    }));
  });

  afterEach(() => {
    localStorage.clear();
    delete global.fetch;
  });

  test('still asks the backend to verify a failed MoMo redirect', async () => {
    paymentService.verifyPaymentResult.mockRejectedValueOnce(new Error('Payment was rejected.'));
    window.history.replaceState(
      {},
      '',
      '/user/packages?orderId=ORDER-FAILED&resultCode=1006&message=Rejected'
    );

    render(<PackagesPage />);

    await waitFor(() => {
      expect(paymentService.verifyPaymentResult).toHaveBeenCalledWith('ORDER-FAILED', '1006');
    });
  });

  test('warns that a higher plan code immediately replaces the current plan', async () => {
    mockSubscription = {
      planCode: 'PREMIUM',
      planName: 'Premium',
      planId: 2,
      priceId: 1,
      billingCycle: '1',
      maxInterviewQuota: 15,
      remainingInterviewQuota: 8,
      rewardPoints: 0,
    };
    mockPlans = [
      {
        planId: 2,
        code: 'PREMIUM',
        name: 'Premium',
        interviewQuota: 15,
        quotaResetDays: 30,
        isFree: false,
        prices: [{ priceId: 1, billingCycle: 1, amount: 59000 }],
      },
      {
        planId: 3,
        code: 'VIP',
        name: 'VIP',
        interviewQuota: 30,
        quotaResetDays: 30,
        isFree: false,
        prices: [{ priceId: 3, billingCycle: 1, amount: 99000 }],
      },
    ];
    window.history.replaceState({}, '', '/user/packages?purchase=true');

    render(<PackagesPage />);

    const vipHeading = await screen.findByRole('heading', { name: 'VIP 1 Tháng' });
    const vipCard = vipHeading.closest('article');
    fireEvent.click(vipCard.querySelector('button'));

    expect(await screen.findByText('Gói cao hơn sẽ thay thế gói hiện tại ngay lập tức')).toBeInTheDocument();
    expect(screen.getByText(/Thời gian và quota còn lại/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Xác nhận thay thế và thanh toán' })).toBeInTheDocument();
  });
});
