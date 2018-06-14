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
    public class FontMetricsProvider : IFontMetricsProvider
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

        public Rectangle[] GetCharacterBoundingBoxes(MyScript.IInk.Text.Text text, TextSpan[] spans)
        {
            var label = text.Label;
            var rects = new List<Rectangle>();
            var firstStyle = spans.First().Style;

            var myTEE = StringInfo.GetTextElementEnumerator(label);
            var textFormat = new CanvasTextFormat()
            {
                FontSize = mm2px(firstStyle.FontSize, dpiY),
            };

            var firstChar = label.FirstOrDefault();
            textFormat.FontFamily = firstStyle.FontFamily;

            var canvasTextLayout = new CanvasTextLayout(CanvasDevice.GetSharedDevice(), label, textFormat, float.MaxValue, float.MaxValue);

            var beginCharPosition = 0;
            var charCount = label.Length - 1;
            // Construct the string with different styles
            for (var i = 0; i < spans.Length; ++i)
            {
                var style = spans[i].Style;
                if (spans.Length > 0)
                {
                    var interval = spans.ElementAt(i);
                    beginCharPosition = interval.BeginPosition;
                    charCount = (int)(interval.EndPosition - interval.BeginPosition) + 1;
                }
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
            while (myTEE.MoveNext())
            {
                var canvasCharLayout = new CanvasTextLayout(CanvasDevice.GetSharedDevice(), myTEE.GetTextElement(), textFormat, 2000, 1000);
                canvasCharLayout.SetFontSize(0, 1, canvasTextLayout.GetFontSize(myTEE.ElementIndex));
                canvasCharLayout.SetFontStyle(0, 1, canvasTextLayout.GetFontStyle(myTEE.ElementIndex));
                canvasCharLayout.SetFontWeight(0, 1, canvasTextLayout.GetFontWeight(myTEE.ElementIndex));
                canvasCharLayout.SetFontFamily(0, 1, canvasTextLayout.GetFontFamily(myTEE.ElementIndex));
                var drawCharBounds = canvasCharLayout.DrawBounds;

                var v2 = canvasTextLayout.GetCaretPosition(myTEE.ElementIndex, false);
                var newX = (float)drawCharBounds.X + v2.X;
                var newY = (float)drawCharBounds.Y - baseline;

                //Need to do little translation to clip to the line
                rects.Add(new Rectangle(px2mm(newX, dpiX), px2mm(newY, dpiY), px2mm((float)drawCharBounds.Width, dpiX), px2mm((float)drawCharBounds.Height, dpiY)));
            }
            return rects.ToArray();
        }

        public float GetFontSizePx(Style style)
        {
            return style.FontSize;
        }
    };
}