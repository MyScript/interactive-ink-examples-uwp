// Copyright @ MyScript. All rights reserved.

using Microsoft.Graphics.Canvas;
using MyScript.IInk.Graphics;
using System;

namespace MyScript.IInk.UIReferenceImplementation
{
    public class ImagePainter : IImagePainter
    {
        private static Color _defaultBackgroundColor = new Color(0xffffffff);

        private CanvasRenderTarget _image;
        private CanvasDrawingSession _session;
        private Canvas _canvas;

        public ImageLoader ImageLoader { get; set; }
        public Graphics.Color BackgroundColor { get; set; }

        public ImagePainter()
        {
            BackgroundColor = _defaultBackgroundColor;
        }

        public void PrepareImage(int width, int height, float dpi)
        {
            if (_image != null)
                _image = null;

            // Use 96 dpi to match the default DIP unit used by UWP
            _image = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), width, height, 96);
            _session = _image.CreateDrawingSession();

            var color = (BackgroundColor != null) ? BackgroundColor : _defaultBackgroundColor;
            _canvas = new Canvas(_session, null, ImageLoader);
            _canvas.Begin();
            _canvas.Clear(color);
        }

        public void SaveImage(string path)
        {
            if ((_image != null) && !string.IsNullOrWhiteSpace(path))
            {
                _canvas.End();
                _session.Dispose();
                _session = null;
                var task = System.Threading.Tasks.Task.Run(async() => { await _image.SaveAsync(path); });
                if (task != null)
                    System.Threading.Tasks.Task.WaitAll(task);
            }
            _image = null;
        }

        public ICanvas CreateCanvas()
        {
            return _canvas;
        }
    }
}
