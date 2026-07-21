import { ENDPOINTS } from '../config/api';

const getToken = () => {
  const token = localStorage.getItem('token');
  if (!token) {
    throw new Error('Authentication token not found');
  }
  return token;
};

const parseJsonResponse = async (response) => {
  if (!response.ok) {
    const contentType = response.headers.get('Content-Type') || '';
    const body = contentType.includes('application/json')
      ? await response.json()
      : { message: await response.text() };

    const message = body?.message || body?.Message || body?.title || body?.Title || 'Payment request failed';
    throw new Error(message);
  }

  return response.json();
};

const paymentService = {
  createPayment: async (packageId = 1) => {
    const token = getToken();
    const response = await fetch(ENDPOINTS.PAYMENT_CREATE, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${token}`,
        'Content-Type': 'application/json',
        Accept: 'application/json',
      },
      body: JSON.stringify({ packageId }),
    });

    return parseJsonResponse(response);
  },

  checkPayment: async (orderCode) => {
    if (!orderCode) {
      throw new Error('Missing order code');
    }

    const token = getToken();
    const response = await fetch(ENDPOINTS.PAYMENT_CHECK(orderCode), {
      method: 'GET',
      headers: {
        Authorization: `Bearer ${token}`,
        Accept: 'application/json',
      },
    });

    return parseJsonResponse(response);
  },

  verifyPaymentResult: async (orderId, resultCode = null) => {
    if (!orderId) {
      throw new Error('Missing order ID');
    }

    let url = ENDPOINTS.PAYMENT_VERIFY(orderId);
    if (resultCode !== null && resultCode !== undefined) {
      url += `&resultCode=${encodeURIComponent(resultCode)}`;
    }

    const response = await fetch(url, {
      method: 'GET',
      headers: {
        Accept: 'application/json',
      },
    });

    return parseJsonResponse(response);
  }
};

export default paymentService;
