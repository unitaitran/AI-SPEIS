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

  // Handle empty responses (e.g. 204 No Content)
  const contentType = response.headers.get('Content-Type') || '';
  if (response.status === 204 || !contentType.includes('application/json')) {
    return null;
  }

  return response.json();
};

const cvService = {
  /** GET /api/CVFile/MyCV */
  getMyCV: async () => {
    const response = await fetch(ENDPOINTS.CV_GET_MY, {
      headers: { ...getAuthHeaders(), Accept: 'application/json' },
    });
    return handleResponse(response);
  },

  /** GET /api/CVFile/history */
  getMyCVHistory: async (pageNumber = 1, pageSize = 10) => {
    const response = await fetch(`${ENDPOINTS.CV_GET_HISTORY}?PageNumber=${pageNumber}&PageSize=${pageSize}`, {
      headers: { ...getAuthHeaders(), Accept: 'application/json' },
    });
    return handleResponse(response);
  },

  /** POST /api/CVFile/upload (multipart) */
  uploadCV: async (file) => {
    const formData = new FormData();
    formData.append('file', file);
    const response = await fetch(ENDPOINTS.CV_UPLOAD, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: formData,
    });
    return handleResponse(response);
  },

  /** DELETE /api/CVFile/{id} */
  deleteCV: async (id) => {
    const response = await fetch(ENDPOINTS.CV_DELETE(id), {
      method: 'DELETE',
      headers: getAuthHeaders(),
    });
    return handleResponse(response);
  },

  /** POST /api/CVFile/{id}/parse — trigger AI analysis */
  triggerParse: async (id) => {
    const response = await fetch(ENDPOINTS.CV_PARSE(id), {
      method: 'POST',
      headers: { ...getAuthHeaders(), 'Content-Type': 'application/json' },
    });
    return handleResponse(response);
  },

  /** GET /api/CVFile/{id}/status — poll processing status */
  getParseStatus: async (id) => {
    const response = await fetch(ENDPOINTS.CV_STATUS(id), {
      headers: { ...getAuthHeaders(), Accept: 'application/json' },
    });
    return handleResponse(response);
  },

  /** GET /api/CVFile/{id}/parsed-data — retrieve extracted data */
  getParsedData: async (id) => {
    const response = await fetch(ENDPOINTS.CV_PARSED_DATA(id), {
      headers: { ...getAuthHeaders(), Accept: 'application/json' },
    });
    return handleResponse(response);
  },

  /** PUT /api/CVFile/{id}/confirm — confirm (optionally edited) data */
  confirmParsedData: async (id, data) => {
    const response = await fetch(ENDPOINTS.CV_CONFIRM(id), {
      method: 'PUT',
      headers: {
        ...getAuthHeaders(),
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(data),
    });
    return handleResponse(response);
  },
};

export default cvService;
