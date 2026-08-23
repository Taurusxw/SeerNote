using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace SeerNote.Platform
{
    public enum ClipboardFailure
    {
        None,
        WrongThreadApartment,
        ClipboardBusy,
        UnexpectedError
    }

    public sealed class ClipboardResult
    {
        private ClipboardResult(bool succeeded, ClipboardFailure failure, Exception exception)
        {
            Succeeded = succeeded;
            Failure = failure;
            Exception = exception;
        }

        public bool Succeeded { get; private set; }

        public ClipboardFailure Failure { get; private set; }

        public Exception Exception { get; private set; }

        internal static ClipboardResult Success()
        {
            return new ClipboardResult(true, ClipboardFailure.None, null);
        }

        internal static ClipboardResult Failed(ClipboardFailure failure, Exception exception)
        {
            return new ClipboardResult(false, failure, exception);
        }
    }

    /// <summary>
    /// Copies Unicode text with a small retry window for transient clipboard contention.
    /// </summary>
    public sealed class ClipboardService
    {
        private const int AttemptCount = 3;
        private const int RetryDelayMilliseconds = 35;

        public ClipboardResult TrySetText(string text)
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                return ClipboardResult.Failed(ClipboardFailure.WrongThreadApartment, null);
            }

            string value = text ?? string.Empty;
            ExternalException busyException = null;
            for (int attempt = 0; attempt < AttemptCount; attempt++)
            {
                try
                {
                    Clipboard.SetText(value, TextDataFormat.UnicodeText);
                    return ClipboardResult.Success();
                }
                catch (ExternalException exception)
                {
                    busyException = exception;
                    if (attempt + 1 < AttemptCount)
                    {
                        Thread.Sleep(RetryDelayMilliseconds);
                    }
                }
                catch (Exception exception)
                {
                    return ClipboardResult.Failed(ClipboardFailure.UnexpectedError, exception);
                }
            }

            return ClipboardResult.Failed(ClipboardFailure.ClipboardBusy, busyException);
        }
    }
}
