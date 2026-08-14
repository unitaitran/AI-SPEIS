import { useCallback, useRef, useState } from 'react';
import technicalV2InterviewApi from '../../services/technicalV2InterviewApi';

const initialState = { status: 'IDLE', isCompleted: false, error: null };

export default function useTechnicalPreGenerator() {
  const [state, setState] = useState(initialState);
  const controllerRef = useRef(null);

  const trigger = useCallback(async (sessionId) => {
    if (!sessionId || controllerRef.current) return;
    const controller = new AbortController();
    controllerRef.current = controller;
    setState({ status: 'PREPARING', isCompleted: false, error: null });
    try {
      await technicalV2InterviewApi.initialize(sessionId, undefined, { signal: controller.signal });
      setState({ status: 'COMPLETED', isCompleted: true, error: null });
    } catch (error) {
      if (error?.name !== 'AbortError') {
        setState({ status: 'FAILED', isCompleted: false, error });
      }
    } finally {
      controllerRef.current = null;
    }
  }, []);

  const cancel = useCallback(() => {
    controllerRef.current?.abort();
    controllerRef.current = null;
    setState(initialState);
  }, []);

  return { ...state, trigger, cancel };
}
