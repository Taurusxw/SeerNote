[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$charcoal = [System.Drawing.Color]::FromArgb(255, 23, 40, 42)
$paper = [System.Drawing.Color]::FromArgb(255, 250, 248, 242)
$teal = [System.Drawing.Color]::FromArgb(255, 36, 107, 103)
$gold = [System.Drawing.Color]::FromArgb(255, 215, 166, 74)

function New-ScaledPath {
    param(
        [System.Drawing.Drawing2D.GraphicsPath]$Path,
        [double]$Scale
    )

    $matrix = New-Object System.Drawing.Drawing2D.Matrix
    try {
        $matrix.Scale([single]$Scale, [single]$Scale)
        $Path.Transform($matrix)
        return $Path
    }
    finally {
        $matrix.Dispose()
    }
}

function New-RoundedRectanglePath {
    param(
        [single]$X,
        [single]$Y,
        [single]$Width,
        [single]$Height,
        [single]$Radius
    )

    $diameter = $Radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-SeerNoteBitmap {
    param([int]$Size)

    $scale = $Size / 256.0
    $bitmap = New-Object System.Drawing.Bitmap -ArgumentList @($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $background = New-RoundedRectanglePath 12 12 232 232 52
        $paperPath = New-RoundedRectanglePath 42 34 172 188 28
        $fold = New-Object System.Drawing.Drawing2D.GraphicsPath
        $fold.AddPolygon([System.Drawing.PointF[]]@(
                (New-Object System.Drawing.PointF 166, 34),
                (New-Object System.Drawing.PointF 166, 54),
                (New-Object System.Drawing.PointF 192, 80),
                (New-Object System.Drawing.PointF 214, 80)
            ))

        $linePen = New-Object System.Drawing.Pen $charcoal, ([single](8 * $scale))
        $linePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $linePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $backgroundBrush = New-Object System.Drawing.SolidBrush $charcoal
        $paperBrush = New-Object System.Drawing.SolidBrush $paper
        $tealBrush = New-Object System.Drawing.SolidBrush $teal
        $goldBrush = New-Object System.Drawing.SolidBrush $gold
        try {
            $background = New-ScaledPath $background $scale
            $paperPath = New-ScaledPath $paperPath $scale
            $fold = New-ScaledPath $fold $scale

            $graphics.FillPath($tealBrush, $background)
            $graphics.FillPath($paperBrush, $paperPath)
            $graphics.FillPath($goldBrush, $fold)
            $graphics.FillEllipse($tealBrush, [single](90 * $scale), [single](88 * $scale), [single](76 * $scale), [single](76 * $scale))
            $graphics.FillEllipse($backgroundBrush, [single](114 * $scale), [single](112 * $scale), [single](28 * $scale), [single](28 * $scale))
            $graphics.DrawLine($linePen, [single](84 * $scale), [single](180 * $scale), [single](172 * $scale), [single](180 * $scale))
        }
        finally {
            $linePen.Dispose()
            $backgroundBrush.Dispose()
            $paperBrush.Dispose()
            $tealBrush.Dispose()
            $goldBrush.Dispose()
            $background.Dispose()
            $paperPath.Dispose()
            $fold.Dispose()
        }
    }
    catch {
        $bitmap.Dispose()
        throw
    }
    finally {
        $graphics.Dispose()
    }

    return $bitmap
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = [System.IO.Path]::GetDirectoryName($resolvedOutput)
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    [System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
}

$entries = New-Object System.Collections.Generic.List[object]
try {
    foreach ($size in $sizes) {
        $bitmap = New-SeerNoteBitmap $size
        try {
            $pngStream = New-Object System.IO.MemoryStream
            try {
                $bitmap.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
                $entries.Add($pngStream.ToArray())
            }
            finally {
                $pngStream.Dispose()
            }
        }
        finally {
            $bitmap.Dispose()
        }
    }

    $temporaryOutput = "$resolvedOutput.$([System.Guid]::NewGuid().ToString('N')).tmp"
    $stream = New-Object System.IO.FileStream($temporaryOutput, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write)
    $writer = New-Object System.IO.BinaryWriter($stream)
    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$sizes.Count)

        $offset = 6 + (16 * $sizes.Count)
        for ($index = 0; $index -lt $sizes.Count; $index++) {
            $size = $sizes[$index]
            $dimension = if ($size -eq 256) { 0 } else { $size }
            $writer.Write([byte]$dimension)
            $writer.Write([byte]$dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$entries[$index].Length)
            $writer.Write([UInt32]$offset)
            $offset += $entries[$index].Length
        }

        foreach ($entry in $entries) {
            $writer.Write([byte[]]$entry)
        }
    }
    finally {
        $writer.Dispose()
    }

    Move-Item -LiteralPath $temporaryOutput -Destination $resolvedOutput -Force
}
finally {
    $entries.Clear()
}
