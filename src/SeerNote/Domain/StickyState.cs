using System;

namespace SeerNote.Domain
{
    public sealed class StickyState
    {
        public StickyState()
        {
            Width = 360;
            Height = 260;
        }

        public bool IsOpen { get; set; }

        public double Left { get; set; }

        public double Top { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public StickyState Clone()
        {
            return new StickyState
            {
                IsOpen = IsOpen,
                Left = Left,
                Top = Top,
                Width = Width,
                Height = Height
            };
        }

        public bool TryValidate(out string error)
        {
            if (Double.IsNaN(Left) || Double.IsInfinity(Left) ||
                Double.IsNaN(Top) || Double.IsInfinity(Top) ||
                Double.IsNaN(Width) || Double.IsInfinity(Width) ||
                Double.IsNaN(Height) || Double.IsInfinity(Height))
            {
                error = "Sticky window bounds must be finite numbers.";
                return false;
            }

            if (Width <= 0 || Height <= 0)
            {
                error = "Sticky window bounds must have positive dimensions.";
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
