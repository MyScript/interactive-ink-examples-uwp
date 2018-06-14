// Copyright MyScript. All right reserved.

using Microsoft.Graphics.Canvas;
using MyScript.IInk.Graphics;
using System;

namespace MyScript.IInk.UIReferenceImplementation
{
    public class ImageDrawer : IImageDrawer
    {
        private static Color _defaultBackgroundColor = new Color(0xffffffff);

        private CanvasRenderTarget _image;

        public ImageLoader ImageLoader { get; set; }
        public Graphics.Color BackgroundColor { get; set; }

        public ImageDrawer()
        {
            BackgroundColor = _defaultBackgroundColor;
        }

        public void PrepareImage(int width, int height)
        {
            if (_image != null)
                _image = null;

            // Use 96 dpi to match the default DIP unit used by UWP
            _image = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), width, height, 96);
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
                using (var session = _image.CreateDrawingSession())
                {
                    var canvas = new Canvas(session, this, ImageLoader);
                    var color = (BackgroundColor != null) ? BackgroundColor : _defaultBackgroundColor;

                    canvas.Begin();
                    canvas.Clear(color);

                    if (layers.HasFlag(LayerType.BACKGROUND))
                        renderer.DrawBackground(x, y, width, height, canvas);

                    if (layers.HasFlag(LayerType.MODEL))
                        renderer.DrawModel(x, y, width, height, canvas);

                    if (layers.HasFlag(LayerType.TEMPORARY))
                        renderer.DrawTemporaryItems(x, y, width, height, canvas);

                    if (layers.HasFlag(LayerType.CAPTURE))
                        renderer.DrawCaptureStrokes(x, y, width, height, canvas);

                    canvas.End();
                }
            }
        }
    }
}
