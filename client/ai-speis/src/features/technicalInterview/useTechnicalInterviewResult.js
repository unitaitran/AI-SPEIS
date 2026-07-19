import { useCallback, useEffect, useRef, useState } from 'react';
import technicalInterviewApi from '../../services/technicalInterviewApi';

export default function useTechnicalInterviewResult(sessionId) {
  const [result, setResult] = useState(null);
  const [isLoading, setIsLoading] = useState(Boolean(sessionId));
  const [error, setError] = useState(null);
  const requestIdRef = useRef(0);

  const load = useCallback(async () => {
    if (!sessionId) {
      setIsLoading(false);
      return;
    }
    const requestId = requestIdRef.current + 1;
    requestIdRef.current = requestId;
    setIsLoading(true);
    setError(null);
    try {
      const response = await technicalInterviewApi.getResult(sessionId);
      if (requestIdRef.current === requestId) setResult(response?.result || response);
    } catch (requestError) {
      if (requestIdRef.current === requestId) setError(requestError);
    } finally {
      if (requestIdRef.current === requestId) setIsLoading(false);
    }
  }, [sessionId]);

  useEffect(() => {
    load();
    return () => {
      requestIdRef.current += 1;
    };
  }, [load]);

  return { result, isLoading, error, reload: load };
}

