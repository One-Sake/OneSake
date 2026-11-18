interface ToastProps {
  message: string;
}

// Short status message at the bottom of the screen. The parent hides it after a moment.
export function Toast({ message }: ToastProps) {
  if (!message) {
    return null;
  }

  return (
    <div className="ui-toast" role="status" aria-live="polite">
      {message}
    </div>
  );
}
