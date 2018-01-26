// Copyright MyScript. All right reserved.

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using MyScript.IInk.Graphics;
using System;

namespace MyScript.IInk.UIReferenceImplementation
{
    public class ImageDrawer : IImageDrawer
    {
        private CanvasRenderTarget _image;
        private float _dpiX;
        private float _dpiY;

        public ImageLoader ImageLoader { get; set; }

        public ImageDrawer(float dpiX, float dpiY)
        {
            _dpiX = dpiX;
            _dpiY = dpiY;
        }

        public void PrepareImage(int width, int height)
        {
            if (_image != null)
                _image = null;

            _image = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), width, height, Math.Max(_dpiX, _dpiY));
        }

        public void SaveImage(string path)
        {
            if ((_image != null) && !string.IsNullOrWhiteSpace(path))
            {
                var task = System.Threading.Tasks.Task.Run(async() => { await _image.SaveAsync(path); });
                if (task != null)
                    System.Threading.Tasks.Task.WaitAll(task);
            }

            _image = null;
        }

        public void Invalidate(Renderer renderer, LayerType layers)
        {
            if (_image != null && renderer != null)
            {
                var size = _image.SizeInPixels;
                Invalidate(renderer, 0, 0, (int)size.Width, (int)size.Height, layers);
            }
        }

        public void Invalidate(Renderer renderer, int x, int y, int width, int height, LayerType layers)
        {
            if (_image != null && renderer != null)
            {
                var region = new Windows.Foundation.Rect(x, y, width, height);

                using (var session = _image.CreateDrawingSession())
                {
                    var canvas = new Canvas(session, this, ImageLoader);

                    if (layers.HasFlag(LayerType.BACKGROUND))
                    {
                        var white = new Color(0xffffffff);
                        canvas.Begin();
                        canvas.Clear(white);
                        renderer.DrawBackground(x, y, width, height, canvas);
                        canvas.End();
                    }

                    if (layers.HasFlag(LayerType.MODEL))
                    {
                        canvas.Begin();
                        renderer.DrawModel(x, y, width, height, canvas);
                        canvas.End();
                    }

                    if (layers.HasFlag(LayerType.TEMPORARY))
                    {
                        canvas.Begin();
                        renderer.DrawTemporaryItems(x, y, width, height, canvas);
                        canvas.End();
                    }

                    if (layers.HasFlag(LayerType.CAPTURE))
                    {
                        canvas.Begin();
                        renderer.DrawCaptureStrokes(x, y, width, height, canvas);
                        canvas.End();
                    }
                }
            }
        }
    }
}
