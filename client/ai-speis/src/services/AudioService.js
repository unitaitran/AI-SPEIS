import { ENDPOINTS } from '../config/api';

const getAuthHeaders = () => {
  const token = localStorage.getItem('token');
  if (!token) throw new Error('Authentication token not found');
  return { Authorization: `Bearer ${token}` };
};

const audioService = {
  checkSpeechToText: async (audioBlob, languageCode = 'vi-VN') => {
    const formData = new FormData();
    formData.append('audioFile', audioBlob, 'record.webm');
    formData.append('languageCode', languageCode);

    const response = await fetch(ENDPOINTS.AUDIO_SPEECH_TO_TEXT, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: formData,
    });

    if (!response.ok) {
      let errorMessage = `Request failed with status ${response.status}`;
      try {
        const errorData = await response.json();
        errorMessage = errorData.Message || errorData.message || errorMessage;
      } catch (e) {}
      throw new Error(errorMessage);
    }
    return response.json();
  }
};

export default audioService;
