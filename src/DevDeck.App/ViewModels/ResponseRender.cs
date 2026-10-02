using System.Runtime.Versioning;
using Avalonia.Media.Imaging;
using PDFtoImage;
using SkiaSharp;
using Svg.Skia;

namespace DevDeck.App.ViewModels
{
    /// <summary>
    ///  Draws the response kinds Avalonia cannot draw on its own: SVG, and PDF page by page.
    /// </summary>
    /// <remarks>
    ///  Everything here is slow by UI standards - a PDF page is tens of milliseconds, a large SVG
    ///  can be more - so it is only ever called from a background thread. The results are plain
    ///  bitmaps, which Avalonia is happy to receive from any thread.
    ///
    ///  Both renderers draw onto Skia, the same engine Avalonia draws with, and hand back PNG
    ///  bytes decoded as an Avalonia bitmap. Encoding to hand over is a copy, but it keeps the two
    ///  libraries from sharing native objects across their lifetimes, which is where the crashes
    ///  in this kind of bridging always come from.
    /// </remarks>
    internal static class ResponseRender
    {
        /// <summary>
        ///  The longest side an SVG is drawn at.
        /// </summary>
        /// <remarks>
        ///  SVG has no size of its own worth trusting - many declare a 24x24 icon box, others none
        ///  at all - so it is scaled to something worth looking at and never to more than this, so a
        ///  drawing that declares itself 100,000 units wide does not ask for a gigabyte of pixels.
        /// </remarks>
        private const float SvgSide = 1200;

        /// <summary>
        ///  Most PDF pages drawn. A preview is for checking it is the right document; Open hands it
        ///  to a real viewer for the rest.
        /// </summary>
        public const int MostPdfPages = 25;

        public static Bitmap Svg(byte[] bytes)
        {
            using SKSvg svg = new();
            using MemoryStream source = new(bytes);

            if (svg.Load(source) is not { } picture)
            {
                throw new InvalidOperationException("The SVG did not load.");
            }

            SKRect bounds = picture.CullRect;

            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                throw new InvalidOperationException("The SVG has no size to draw at.");
            }

            float scale = SvgSide / Math.Max(bounds.Width, bounds.Height);

            // Small icons come up to a viewable size; large drawings come down to the limit.
            scale = Math.Clamp(scale, 0.05f, 16f);

            int width = Math.Max(1, (int)Math.Ceiling(bounds.Width * scale));
            int height = Math.Max(1, (int)Math.Ceiling(bounds.Height * scale));

            using SKBitmap target = new(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (SKCanvas canvas = new(target))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.Scale(scale);
                canvas.Translate(-bounds.Left, -bounds.Top);
                canvas.DrawPicture(picture);
            }

            return ToBitmap(target);
        }

        /// <summary>The first pages of a PDF, and how many it has in all.</summary>
        /// <remarks>PDFium ships for the three desktop platforms the app does, and no others.</remarks>
        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        public static (List<Bitmap> Pages, int Total) Pdf(byte[] bytes)
        {
            int total = Conversion.GetPageCount(bytes);
            List<Bitmap> pages = [];

            // Rendered at a resolution that reads well at the pane's usual width, with the white
            // page background a PDF assumes - a transparent page on a dark theme is black text on
            // black.
            RenderOptions options = new(Dpi: 110, BackgroundColor: SKColors.White, WithAnnotations: true, WithFormFill: true);

            for (int page = 0; page < Math.Min(total, MostPdfPages); page++)
            {
                using SKBitmap drawn = Conversion.ToImage(bytes, page: page, options: options);
                pages.Add(ToBitmap(drawn));
            }

            return (pages, total);
        }

        private static Bitmap ToBitmap(SKBitmap drawn)
        {
            using SKData encoded = drawn.Encode(SKEncodedImageFormat.Png, 100);
            using MemoryStream stream = new(encoded.ToArray());

            return new Bitmap(stream);
        }
    }
}
