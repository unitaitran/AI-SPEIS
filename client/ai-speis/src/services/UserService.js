import { ENDPOINTS } from '../config/api';

const statusMap = {
  active: true,
  locked: false,
};

const roleMap = {
  student: 'user',
  user: 'user',
  admin: 'admin',
};

const sortByMap = {
  fullName: 'FullName',
  email: 'Email',
  role: 'Role',
  registerDate: 'CreatedAt',
  status: 'Status',
};

const sortDirectionMap = {
  asc: 'Asc',
  desc: 'Desc',
};

const buildQueryString = (params) => {
  const query = new URLSearchParams();

  if (params.page != null) {
    query.set('PageNumber', String(params.page));
  }

  if (params.pageSize != null) {
    query.set('PageSize', String(params.pageSize));
  }

  if (params.search) {
    query.set('Search', params.search);
  }

  if (params.role) {
    const normalizedRole = roleMap[params.role.toLowerCase()];
    if (normalizedRole) {
      query.set('Role', normalizedRole);
    }
  }

  if (params.status) {
    const normalizedStatus = statusMap[params.status.toLowerCase()];
    if (normalizedStatus !== undefined) {
      query.set('Status', String(normalizedStatus));
    }
  }

  if (params.sortBy) {
    const mappedSortBy = sortByMap[params.sortBy];
    if (mappedSortBy) {
      query.set('SortBy', mappedSortBy);
    }
  }

  if (params.sortOrder) {
    const mappedDirection = sortDirectionMap[params.sortOrder];
    if (mappedDirection) {
      query.set('SortDirection', mappedDirection);
    }
  }

  return query.toString();
};

const parseJsonResponse = async (response) => {
  if (!response.ok) {
    const contentType = response.headers.get('Content-Type') || '';
    const body = contentType.includes('application/json')
      ? await response.json()
      : { message: await response.text() };

    const message = body?.message || body?.Title || body?.title || JSON.stringify(body);
    throw new Error(message || `Request failed with status ${response.status}`);
  }

  return response.json();
};

export const userService = {
  getUsers: async (params = {}) => {
    const queryString = buildQueryString(params);
    const url = `${ENDPOINTS.ADMIN_USERS}${queryString ? `?${queryString}` : ''}`;
    const token = localStorage.getItem('token');

    if (!token) {
      throw new Error('Authentication token not found');
    }

    const response = await fetch(url, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${token}`,
        'Accept': 'application/json',
      },
    });

    return parseJsonResponse(response);
  },

  getUserById: async (userId) => {
    if (!userId) {
      throw new Error('Missing user ID');
    }

    const url = `${ENDPOINTS.ADMIN_USERS}/${userId}`;
    const token = localStorage.getItem('token');

    if (!token) {
      throw new Error('Authentication token not found');
    }

    const response = await fetch(url, {
      method: 'GET',
      headers: {
        'Authorization': `Bearer ${token}`,
        'Accept': 'application/json',
      },
    });

    return parseJsonResponse(response);
  },

  getUserDetail(userId) {
    return this.getUserById(userId);
  },

  lockUser: async (userId, reason = '') => {
    if (!userId) {
      throw new Error('Missing user ID');
    }

    const url = ENDPOINTS.ADMIN_USER_LOCK(userId);
    const token = localStorage.getItem('token');

    if (!token) {
      throw new Error('Authentication token not found');
    }

    const response = await fetch(url, {
      method: 'PATCH',
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ reason }),
    });

    return parseJsonResponse(response);
  },

  unlockUser: async (userId) => {
    if (!userId) {
      throw new Error('Missing user ID');
    }

    const url = ENDPOINTS.ADMIN_USER_UNLOCK(userId);
    const token = localStorage.getItem('token');

    if (!token) {
      throw new Error('Authentication token not found');
    }

    const response = await fetch(url, {
      method: 'PATCH',
      headers: {
        'Authorization': `Bearer ${token}`,
      },
    });

    return parseJsonResponse(response);
  },

  assignRole: async () => {
    throw new Error('Assign role is not supported by the current backend API');
  },

  assignPackage: async () => {
    throw new Error('Assign package is not supported by the current backend API');
  },

  batchLockUsers: async () => {
    throw new Error('Batch lock is not supported by the current backend API');
  },

  batchAssignPackage: async () => {
    throw new Error('Batch assign package is not supported by the current backend API');
  },
};

export default userService;

