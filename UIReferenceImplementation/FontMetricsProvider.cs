// Copyright @ MyScript. All rights reserved.

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Text;
using MyScript.IInk.Graphics;
using MyScript.IInk.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MyScript.IInk.UIReferenceImplementation
{
    internal static class FontFamilies
    {
        public static string MyScriptInter      { get { return _myScriptInter ?? _defaultFamily; }      private set { _myScriptInter = value; } }
        public static string MyScriptInterBold  { get { return _myScriptInterBold ?? _defaultFamily; }  private set { _myScriptInterBold = value; } }
        public static string StixRegular        { get { return _stixRegular ?? _defaultStixFamily; }    private set { _stixRegular = value; } }
        public static string StixItalic         { get { return _stixItalic ?? _defaultStixFamily; }     private set { _stixItalic = value; } }

        private const string _defaultFamily = "Segoe UI";
        private const string _defaultStixFamily = "STIX";

        private const string _fontFolder = "fonts";

        private static bool _initialized = false;
        private static string _myScriptInter;
        private static string _myScriptInterBold;
        private static string _stixRegular;
        private static string _stixItalic;

        private static string RegisterFontFamily(string filename, string name, string defaultFamily)
        {
            var localPath = System.IO.Path.Combine(_fontFolder, filename);
            var fullPath = System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, localPath);

            if (System.IO.File.Exists(fullPath))
                return "ms-appx:///" + localPath + "#" + name;

            return defaultFamily;
        }

        public static void Initialize()
        {
            if (_initialized)
                return;

            MyScriptInter = RegisterFontFamily("MyScriptInter-Regular.otf", "MyScriptInter", _defaultFamily);
            MyScriptInterBold = RegisterFontFamily("MyScriptInter-Bold.otf", "MyScriptInter", _defaultFamily);
            StixRegular = RegisterFontFamily("STIXTwoMath-Regular.otf", "STIX Two Math", _defaultStixFamily);
            StixItalic = RegisterFontFamily("STIXTwoText-Italic.otf", "STIX Two Text", _defaultStixFamily);

            _initialized = true;
        }
    }

    public class FontMetricsProvider : IFontMetricsProvider
    {
        public const bool UseColorFont = true;

        private float _dpiX;
        private float _dpiY;

        public static void Initialize()
        {
            FontFamilies.Initialize();
        }

        public FontMetricsProvider(float dpiX, float dpiY)
        {
            _dpiX = dpiX;
            _dpiY = dpiY;
        }

        public static string ToPlatformFontFamily(string family, string style, int weight)
        {
            if (family == "MyScriptInter")
                return ToPlatformFontWeight(weight).Equals(Windows.UI.Text.FontWeights.Bold) ? FontFamilies.MyScriptInterBold : FontFamilies.MyScriptInter;
            else if (family == "STIX")
                return (ToPlatformFontStyle(style) == Windows.UI.Text.FontStyle.Italic) ? FontFamilies.StixItalic : FontFamilies.StixRegular;

            return family;
        }

        public static Windows.UI.Text.FontWeight ToPlatformFontWeight(int weight)
        {
            var fontWeight = Windows.UI.Text.FontWeights.Normal;

            if (weight >= 700)
                fontWeight = Windows.UI.Text.FontWeights.Bold;
            else if (weight >= 400)
                fontWeight = Windows.UI.Text.FontWeights.Normal;
            else
                fontWeight = Windows.UI.Text.FontWeights.Light;

            return fontWeight;
        }

        public static Windows.UI.Text.FontStyle ToPlatformFontStyle(string style)
        {
            var fontStyle = Windows.UI.Text.FontStyle.Normal;

            if (style.Equals("italic"))
                fontStyle = Windows.UI.Text.FontStyle.Italic;
            else if (style.Equals("oblique"))
                fontStyle = Windows.UI.Text.FontStyle.Oblique;

            return fontStyle;
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
            var fontFamily = ToPlatformFontFamily(style.FontFamily, style.FontStyle, style.FontWeight);
            var fontSize = mm2px(style.FontSize, _dpiY);
            var fontWeight = ToPlatformFontWeight(style.FontWeight);
            var fontStyle = ToPlatformFontStyle(style.FontStyle);
            return new FontKey(fontFamily, fontSize, fontWeight, fontStyle);
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
                    FontWeight = fontKey.FontWeight,
                    WordWrapping = CanvasWordWrapping.NoWrap,
                    Options = UseColorFont ? CanvasDrawTextOptions.EnableColorFont : CanvasDrawTextOptions.Default
                };

                using (var canvasCharLayout = new CanvasTextLayout(canvasDevice, glyphLabel, textFormat, 0.0f, 0.0f))
                {
                    int charCount = 0;
                    if (canvasCharLayout.ClusterMetrics != null)
                    {
                        foreach (var c in canvasCharLayout.ClusterMetrics)
                            charCount += c.CharacterCount;
                    }

                    var rect = canvasCharLayout.DrawBounds;
                    var left = (float)rect.Left;
                    var top = (float)rect.Top - canvasCharLayout.LineMetrics[0].Baseline;
                    var leftBearing = (float)(-rect.Left);
                    var height = (float)rect.Height;

                    var charEnd = (charCount > 0) ? (charCount - 1) : 0;
                    var advance = canvasCharLayout.GetCaretPosition(charEnd, true);
                    var width = (float)rect.Width;
                    var right = (float)rect.Right;
                    var rightBearing = (float)advance.X - right;

                    var glyphX = px2mm(left, _dpiX);
                    var glyphY = px2mm(top, _dpiY);
                    var glyphW = px2mm(width, _dpiX);
                    var glyphH = px2mm(height, _dpiY);
                    var glyphRect = new Rectangle(glyphX, glyphY, glyphW, glyphH);
                    var glyphLeftBearing = px2mm(leftBearing, _dpiX);
                    var glyphRightBearing = px2mm(rightBearing, _dpiX);

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
                    FontSize = firstFontKey.FontSize,
                    FontFamily = firstFontKey.FontFamily,
                    FontStyle = firstFontKey.FontStyle,
                    FontWeight = firstFontKey.FontWeight,
                    WordWrapping = CanvasWordWrapping.NoWrap,
                    Options = UseColorFont ? CanvasDrawTextOptions.EnableColorFont : CanvasDrawTextOptions.Default
                };

                using (var canvasTextLayout = new CanvasTextLayout(canvasDevice, text.Label, textFormat, 0.0f, 0.0f))
                {
                    for (int i = 0; i < spans.Length; ++i)
                    {
                        var charIndex = text.GetGlyphBeginAt(spans[i].BeginPosition);
                        var charCount = text.GetGlyphEndAt(spans[i].EndPosition - 1) - charIndex;

                        var style = spans[i].Style;
                        var fontKey = FontKeyFromStyle(style);

                        canvasTextLayout.SetFontFamily(charIndex, charCount, fontKey.FontFamily);
                        canvasTextLayout.SetFontSize(charIndex, charCount, fontKey.FontSize);
                        canvasTextLayout.SetFontWeight(charIndex, charCount, fontKey.FontWeight);
                        canvasTextLayout.SetFontStyle(charIndex, charCount, fontKey.FontStyle);
                    }

                    for (int i = 0; i < text.GlyphCount; ++i)
                    {
                        var glyphLabel = text.GetGlyphLabelAt(i);
                        var glyphCharStart = text.GetGlyphBeginAt(i);
                        var glyphCharEnd = text.GetGlyphEndAt(i);

                        var glyphFontKey = new FontKey  ( canvasTextLayout.GetFontFamily(glyphCharStart)
                                                        , canvasTextLayout.GetFontSize(glyphCharStart)
                                                        , canvasTextLayout.GetFontWeight(glyphCharStart)
                                                        , canvasTextLayout.GetFontStyle(glyphCharStart));

                        var glyphMetrics_ = GetGlyphMetrics(glyphFontKey, glyphLabel, canvasDevice);

                        // Find cluster associated with element
                        // (Use of ClusterMetrics to identify ligatures in the CanvasTextLayout)
                        int cluster = -1;
                        int clusterCharStart = 0;

                        if (canvasTextLayout.ClusterMetrics != null)
                        {
                            for (int c = 0; c < canvasTextLayout.ClusterMetrics.Length; ++c)
                            {
                                var clusterCharCount = canvasTextLayout.ClusterMetrics[c].CharacterCount;
                                if ((glyphCharStart >= clusterCharStart) && (glyphCharStart < (clusterCharStart + clusterCharCount)))
                                {
                                    cluster = c;
                                    break;
                                }

                                clusterCharStart += clusterCharCount;
                            }
                        }

                        if ( (i > 0) && (cluster >= 0) && (glyphCharStart > clusterCharStart) )
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
                            float glyphX = 0.0f;
                            var charRegions = canvasTextLayout.GetCharacterRegions(glyphCharStart, glyphCharEnd - glyphCharStart);

                            if ((charRegions != null) && (charRegions.Length > 0))
                            {
                                glyphX = (float)charRegions[0].LayoutBounds.X;
                            }
                            else
                            {
                                var glyphPos = canvasTextLayout.GetCaretPosition(glyphCharStart, false);
                                glyphX = (float)glyphPos.X;
                            }

                            glyphMetrics_.BoundingBox.X += px2mm(glyphX, _dpiX);
                        }

                        glyphMetrics[i] = glyphMetrics_;
                    }
                }
            }

            return glyphMetrics;
        }
   };
}
