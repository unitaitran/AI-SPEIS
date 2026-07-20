import { useEffect, useRef } from 'react';

const FOCUSABLE_SELECTOR = [
  'button:not([disabled])',
  '[href]',
  'input:not([disabled])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])',
].join(',');

export default function useAccessibleDialog({ isOpen, onClose, closeDisabled = false }) {
  const dialogRef = useRef(null);

  useEffect(() => {
    if (!isOpen) return undefined;
    const previouslyFocused = document.activeElement;
    const dialog = dialogRef.current;
    const focusable = () => [...(dialog?.querySelectorAll(FOCUSABLE_SELECTOR) || [])];
    window.requestAnimationFrame(() => focusable()[0]?.focus());

    const handleKeyDown = (event) => {
      if (event.key === 'Escape' && !closeDisabled) {
        event.preventDefault();
        onClose?.();
        return;
      }
      if (event.key !== 'Tab') return;

      const elements = focusable();
      if (elements.length === 0) {
        event.preventDefault();
        dialog?.focus();
        return;
      }

      const first = elements[0];
      const last = elements[elements.length - 1];
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('keydown', handleKeyDown);
      previouslyFocused?.focus?.();
    };
  }, [closeDisabled, isOpen, onClose]);

  return dialogRef;
}
