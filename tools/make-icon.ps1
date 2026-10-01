# Draws DevDeck's mark - the same one the splash screen shows - and writes it out as the app icon
# (src/DevDeck.App/Assets/DevDeck.ico) and the README's icon (docs/images/icon.png).
#
#   pwsh tools/make-icon.ps1
#
# The splash draws the mark live in the theme's colours. An icon cannot follow the theme, so this
# draws it once in the app's own steel and brass - the Steel Dark palette - with GDI+, which every
# Windows machine has, rather than needing an image editor to change it.
#
# Small sizes are not the big one shrunk. At 16 and 24 pixels the terminal dots, the prompt and the
# chain are a smudge, so those sizes keep only the tile and DD, drawn heavier.

param([string]$Root = (Split-Path $PSScriptRoot -Parent))

Add-Type -AssemblyName System.Drawing

$ink    = [System.Drawing.Color]::FromArgb(255, 0x12, 0x16, 0x1B)
$panel  = [System.Drawing.Color]::FromArgb(255, 0x16, 0x1C, 0x24)
$code   = [System.Drawing.Color]::FromArgb(255, 0x0E, 0x12, 0x17)
$accent = [System.Drawing.Color]::FromArgb(255, 0xC8, 0xA1, 0x5A)
$soft   = [System.Drawing.Color]::FromArgb(255, 0xD8, 0xB2, 0x6C)
$text   = [System.Drawing.Color]::FromArgb(255, 0xDC, 0xE3, 0xEA)

function Rounded([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

function Alpha([System.Drawing.Color]$c, [int]$a) { [System.Drawing.Color]::FromArgb($a, $c.R, $c.G, $c.B) }

# The mark at $size pixels, on a transparent ground.
function Draw([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.Clear([System.Drawing.Color]::Transparent)

    $small = $size -le 24
    $s = [float]$size

    # A margin for the glow on the larger sizes; the small ones use the whole square.
    $m = if ($small) { 0.5 } else { $s * 0.06 }
    $w = $s - 2 * $m
    $r = $w * 0.23

    # The glow: the tile's outline, widening and fading outwards.
    if (-not $small) {
        for ($i = 6; $i -ge 1; $i--) {
            $pen = New-Object System.Drawing.Pen (Alpha $accent (14 + 4 * (6 - $i))), ($s * 0.012 * $i)
            $g.DrawPath($pen, (Rounded $m $m $w $w $r))
            $pen.Dispose()
        }
    }

    # The tile.
    $tile = Rounded $m $m $w $w $r
    $g.FillPath((New-Object System.Drawing.SolidBrush $panel), $tile)
    $border = [Math]::Max(1.0, $s * ($(if ($small) { 0.08 } else { 0.022 })))
    $g.DrawPath((New-Object System.Drawing.Pen $accent, $border), $tile)

    # DD.
    $family = 'Segoe UI Black'
    if (-not ([System.Drawing.FontFamily]::Families | Where-Object Name -eq $family)) { $family = 'Arial Black' }
    $fontSize = if ($small) { $w * 0.52 } else { $w * 0.36 }
    $font = New-Object System.Drawing.Font $family, $fontSize, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $format = New-Object System.Drawing.StringFormat
    $format.Alignment = 'Center'
    $format.LineAlignment = 'Center'
    $centre = if ($small) { $m + $w * 0.5 } else { $m + $w * 0.42 }
    $box = New-Object System.Drawing.RectangleF ($m - $w), ($centre - $w * 0.5), ($w * 3), $w
    if (-not $small) {
        $shadow = New-Object System.Drawing.RectangleF ($box.X + $s * 0.012), ($box.Y + $s * 0.012), $box.Width, $box.Height
        $g.DrawString('DD', $font, (New-Object System.Drawing.SolidBrush (Alpha $soft 90)), $shadow, $format)
    }
    $g.DrawString('DD', $font, (New-Object System.Drawing.SolidBrush $accent), $box, $format)

    if (-not $small) {
        # The terminal's title bar: three dots, and the prompt.
        $dot = $w * 0.045
        $top = $m + $w * 0.09
        foreach ($k in 0..2) {
            $brush = New-Object System.Drawing.SolidBrush (Alpha $accent (255 - 70 * $k))
            $g.FillEllipse($brush, $m + $w * 0.1 + $k * $dot * 1.7, $top, $dot, $dot)
        }
        $prompt = New-Object System.Drawing.Font 'Consolas', ($w * 0.085), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
        $right = New-Object System.Drawing.StringFormat
        $right.Alignment = 'Far'
        $right.LineAlignment = 'Center'
        $g.DrawString('>_', $prompt, (New-Object System.Drawing.SolidBrush $accent),
            (New-Object System.Drawing.RectangleF $m, ($top - $w * 0.05), ($w * 0.9), ($dot + $w * 0.1)), $right)

        # The deck: three layers, the front one brightest, joined by the chain.
        $lw = $w * 0.58
        $lh = $w * 0.1
        $lx = $m + ($w - $lw) / 2
        $ly = $m + $w * 0.66
        $step = $w * 0.075
        $line = [Math]::Max(1.0, $s * 0.011)
        foreach ($k in 0..2) {
            $layer = Rounded $lx ($ly + $k * $step) $lw $lh ($lh * 0.35)
            $g.FillPath((New-Object System.Drawing.SolidBrush $code), $layer)
            $g.DrawPath((New-Object System.Drawing.Pen (Alpha $accent (130 + 60 * $k)), $line), $layer)
        }
        $cx = $m + $w / 2
        $g.DrawLine((New-Object System.Drawing.Pen (Alpha $text 220), ($s * 0.009)), $cx, ($ly + $lh * 0.5), $cx, ($ly + 2 * $step + $lh * 0.5))
        $r2 = $w * 0.022
        foreach ($k in 0..2) {
            $g.FillEllipse((New-Object System.Drawing.SolidBrush $text), $cx - $r2, $ly + $k * $step + $lh * 0.5 - $r2, 2 * $r2, 2 * $r2)
        }
    }

    $g.Dispose()
    return $bmp
}

function Png([System.Drawing.Bitmap]$bmp) {
    $stream = New-Object System.IO.MemoryStream
    $bmp.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    return , $stream.ToArray()
}

# An .ico holding one PNG per size, which Windows has read since Vista.
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = foreach ($size in $sizes) { , (Png (Draw $size)) }

$ico = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $ico
$writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $edge = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$edge); $writer.Write([byte]$edge); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([UInt16]1); $writer.Write([UInt16]32)
    $writer.Write([UInt32]$images[$i].Length); $writer.Write([UInt32]$offset)
    $offset += $images[$i].Length
}
foreach ($image in $images) { $writer.Write($image) }
$writer.Flush()

[System.IO.File]::WriteAllBytes((Join-Path $Root 'src/DevDeck.App/Assets/DevDeck.ico'), $ico.ToArray())
(Draw 256).Save((Join-Path $Root 'docs/images/icon.png'), [System.Drawing.Imaging.ImageFormat]::Png)

"Wrote DevDeck.ico ($($sizes -join ', ') px) and docs/images/icon.png"
