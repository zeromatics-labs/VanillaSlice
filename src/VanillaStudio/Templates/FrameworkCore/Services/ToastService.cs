namespace {{ProjectName}}.Framework.Services
{
    public class ToastService
    {
        private readonly List<ToastMessage> _toasts = new();

        public IEnumerable<ToastMessage> Toasts => _toasts;

        public event Action? OnToastsChanged;

        public void ShowSuccess(string message, int durationMs = 3000)
            => Show(message, ToastType.Success, durationMs);

        public void ShowError(string message, int durationMs = 5000)
            => Show(message, ToastType.Error, durationMs);

        public void ShowWarning(string message, int durationMs = 4000)
            => Show(message, ToastType.Warning, durationMs);

        public void ShowInfo(string message, int durationMs = 3000)
            => Show(message, ToastType.Info, durationMs);

        public void Show(string message, ToastType type = ToastType.Info, int durationMs = 3000)
        {
            var toast = new ToastMessage
            {
                Id = Guid.NewGuid(),
                Message = message,
                Type = type
            };

            _toasts.Add(toast);
            OnToastsChanged?.Invoke();

            _ = Task.Delay(durationMs).ContinueWith(_ => Dismiss(toast.Id));
        }

        public void Dismiss(Guid id)
        {
            var toast = _toasts.FirstOrDefault(t => t.Id == id);
            if (toast != null)
            {
                _toasts.Remove(toast);
                OnToastsChanged?.Invoke();
            }
        }
    }

    public class ToastMessage
    {
        public Guid Id { get; set; }
        public string Message { get; set; } = string.Empty;
        public ToastType Type { get; set; }
    }

    public enum ToastType
    {
        Success,
        Error,
        Warning,
        Info
    }
}
