using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace SeerNote.Platform
{
    /// <summary>
    /// Owns the process-wide mutex and relays activation requests from later launches.
    /// </summary>
    public sealed class SingleInstanceGuard : IDisposable
    {
        private readonly Mutex _mutex;
        private readonly EventWaitHandle _activationEvent;
        private readonly RegisteredWaitHandle _activationWait;
        private readonly FileStream _lockFile;
        private bool _disposed;

        private SingleInstanceGuard(Mutex mutex, EventWaitHandle activationEvent, FileStream lockFile)
        {
            _mutex = mutex;
            _activationEvent = activationEvent;
            _lockFile = lockFile;
            _activationWait = ThreadPool.RegisterWaitForSingleObject(
                _activationEvent,
                OnActivationRequested,
                null,
                Timeout.Infinite,
                false);
        }

        public event EventHandler ActivationRequested;

        public static bool TryAcquire(string applicationId, out SingleInstanceGuard guard)
        {
            return TryAcquire(applicationId, null, out guard);
        }

        /// <summary>
        /// Attempts to acquire the named instance mutex and, when supplied, an exclusive lock file.
        /// The file lock protects the data directory when two executable-directory aliases produce
        /// different named mutex identities.
        /// </summary>
        public static bool TryAcquire(string applicationId, string lockFilePath, out SingleInstanceGuard guard)
        {
            if (string.IsNullOrWhiteSpace(applicationId))
            {
                throw new ArgumentException("An application identifier is required.", nameof(applicationId));
            }

            string objectPrefix = "Local\\SeerNote." + CreateStableName(applicationId);
            bool ownsMutex = false;
            Mutex mutex = null;
            EventWaitHandle activationEvent = null;
            FileStream lockFile = null;

            try
            {
                mutex = new Mutex(true, objectPrefix + ".Instance", out ownsMutex);
                activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, objectPrefix + ".Activate");

                if (!ownsMutex)
                {
                    activationEvent.Set();
                    activationEvent.Dispose();
                    mutex.Dispose();
                    guard = null;
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(lockFilePath))
                {
                    try
                    {
                        lockFile = new FileStream(lockFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    }
                    catch (IOException exception) when (IsSharingViolation(exception))
                    {
                        activationEvent.Dispose();
                        mutex.ReleaseMutex();
                        mutex.Dispose();
                        guard = null;
                        return false;
                    }
                }

                guard = new SingleInstanceGuard(mutex, activationEvent, lockFile);
                return true;
            }
            catch
            {
                if (lockFile != null)
                {
                    lockFile.Dispose();
                }

                if (activationEvent != null)
                {
                    activationEvent.Dispose();
                }

                if (mutex != null)
                {
                    if (ownsMutex)
                    {
                        mutex.ReleaseMutex();
                    }

                    mutex.Dispose();
                }

                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _activationWait.Unregister(null);
            _activationEvent.Dispose();
            if (_lockFile != null)
            {
                _lockFile.Dispose();
            }
            _mutex.ReleaseMutex();
            _mutex.Dispose();
            ActivationRequested = null;
        }

        private void OnActivationRequested(object state, bool timedOut)
        {
            if (_disposed || timedOut)
            {
                return;
            }

            EventHandler handler = ActivationRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private static string CreateStableName(string applicationId)
        {
            byte[] source = Encoding.UTF8.GetBytes(applicationId);
            using (SHA256 hash = SHA256.Create())
            {
                byte[] digest = hash.ComputeHash(source);
                StringBuilder builder = new StringBuilder(digest.Length * 2);
                for (int index = 0; index < digest.Length; index++)
                {
                    builder.Append(digest[index].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        public static string GetDirectoryIdentity(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("A directory path is required.", nameof(directoryPath));
            }

            string fullPath = Path.GetFullPath(directoryPath);
            string root = Path.GetPathRoot(fullPath);
            while (fullPath.Length > root.Length && IsDirectorySeparator(fullPath[fullPath.Length - 1]))
            {
                fullPath = fullPath.Substring(0, fullPath.Length - 1);
            }

            return fullPath.ToUpperInvariant();
        }

        private static bool IsSharingViolation(IOException exception)
        {
            const int SharingViolation = 32;
            return (exception.HResult & 0xffff) == SharingViolation;
        }

        private static bool IsDirectorySeparator(char value)
        {
            return value == Path.DirectorySeparatorChar || value == Path.AltDirectorySeparatorChar;
        }
    }
}
