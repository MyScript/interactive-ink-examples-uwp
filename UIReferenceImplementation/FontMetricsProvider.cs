// Copyright MyScript. All right reserved.

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using MyScript.IInk.Graphics;
using MyScript.IInk.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MyScript.IInk.UIReferenceImplementation
{
    public class FontMetricsProvider : IFontMetricsProvider2
    {
        private float dpiX;
        private float dpiY;

        public FontMetricsProvider(float dpiX, float dpiY)
        {
            this.dpiX = dpiX;
            this.dpiY = dpiY;
        }

        private static float px2mm(float px, float dpi)
        {
            return 25.4f * (px / dpi);
        }

        private static float mm2px(float mmm, float dpi)
        {
            return (mmm / 25.4f) * dpi;
        }

        private static List<GlyphMetrics> GetGlyphMetrics_(MyScript.IInk.Text.Text text, TextSpan[] spans, CanvasDevice canvasDevice, float dpiX, float dpiY)
        {
            var glyphMetrics = new List<GlyphMetrics>();

            var label = text.Label;
            var firstStyle = spans.First().Style;

            var textFormat = new CanvasTextFormat()
            {
                FontSize = mm2px(firstStyle.FontSize, dpiY),
            };

            var firstChar = label.FirstOrDefault();
            textFormat.FontFamily = firstStyle.FontFamily;

            var canvasTextLayout = new CanvasTextLayout(canvasDevice, label, textFormat, float.MaxValue, float.MaxValue);

            for (var i = 0; i < spans.Length; ++i)
            {
                var style = spans[i].Style;
                var interval = spans.ElementAt(i);
                var beginCharPosition = interval.BeginPosition;
                var charCount = (int)(interval.EndPosition - interval.BeginPosition) + 1;

                var fontWeight = new Windows.UI.Text.FontWeight();
                fontWeight.Weight = (ushort)style.FontWeight;

                var fontStyle = Windows.UI.Text.FontStyle.Normal;
                if (style.FontStyle == "italic")
                    fontStyle = Windows.UI.Text.FontStyle.Italic;
                else if (style.FontStyle == "oblique")
                    fontStyle = Windows.UI.Text.FontStyle.Oblique;

                canvasTextLayout.SetFontSize(beginCharPosition, charCount, mm2px(firstStyle.FontSize, dpiY));
                canvasTextLayout.SetFontWeight(beginCharPosition, charCount, fontWeight);
                canvasTextLayout.SetFontStyle(beginCharPosition, charCount, fontStyle);
                canvasTextLayout.SetFontFamily(beginCharPosition, charCount, style.FontFamily);
            }

            var baseline = canvasTextLayout.LineMetrics[0].Baseline;
            var myTEE = StringInfo.GetTextElementEnumerator(label);
            while (myTEE.MoveNext())
            {
                var canvasCharLayout = new CanvasTextLayout(canvasTextLayout.Device, myTEE.GetTextElement(), textFormat, 2000, 1000);
                canvasCharLayout.SetFontSize(0, 1, canvasTextLayout.GetFontSize(myTEE.ElementIndex));
                canvasCharLayout.SetFontStyle(0, 1, canvasTextLayout.GetFontStyle(myTEE.ElementIndex));
                canvasCharLayout.SetFontWeight(0, 1, canvasTextLayout.GetFontWeight(myTEE.ElementIndex));
                canvasCharLayout.SetFontFamily(0, 1, canvasTextLayout.GetFontFamily(myTEE.ElementIndex));

                var charRect = canvasCharLayout.DrawBounds;
                var charPos = canvasTextLayout.GetCaretPosition(myTEE.ElementIndex, false);
                var charX = (float)charRect.X + charPos.X;
                var charY = (float)charRect.Y - baseline;
                var charLeftBearing = (float)(-charRect.Left);
                var charRightBearing = 0.0f;

                var glyphX = px2mm(charX, dpiX);
                var glyphY = px2mm(charY, dpiY);
                var glyphW = px2mm((float)charRect.Width, dpiX);
                var glyphH = px2mm((float)charRect.Height, dpiY);
                var glyphRect = new Rectangle(glyphX, glyphY, glyphW, glyphH);
                var glyphLeftBearing = px2mm(charLeftBearing, dpiX);
                var glyphRightBearing = px2mm(charRightBearing, dpiX);

                glyphMetrics.Add(new GlyphMetrics(glyphRect, glyphLeftBearing, glyphRightBearing));
            }

            return glyphMetrics;
        }

        public Rectangle[] GetCharacterBoundingBoxes(MyScript.IInk.Text.Text text, TextSpan[] spans)
        {
            var glyphMetrics = GetGlyphMetrics_(text, spans, CanvasDevice.GetSharedDevice(), dpiX, dpiY);
            var rectangles = new List<Rectangle>();

            foreach (var metrics in glyphMetrics)
                rectangles.Add(metrics.BoundingBox);

            return rectangles.ToArray();
        }

        public float GetFontSizePx(Style style)
        {
            return style.FontSize;
        }

        public bool SupportsGlyphMetrics()
        {
            return true;
        }

        public GlyphMetrics[] GetGlyphMetrics(MyScript.IInk.Text.Text text, TextSpan[] spans)
        {
            var glyphMetrics = GetGlyphMetrics_(text, spans, CanvasDevice.GetSharedDevice(), dpiX, dpiY);
            return glyphMetrics.ToArray();
        }
   };
}