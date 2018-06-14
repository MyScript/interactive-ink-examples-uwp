// Copyright MyScript. All right reserved.

using MyScript.IInk.Graphics;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.ApplicationModel.Core;

namespace MyScript.IInk.UIReferenceImplementation
{
    public class Layer
    {
        public LayerType Type { get; private set; }
        public ImageLoader ImageLoader { get; set; }

        private CanvasVirtualControl _control;
        private IRenderTarget _target;
        private Renderer _renderer ;

        public Layer(CanvasVirtualControl control, IRenderTarget target, LayerType type, Renderer renderer)
        {
            Type = type;
            _control = control;
            _target = target;
            _renderer = renderer;
        }

        public void Update()
        {
            // It must be done on UI thread
            var task = _control.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    _control.Invalidate();
                });
        }

        private void Update_(int x, int y, int width, int height)
        {
            // Clamp region's coordinates into control's rect
            // (control.Invalidate may raise an exception)
            var region = ClampRect(x, y, width, height);

            if (region.Width > 0 && region.Height > 0)
            {
                _control.Invalidate(region);
            }
        }

        public void Update(int x, int y, int width, int height)
        {
            // It must be done on UI thread
            var task = _control.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () =>
                {
                    Update_(x, y, width, height);
                });
        }

        /// <summary>Retranscribe draw with renderer</summary>
        public void OnPaint(int x, int y, int width, int height)
        {
            // Clamp region's coordinates into control's rect
            // (control.CreateDrawingSession may raise an exception)
            var region = ClampRect(x, y, width, height);
            if (region.Width <= 0 || region.Height <= 0)
                return;

            using (var session = _control.CreateDrawingSession(region))
            {
                var canvas = new Canvas(session, _target, ImageLoader);

                switch (Type)
                {
                    case LayerType.BACKGROUND:
                        canvas.Begin();
                        _renderer.DrawBackground(x, y, width, height, canvas);
                        canvas.End();
                        break;

                    case LayerType.MODEL:
                        canvas.Begin();
                        _renderer.DrawModel(x, y, width, height, canvas);
                        canvas.End();
                        break;

                    case LayerType.TEMPORARY:
                        canvas.Begin();
                        _renderer.DrawTemporaryItems(x, y, width, height, canvas);
                        canvas.End();
                        break;

                    case LayerType.CAPTURE:
                        canvas.Begin();
                        _renderer.DrawCaptureStrokes(x, y, width, height, canvas);
                        canvas.End();
                        break;

                    default:
                        break;
                }
            }
        }

        /// <summary>Clamp rect's coordinates into control's rect</summary>
        private Rect ClampRect(int x, int y, int width, int height)
        {
            if (x < 0)
            {
                width += x;
                x = 0;
            }
            if (y < 0)
            {
                height += y;
                y = 0;
            }
            if ((x + width) > _control.ActualWidth)
            {
                width = (int)(_control.ActualWidth - x);
            }
            if ((y + height) > _control.ActualHeight)
            {
                height = (int)(_control.ActualHeight - y);
            }

            return new Rect(x, y, width > 0 ? width : 0, height > 0 ? height : 0);
        }
    };
}