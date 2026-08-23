using System;
using System.Collections.Generic;
using SeerNote.Domain;

namespace SeerNote.Storage
{
    public sealed class LoadResult
    {
        internal LoadResult(AppState state, RecoveryReport recovery, Exception error)
        {
            State = state;
            Recovery = recovery ?? new RecoveryReport();
            Error = error;
        }

        public AppState State { get; private set; }
        public RecoveryReport Recovery { get; private set; }
        public Exception Error { get; private set; }
        public bool Success { get { return State != null && Error == null; } }
    }

    public sealed class SaveResult
    {
        internal SaveResult(string path, DateTime savedUtc, Exception error)
        {
            Path = path;
            SavedUtc = savedUtc;
            Error = error;
        }

        public string Path { get; private set; }
        public DateTime SavedUtc { get; private set; }
        public Exception Error { get; private set; }
        public bool Success { get { return Error == null; } }
    }

    public sealed class RecoveryReport
    {
        private readonly List<string> _diagnostics = new List<string>();

        public bool Recovered { get; internal set; }
        public string SourcePath { get; internal set; }
        public string PreservedCorruptPath { get; internal set; }
        public IList<string> Diagnostics { get { return _diagnostics.AsReadOnly(); } }

        internal void AddDiagnostic(string message)
        {
            _diagnostics.Add(message);
        }
    }
}
