using PdfiumViewer;
using PdfSharp.Drawing;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms.Integration;
using System.Diagnostics;
using System;

namespace CadEye.Lib
{
    public class Pdf_ium_Viewer
    {

        public byte[] RenderPdfPage(string path)
        {
            try
            {
                byte[] resource = File.ReadAllBytes(path);
                return resource;
            }
            catch
            {
                Debug.WriteLine($"RenderPdfPage : Error");
                return null;
            }
        }
        private MemoryStream _pdfStream;
        private PdfRenderer _pdfRenderer;
        public WindowsFormsHost Pdf_Created(byte[] pdfBytes)
        {
            try
            {
                var host = new WindowsFormsHost
                {
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                    VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                    Background = System.Windows.Media.Brushes.Transparent,
                };

                _pdfRenderer = new PdfRenderer
                {
                    Dock = System.Windows.Forms.DockStyle.Fill
                };

                _pdfStream = new MemoryStream(pdfBytes);
                var pdfDocument = PdfiumViewer.PdfDocument.Load(_pdfStream);
                _pdfRenderer.Load(pdfDocument);

                host.Child = _pdfRenderer;
                return host;
            }
            catch
            {
                Debug.WriteLine($"Pdf_Created : Error");
                return null;
            }
        }
        private const int RenderWidth = 2000;
        public List<Point> GetDifferences(byte[] targetBytes, byte[] sourceBytes)
        {
            using (var targetStream = new MemoryStream(targetBytes))
            using (var sourceStream = new MemoryStream(sourceBytes))
            using (var targetPdf = PdfiumViewer.PdfDocument.Load(targetStream))
            using (var sourcePdf = PdfiumViewer.PdfDocument.Load(sourceStream))
            {
                // PDF 페이지 종횡비 유지
                int targetRenderHeight = (int)(RenderWidth * targetPdf.PageSizes[0].Height / targetPdf.PageSizes[0].Width);
                int sourceRenderHeight = (int)(RenderWidth * sourcePdf.PageSizes[0].Height / sourcePdf.PageSizes[0].Width);

                using (var targetBmp = targetPdf.Render(0, RenderWidth, targetRenderHeight, true))
                using (var sourceBmp = sourcePdf.Render(0, RenderWidth, sourceRenderHeight, true))
                {
                    var targetBitmap = new Bitmap(targetBmp);
                    var sourceBitmap = new Bitmap(sourceBmp);

                    // 차이 좌표 반환 + AnnotatePdf에 사용할 실제 Bitmap 크기 전달
                    var differences = CompareBitmaps(targetBitmap, sourceBitmap);

                    // 필요 시 Bitmap 크기를 같이 반환하거나 AnnotatePdf 호출 시 전달
                    AnnotatedBitmapWidth = targetBitmap.Width;
                    AnnotatedBitmapHeight = targetBitmap.Height;

                    return differences;
                }
            }
        }
        public int AnnotatedBitmapWidth { get; private set; }
        public int AnnotatedBitmapHeight { get; private set; }
        public List<Point> CompareBitmaps(Bitmap bmp1, Bitmap bmp2)
        {
            var differences = new List<Point>();

            for (int y = 0; y < bmp1.Height; y += 2)
            {
                for (int x = 0; x < bmp1.Width; x += 2)
                {
                    var c1 = bmp1.GetPixel(x, y);
                    var c2 = bmp2.GetPixel(x, y);

                    if (c1 != c2)
                        differences.Add(new Point(x, y));
                }
            }
            return differences;
        }
        public byte[] AnnotatePdf(byte[] originalPdfBytes, List<Point> differences, int bitmapWidth, int bitmapHeight)
        {
            using (var inputStream = new MemoryStream(originalPdfBytes))
            using (var outputStream = new MemoryStream())
            {
                var pdfDoc = PdfSharp.Pdf.IO.PdfReader.Open(inputStream, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Modify);
                var page = pdfDoc.Pages[0];
                var gfx = XGraphics.FromPdfPage(page);

                double pageWidth = page.Width.Point;
                double pageHeight = page.Height.Point;
                double scaleX = pageWidth / bitmapWidth;
                double scaleY = pageHeight / bitmapHeight;

                var pen = new XPen(XColors.Red, 0.5);

                foreach (var pt in differences)
                {
                    double pdfX = pt.X * scaleX;
                    double pdfY = pt.Y * scaleY;

                    gfx.DrawRectangle(pen, pdfX - 2, pdfY - 2, 4, 4);
                }

                pdfDoc.Save(outputStream);
                return outputStream.ToArray();
            }
        }
    }
}
