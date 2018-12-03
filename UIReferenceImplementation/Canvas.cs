// Copyright MyScript. All right reserved.

using MyScript.IInk.Graphics;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.Text;
using System;
using System.Numerics;
using System.Collections.Generic;
using Windows.Foundation;

namespace MyScript.IInk.UIReferenceImplementation
{
    public class Canvas : ICanvas
    {
        private CanvasDrawingSession _session;
        private IRenderTarget _target;
        private ImageLoader _imageLoader;

        private Transform _transform;
        private float _strokeWidth { get; set; }
        private Windows.UI.Color _strokeColor { get; set; }
        private CanvasStrokeStyle _strokeStyle { get; set; }
        private Windows.UI.Color _fillColor { get; set; }
        private CanvasFilledRegionDetermination _fillRule { get; set; }
        private CanvasTextFormat _fontProperties { get; set; }
        private Dictionary<string, Rect> _layers;
        private float _baseline;

        private CanvasActiveLayer _activeLayer;
        private Rect _activeLayerRect;

        public Canvas(CanvasDrawingSession session, IRenderTarget target, ImageLoader imageLoader)
        {
            _session = session;
            _target = target;
            _imageLoader = imageLoader;

            _transform = new Transform(1, 0, 0, 0, 1, 0);

            _strokeWidth = 1.0f;
            _strokeColor = Windows.UI.Colors.Transparent;

            _strokeStyle = new CanvasStrokeStyle
            {
                StartCap = CanvasCapStyle.Flat,
                EndCap = CanvasCapStyle.Flat,
                DashCap = CanvasCapStyle.Flat,
                LineJoin = CanvasLineJoin.Miter
            };

            _fillColor = Windows.UI.Colors.Black;
            _fillRule = CanvasFilledRegionDetermination.Winding;

            _fontProperties = new CanvasTextFormat
            {
                FontStyle = Windows.UI.Text.FontStyle.Normal,
                FontWeight = Windows.UI.Text.FontWeights.Normal
            };

            _baseline = 1.0f;

            _layers = new Dictionary<string, Rect>();
            _activeLayer = null;
            _activeLayerRect = Rect.Empty;
        }

        public void Begin()
        {
            _session.Antialiasing = CanvasAntialiasing.Antialiased;
            _session.TextAntialiasing = CanvasTextAntialiasing.Auto;

            var defaultStyle = new Style();
            defaultStyle.SetChangeFlags((uint)StyleFlag.StyleFlag_ALL);
            defaultStyle.ApplyTo(this);
        }

        public void End()
        {
            _session.Flush();
        }

        public void Clear(Color color)
        {
            var color_ = Windows.UI.Color.FromArgb((byte)color.A, (byte)color.R, (byte)color.G, (byte)color.B);
            _session.Clear(color_);
        }

        public Transform Transform
        {
            get { return _transform; }
            set
            {
                _transform = value;
                _session.Transform = new Matrix3x2((float)Transform.XX, (float)Transform.XY,
                                                  (float)Transform.YX, (float)Transform.YY,
                                                  (float)Transform.TX, (float)Transform.TY);
            }
        }

        public void SetStrokeColor(Color color)
        {
            _strokeColor = Windows.UI.Color.FromArgb((byte)color.A, (byte)color.R, (byte)color.G, (byte)color.B);
        }

        public void SetStrokeWidth(float width)
        {
            _strokeWidth = width;
        }

        public void SetStrokeLineCap(LineCap lineCap)
        {
            if (lineCap == LineCap.BUTT)
            {
                _strokeStyle.StartCap = CanvasCapStyle.Flat;
                _strokeStyle.EndCap = CanvasCapStyle.Flat;
                _strokeStyle.DashCap = CanvasCapStyle.Flat;
            }
            else if (lineCap == LineCap.ROUND)
            {
                _strokeStyle.StartCap = CanvasCapStyle.Round;
                _strokeStyle.EndCap = CanvasCapStyle.Round;
                _strokeStyle.DashCap = CanvasCapStyle.Round;
            }
            else if (lineCap == LineCap.SQUARE)
            {
                _strokeStyle.StartCap = CanvasCapStyle.Square;
                _strokeStyle.EndCap = CanvasCapStyle.Square;
                _strokeStyle.DashCap = CanvasCapStyle.Square;
            }
        }

        public void SetStrokeLineJoin(LineJoin lineJoin)
        {
            if (lineJoin == LineJoin.BEVEL)
                _strokeStyle.LineJoin = CanvasLineJoin.Bevel;
            else if (lineJoin == LineJoin.MITER)
                _strokeStyle.LineJoin = CanvasLineJoin.Miter;
            else if (lineJoin == LineJoin.ROUND)
                _strokeStyle.LineJoin = CanvasLineJoin.Round;
        }

        public void SetStrokeMiterLimit(float limit)
        {
            _strokeStyle.MiterLimit = limit;
        }

        public void SetStrokeDashArray(float[] array)
        {
            _strokeStyle.CustomDashStyle = array;
        }

        public void SetStrokeDashOffset(float offset)
        {
            _strokeStyle.DashOffset = offset;
        }

        public void SetFillColor(Color color)
        {
            _fillColor = Windows.UI.Color.FromArgb((byte)color.A, (byte)color.R, (byte)color.G, (byte)color.B);
        }

        public void SetFillRule(FillRule rule)
        {
            if (rule == FillRule.NONZERO)
                _fillRule = CanvasFilledRegionDetermination.Winding;
            else if (rule == FillRule.EVENODD)
                _fillRule = CanvasFilledRegionDetermination.Alternate;
        }

        public void SetFontProperties(string family, float lineHeight, float size, string style, string variant, int weight)
        {
            _fontProperties.FontFamily = family;

            switch (style)
            {
                case "oblique":
                    _fontProperties.FontStyle = Windows.UI.Text.FontStyle.Oblique;
                    break;
                case "italic":
                    _fontProperties.FontStyle = Windows.UI.Text.FontStyle.Italic;
                    break;
                default: // "normal"
                    _fontProperties.FontStyle = Windows.UI.Text.FontStyle.Normal;
                    break;
            }

            if (weight >= 700)
                _fontProperties.FontWeight = Windows.UI.Text.FontWeights.Bold;
            else if (weight >= 400)
                _fontProperties.FontWeight = Windows.UI.Text.FontWeights.Normal;
            else
                _fontProperties.FontWeight = Windows.UI.Text.FontWeights.Light;

            _fontProperties.FontSize = size;

            using (var canvasTextLayout = new CanvasTextLayout(_session.Device, "k", _fontProperties, float.MaxValue, float.MaxValue))
            {
                _baseline = canvasTextLayout.LineMetrics[0].Baseline;
            }
        }

        public void StartGroup(string id, float x, float y, float width, float height, bool clipContent)
        {
            if (clipContent)
            {
                _layers.Add(id, _activeLayerRect);

                if (_activeLayer != null)
                {
                    _activeLayer.Dispose();
                    _activeLayer = null;
                    _activeLayerRect = Rect.Empty;
                }

                if (_session != null)
                {
                    _activeLayerRect = new Rect(x, y, width, height);
                    _activeLayer = _session.CreateLayer(1.0f, _activeLayerRect);
                }
            }
        }

        public void EndGroup(string id)
        {
            if (_layers.ContainsKey(id))
            {
                var rect = _layers[id];

                _layers.Remove(id);

                if (_activeLayer != null)
                {
                    _activeLayer.Dispose();
                    _activeLayer = null;
                    _activeLayerRect = Rect.Empty;
                }

                if (rect.IsEmpty == false)
                {
                    if (_session != null)
                    {
                        _activeLayerRect = rect;
                        _activeLayer = _session.CreateLayer(1.0f, _activeLayerRect);
                    }
                }
            }
        }

        public void StartItem(string id)
        {
        }

        public void EndItem(string id)
        {
        }

        public IPath CreatePath()
        {
            return new Path(_session.Device);
        }

        /// <summary>Draw Path to canvas according path</summary>
        public void DrawPath(IPath path)
        {
            var geometry = ((Path)path).CreateGeometry();

            if (_fillColor.A > 0)
            {
                _session.FillGeometry(geometry, _fillColor);
            }

            if (_strokeColor.A > 0)
            {
                _session.DrawGeometry(geometry, _strokeColor, _strokeWidth, _strokeStyle);
            }
        }



        /// <summary>Draw Rectangle to canvas according to region</summary>
        public void DrawRectangle(float x, float y, float width, float height)
        {
            if (_fillColor.A > 0)
            {
                _session.FillRectangle(x, y, width, height, _fillColor);
            }

            if (_strokeColor.A > 0)
            {
                _session.DrawRectangle(x, y, width, height, _strokeColor, _strokeWidth, _strokeStyle);
            }
        }
        /// <summary>Draw Line to canvas according coordinates</summary>
        public void DrawLine(float x1, float y1, float x2, float y2)
        {
            if (_strokeColor.A > 0)
            {
                _session.DrawLine(x1, y1, x2, y2, _strokeColor, _strokeWidth, _strokeStyle);
            }
        }

        public void DrawObject(string url, string mimeType, float x, float y, float width, float height)
        {
            if (_imageLoader == null)
                return;

            var transform = _transform;
            var screenMin = transform.Apply(x, y);
            var screenMax = transform.Apply(x + width, y + height);

            var image = _imageLoader.getImage(  url, mimeType,
                                                (url_, image_) =>
                                                {
                                                    var renderer = _imageLoader.Editor.Renderer;
                                                    var x_ = (int)Math.Floor(screenMin.X);
                                                    var y_ = (int)Math.Floor(screenMin.Y);
                                                    var width_ = (int)Math.Ceiling(screenMax.X) - x_;
                                                    var height_ = (int)Math.Ceiling(screenMax.Y) - y_;
                                                    _target.Invalidate(renderer, x_, y_, width_, height_, LayerType.LayerType_ALL);
                                                } );

            if (image == null)
            {
                // image is not ready yet...
                if (_fillColor.A > 0)
                {
                    _session.FillRectangle(x, y, width, height, _fillColor);
                }
            }
            else
            {
                // adjust rectangle so that the image gets fit into original rectangle
                var size = image.SizeInPixels;
                float fx = width / (float)size.Width;
                float fy = height / (float)size.Height;

                if (fx > fy)
                {
                    float w = (float)(size.Width) * fy;
                    x += (width - w) / 2;
                    width = w;
                }
                else
                {
                    float h = (float)(size.Height) * fx;
                    y += (height - h) / 2;
                    height = h;
                }

                // draw the image
                var rect = new Rect(x, y, width, height);
                _session.DrawImage(image, rect, new Rect(0, 0, size.Width, size.Height));
            }
        }

        /// <summary>Draw Text to canvas according coordinates and label</summary>
        public void DrawText(string label, float x, float y, float xmin, float ymin, float xmax, float ymax)
        {
            if (_fillColor.A > 0)
            {
                _session.DrawText(label, x, y - _baseline, _fillColor, _fontProperties);
            }
        }
    };
}