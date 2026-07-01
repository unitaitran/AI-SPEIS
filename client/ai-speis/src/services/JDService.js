import { ENDPOINTS } from '../config/api';

const getAuthHeaders = () => {
  const token = localStorage.getItem('token');
  if (!token) throw new Error('Authentication token not found');
  return { Authorization: `Bearer ${token}` };
};

const handleResponse = async (response) => {
  if (!response.ok) {
    const contentType = response.headers.get('Content-Type') || '';
    let message = `Request failed with status ${response.status}`;
    if (contentType.includes('application/json')) {
      const body = await response.json().catch(() => ({}));
      message = body?.message || body?.Message || message;
    }
    throw new Error(message);
  }
  return response.json();
};

const jdService = {
  /** GET /api/JDFile/history */
  getMyJDHistory: async (pageNumber = 1, pageSize = 5) => {
    const response = await fetch(`${ENDPOINTS.JD_GET_HISTORY}?PageNumber=${pageNumber}&PageSize=${pageSize}`, {
      headers: { ...getAuthHeaders(), Accept: 'application/json' },
    });
    return handleResponse(response);
  },

  /** POST /api/JDFile/upload (multipart) */
  uploadJD: async (file) => {
    const formData = new FormData();
    formData.append('file', file);
    const response = await fetch(ENDPOINTS.JD_UPLOAD, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: formData,
    });
    return handleResponse(response);
  },

  /** POST /api/JDFile/text */
  submitJDText: async (rawText) => {
    const response = await fetch(ENDPOINTS.JD_SUBMIT_TEXT, {
      method: 'POST',
      headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
      body: JSON.stringify({ rawText }),
    });
    return handleResponse(response);
  },

  /** DELETE /api/JDFile/{id} */
  deleteJD: async (id) => {
    const response = await fetch(ENDPOINTS.JD_DELETE(id), {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    return handleResponse(response);
  },
};

export default jdService;
