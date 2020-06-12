// Copyright MyScript. All right reserved.

using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Concurrent;
using Windows.UI.Core;
using Windows.UI;
using System.Threading.Tasks;

namespace MyScript.IInk.UIReferenceImplementation
{
    public class ImageLoader
    {
        private Editor _editor;
        private ConcurrentDictionary<string, CanvasBitmap> _cache;

        public Editor Editor
        {
            get
            {
                return _editor;
            }
        }

        public ImageLoader(Editor editor, string cacheDirectory)
        {
            _editor = editor;
            _cache = new ConcurrentDictionary<string, CanvasBitmap>();
        }

        public CanvasBitmap getImage(string url, string mimeType)
        {
            if (!_cache.ContainsKey(url))
            {
                var dpi = Math.Max(_editor.Renderer.DpiX, _editor.Renderer.DpiY);
                var path = System.IO.Path.GetFullPath(url);
                try
                {
                    var task = System.Threading.Tasks.Task.Run(async () => { _cache[url] = await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), path, dpi); });
                    if (task != null)
                        System.Threading.Tasks.Task.WaitAll(task);
                }
                catch
                {
                    // Error: use fallback bitmap
                    var image = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), 1, 1, dpi);
                    Color[] color = new Color[] { Color.FromArgb(255, 255, 255, 255) };
                    image.SetPixelColors(color, 0, 0, 1, 1);
                    return image;
                }
            }


            return _cache[url];
        }
    }
}
