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

        private class FontKey
        {
            public string FontFamily { get; }
            public float FontSize { get; }
            public Windows.UI.Text.FontWeight FontWeight { get; }
            public Windows.UI.Text.FontStyle FontStyle { get; }

            public FontKey(string fontFamily, float fontSize, Windows.UI.Text.FontWeight fontWeight, Windows.UI.Text.FontStyle fontStyle)
            {
                this.FontFamily = fontFamily;
                this.FontSize = fontSize;
                this.FontWeight = fontWeight;
                this.FontStyle = fontStyle;
            }

            public override bool Equals(object obj)
            {
                if (obj.GetType() != this.GetType())
                    return false;

                FontKey other = (FontKey)obj;
                return this.FontFamily == other.FontFamily && this.FontSize == other.FontSize && this.FontWeight.Weight == other.FontWeight.Weight && this.FontStyle == other.FontStyle;
            }

            public override int GetHashCode()
            {
                return FontFamily.GetHashCode() ^ FontSize.GetHashCode() ^ FontWeight.Weight.GetHashCode() ^ FontStyle.GetHashCode();
            }
        }
        private Dictionary<FontKey, Dictionary<string, GlyphMetrics>> cache = new Dictionary<FontKey, Dictionary<string, GlyphMetrics>>();

        private FontKey FontKeyFromStyle(Style style)
        {
            var fontSize = mm2px(style.FontSize, dpiY);
            var fontWeight = new Windows.UI.Text.FontWeight() {  Weight = (ushort)style.FontWeight };
            var fontStyle = Windows.UI.Text.FontStyle.Normal;

            if (style.FontStyle == "italic")
                fontStyle = Windows.UI.Text.FontStyle.Italic;
            else if (style.FontStyle == "oblique")
                fontStyle = Windows.UI.Text.FontStyle.Oblique;

            return new FontKey(style.FontFamily, fontSize, fontWeight, fontStyle);
        }

        private GlyphMetrics GetGlyphMetrics(FontKey fontKey, string glyphLabel, CanvasDevice canvasDevice)
        {
            Dictionary<string, GlyphMetrics> fontCache = null;
            if (!cache.TryGetValue(fontKey, out fontCache))
            {
                fontCache = new Dictionary<string, GlyphMetrics>();
                cache[fontKey] = fontCache;
            }

            GlyphMetrics value = null;
            if (!fontCache.TryGetValue(glyphLabel, out value))
            {
                var textFormat = new CanvasTextFormat()
                {
                    FontSize = fontKey.FontSize,
                    FontFamily = fontKey.FontFamily,
                    FontStyle = fontKey.FontStyle,
                    FontWeight = fontKey.FontWeight
                };

                using (var canvasCharLayout = new CanvasTextLayout(canvasDevice, glyphLabel, textFormat, 10000, 10000))
                {
                    canvasCharLayout.SetFontFamily(0, 1, fontKey.FontFamily);
                    canvasCharLayout.SetFontSize(0, 1, fontKey.FontSize);
                    canvasCharLayout.SetFontWeight(0, 1, fontKey.FontWeight);
                    canvasCharLayout.SetFontStyle(0, 1, fontKey.FontStyle);

                    var charRect = canvasCharLayout.DrawBounds;
                    var charX = (float)charRect.X;
                    var charY = (float)charRect.Y - canvasCharLayout.LineMetrics[0].Baseline;
                    var charLeftBearing = (float)(-charRect.Left);
                    var charAdvance = canvasCharLayout.GetCaretPosition(0, true);
                    var charRightBearing = (float)(charAdvance.X - charRect.Right);

                    var glyphX = px2mm(charX, dpiX);
                    var glyphY = px2mm(charY, dpiY);
                    var glyphW = px2mm((float)charRect.Width, dpiX);
                    var glyphH = px2mm((float)charRect.Height, dpiY);
                    var glyphRect = new Rectangle(glyphX, glyphY, glyphW, glyphH);
                    var glyphLeftBearing = px2mm(charLeftBearing, dpiX);
                    var glyphRightBearing = px2mm(charRightBearing, dpiX);

                    value = new GlyphMetrics(glyphRect, glyphLeftBearing, glyphRightBearing);
                    fontCache[glyphLabel] = value;
                }
            }

            return new GlyphMetrics(new Rectangle(value.BoundingBox.X, value.BoundingBox.Y, value.BoundingBox.Width, value.BoundingBox.Height), value.LeftSideBearing, value.RightSideBearing);
        }

        public Rectangle[] GetCharacterBoundingBoxes(MyScript.IInk.Text.Text text, TextSpan[] spans)
        {
            var glyphMetrics = GetGlyphMetrics(text, spans);

            Rectangle[] rectangles = new Rectangle[glyphMetrics.Length];
            for (int i = 0; i < glyphMetrics.Length; ++i)
                rectangles[i] = glyphMetrics[i].BoundingBox;

            return rectangles;
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
            CanvasDevice canvasDevice = CanvasDevice.GetSharedDevice();

            GlyphMetrics[] glyphMetrics = new GlyphMetrics[text.GlyphCount];

            var firstStyle = spans[0].Style;
            var firstFontKey = FontKeyFromStyle(firstStyle);

            if (text.GlyphCount == 1)
            {
                glyphMetrics[0] = GetGlyphMetrics(firstFontKey, text.Label, canvasDevice);
            }
            else
            {
                var textFormat = new CanvasTextFormat()
                {
                    FontSize = mm2px(firstStyle.FontSize, dpiY),
                    FontFamily = firstStyle.FontFamily,
                    FontStyle = firstFontKey.FontStyle,
                    FontWeight = firstFontKey.FontWeight
                };

                using (var canvasTextLayout = new CanvasTextLayout(canvasDevice, text.Label, textFormat, 10000, 10000))
                {
                    for (int i = 0; i < spans.Length; ++i)
                    {
                        var charIndex = spans[i].BeginPosition;
                        var charCount = spans[i].EndPosition - spans[i].BeginPosition;

                        var style = spans[i].Style;
                        var fontKey = FontKeyFromStyle(style);

                        canvasTextLayout.SetFontFamily(charIndex, charCount, fontKey.FontFamily);
                        canvasTextLayout.SetFontSize(charIndex, charCount, fontKey.FontSize);
                        canvasTextLayout.SetFontWeight(charIndex, charCount, fontKey.FontWeight);
                        canvasTextLayout.SetFontStyle(charIndex, charCount, fontKey.FontStyle);
                    }

                    // Use of TextElementEnumerator to get character indices as in the CanvasTextLayout
                    var tee = StringInfo.GetTextElementEnumerator(text.Label);

                    // Use of ClusterMetrics to identify ligatures in the CanvasTextLayout
                    int cluster = 0;
                    int clusterStartChar = 0;
                    var clusterCharCount = canvasTextLayout.ClusterMetrics[cluster].CharacterCount;

                    for (int i = 0, g = 0; i < text.GlyphCount; ++i)
                    {
                        var fontKey = new FontKey   ( canvasTextLayout.GetFontFamily(i)
                                                    , canvasTextLayout.GetFontSize(i)
                                                    , canvasTextLayout.GetFontWeight(i)
                                                    , canvasTextLayout.GetFontStyle(i));
                        var glyphLabel = text.GetGlyphLabelAt(i);
                        var glyphMetrics_ = GetGlyphMetrics(fontKey, glyphLabel, canvasDevice);

                        // Find cluster associated to element
                        if (tee.MoveNext())
                            g = tee.ElementIndex;

                        while ( (g < clusterStartChar) || (g >= (clusterStartChar + clusterCharCount)) )
                        {
                            ++cluster;
                            clusterStartChar += clusterCharCount;
                            clusterCharCount = canvasTextLayout.ClusterMetrics[cluster].CharacterCount;
                        }

                        if (g > clusterStartChar)
                        {
                            // Ligature with the previous glyph
                            // The position is not accurate because of glyphs substitution at rendering
                            // but it makes the illusion.
                            var prevGlyphMetrics = glyphMetrics[i-1];
                            glyphMetrics_.BoundingBox.X = prevGlyphMetrics.BoundingBox.X
                                                        + prevGlyphMetrics.BoundingBox.Width
                                                        + prevGlyphMetrics.RightSideBearing
                                                        + glyphMetrics_.LeftSideBearing;
                        }
                        else
                        {
                            var charPos = canvasTextLayout.GetCaretPosition(g, false);
                            glyphMetrics_.BoundingBox.X += px2mm(charPos.X, dpiX);
                        }

                        glyphMetrics[i] = glyphMetrics_;
                    }
                }
            }

            return glyphMetrics;
        }
   };
}
