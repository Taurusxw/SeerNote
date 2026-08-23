using System;

namespace SeerNote.Domain
{
    public sealed class Entry
    {
        public Entry()
        {
            var now = DateTime.UtcNow;
            Id = Guid.NewGuid();
            Title = String.Empty;
            Body = String.Empty;
            Category = String.Empty;
            Sticky = new StickyState();
            CreatedUtc = now;
            UpdatedUtc = now;
        }

        public Guid Id { get; set; }

        public string Title { get; set; }

        public string Body { get; set; }

        public string Category { get; set; }

        public bool IsFavorite { get; set; }

        public bool IsDeleted { get; set; }

        public StickyState Sticky { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime UpdatedUtc { get; set; }

        public DateTime? DeletedUtc { get; set; }

        public string DisplayTitle
        {
            get { return GetDisplayTitle(); }
        }

        public string GetDisplayTitle()
        {
            if (!String.IsNullOrWhiteSpace(Title))
            {
                return Title.Trim();
            }

            if (!String.IsNullOrEmpty(Body))
            {
                var lines = Body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                foreach (var line in lines)
                {
                    if (!String.IsNullOrWhiteSpace(line))
                    {
                        return line.Trim();
                    }
                }
            }

            return String.Empty;
        }

        public Entry Clone()
        {
            return new Entry
            {
                Id = Id,
                Title = Title,
                Body = Body,
                Category = Category,
                IsFavorite = IsFavorite,
                IsDeleted = IsDeleted,
                Sticky = Sticky == null ? null : Sticky.Clone(),
                CreatedUtc = CreatedUtc,
                UpdatedUtc = UpdatedUtc,
                DeletedUtc = DeletedUtc
            };
        }

        public bool TryValidate(out string error)
        {
            if (Id == Guid.Empty)
            {
                error = "Entry ID is required.";
                return false;
            }

            if (Title == null || Body == null || Category == null)
            {
                error = "Entry text fields cannot be null.";
                return false;
            }

            if (Sticky == null)
            {
                error = "Sticky state is required.";
                return false;
            }

            if (!Sticky.TryValidate(out error))
            {
                return false;
            }

            if (CreatedUtc == DateTime.MinValue || UpdatedUtc == DateTime.MinValue || CreatedUtc > UpdatedUtc)
            {
                error = "Entry timestamps are invalid.";
                return false;
            }

            if (DeletedUtc.HasValue && DeletedUtc.Value == DateTime.MinValue)
            {
                error = "Deleted timestamp is invalid.";
                return false;
            }

            error = null;
            return true;
        }

        public void Validate()
        {
            string error;
            if (!TryValidate(out error))
            {
                throw new InvalidOperationException(error);
            }
        }
    }
}
