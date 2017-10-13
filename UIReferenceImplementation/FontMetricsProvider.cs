using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using MyScript.IInk.Graphics;
using MyScript.IInk.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Windows.Foundation;

namespace UIReferenceImplementation
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

        public Rectangle[] GetCharacterBoundingBoxes(Text text, TextSpan[] spans)
        {
            var label = text.Label;
            List<Rectangle> rects = new List<Rectangle>();
            Style firstStyle = spans.First().Style;

            TextElementEnumerator myTEE = StringInfo.GetTextElementEnumerator(label);
            CanvasTextFormat textFormat = new CanvasTextFormat()
            {
                FontSize = mm2px(firstStyle.FontSize, dpiY),
            };

            var firstChar = label.FirstOrDefault();
            textFormat.FontFamily = firstStyle.FontFamily;

            int previousIndex = 0;
            CanvasTextLayout canvasTextLayout = new CanvasTextLayout(CanvasDevice.GetSharedDevice(), label, textFormat, float.MaxValue, float.MaxValue);

            int beginCharPosition = 0;
            int charCount = label.Length - 1;
            // Construct the string with different styles
            for (int i = 0; i < spans.Length; ++i)
            {
                Style style = spans[i].Style;
                if (spans.Length > 0)
                {
                    var interval = spans.ElementAt(i);
                    beginCharPosition = interval.BeginPosition;
                    charCount = (int)(interval.EndPosition - interval.BeginPosition) + 1;
                }
                Windows.UI.Text.FontWeight fontWeight = new Windows.UI.Text.FontWeight();
                fontWeight.Weight = (ushort)style.FontWeight;
                Windows.UI.Text.FontStyle fontStyle = Windows.UI.Text.FontStyle.Normal;
                if (style.FontStyle == "italic")
                    fontStyle = Windows.UI.Text.FontStyle.Italic;
                else if (style.FontStyle == "oblique")
                    fontStyle = Windows.UI.Text.FontStyle.Oblique;
                canvasTextLayout.SetFontSize(beginCharPosition, charCount, mm2px(firstStyle.FontSize, dpiY));
                canvasTextLayout.SetFontWeight(beginCharPosition, charCount, fontWeight);
                canvasTextLayout.SetFontStyle(beginCharPosition, charCount, fontStyle);
                canvasTextLayout.SetFontFamily(beginCharPosition, charCount, style.FontFamily);
            }

            float baseline = canvasTextLayout.LineMetrics[0].Baseline;
            while (myTEE.MoveNext())
            {
                CanvasTextLayout canvasCharLayout = new CanvasTextLayout(CanvasDevice.GetSharedDevice(), myTEE.GetTextElement(), textFormat, 2000, 1000);
                canvasCharLayout.SetFontSize(0, 1, canvasTextLayout.GetFontSize(myTEE.ElementIndex));
                canvasCharLayout.SetFontStyle(0, 1, canvasTextLayout.GetFontStyle(myTEE.ElementIndex));
                canvasCharLayout.SetFontWeight(0, 1, canvasTextLayout.GetFontWeight(myTEE.ElementIndex));
                canvasCharLayout.SetFontFamily(0, 1, canvasTextLayout.GetFontFamily(myTEE.ElementIndex));
                Rect drawCharBounds = canvasCharLayout.DrawBounds;

                Vector2 v2 = canvasTextLayout.GetCaretPosition(myTEE.ElementIndex, false);
                float newX = (float)drawCharBounds.X + v2.X;
                float newY = (float)drawCharBounds.Y - baseline;

                //Need to do little translation to clip to the line
                rects.Add(new Rectangle(px2mm(newX, dpiX), px2mm(newY, dpiY), px2mm((float)drawCharBounds.Width, dpiX), px2mm((float)drawCharBounds.Height, dpiY)));
                previousIndex = myTEE.ElementIndex;
            }
            return rects.ToArray();
        }

        public float GetFontSizePx(Style style)
        {
            return style.FontSize;
        }
    };
}