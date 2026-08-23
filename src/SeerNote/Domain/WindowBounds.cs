using System;

namespace SeerNote.Domain
{
    public sealed class WindowBounds
    {
        public WindowBounds()
        {
            Left = 160;
            Top = 100;
            Width = 1080;
            Height = 720;
        }

        public double Left { get; set; }

        public double Top { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public WindowBounds Clone()
        {
            return new WindowBounds
            {
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
                error = "Window bounds must be finite numbers.";
                return false;
            }

            if (Width <= 0 || Height <= 0)
            {
                error = "Window bounds must have positive dimensions.";
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
