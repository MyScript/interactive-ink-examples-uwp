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
        public delegate void NotificationDelegate(string url, CanvasBitmap image);

        private Editor _editor;
        private string _cacheDirectory;
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
            _cacheDirectory = System.IO.Path.Combine(cacheDirectory, "tmp/render-cache");
            _cache = new ConcurrentDictionary<string, CanvasBitmap>();
        }

        public CanvasBitmap getImage(string url, string mimeType, NotificationDelegate onLoaded)
        {
            if (_cache.ContainsKey(url))
                return _cache[url];

            var dispatcher = Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher;
            var task = dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                            async () =>
                                            {
                                                CanvasBitmap image_ = await loadImage(url, mimeType);
                                                if (image_ != null)
                                                {
                                                    _cache[url] = image_;
                                                    onLoaded?.Invoke(url, image_);
                                                }
                                            });

            return null;
        }

        private async Task<CanvasBitmap> loadImage(string url, string mimeType)
        {
            var dpi = Math.Max(_editor.Renderer.DpiX, _editor.Renderer.DpiY);

            if (mimeType.StartsWith("image/"))
            {
                try
                {
                    var path = getFilePath(url);
                    CanvasBitmap image_ = await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), path, dpi);

                    // Delete temporary file... disabled because it fails (CanvasBitmap has locked it ?)
                    //System.IO.File.Delete(path);

                    return image_;
                }
                catch
                {
                    // Error: use fallback bitmap
                }
            }

            // Fallback 1x1 bitmap
            var image = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), 1, 1, dpi);

            Color[] color = new Color[] { Color.FromArgb(255, 255, 255, 255) };
            image.SetPixelColors(color,  0, 0, 1, 1);

            return image;
        }

        private string getFilePath(string url)
        {
            var filePath = System.IO.Path.Combine(_cacheDirectory, url);
            var fullFilePath = System.IO.Path.GetFullPath(filePath);
            var folderPath = System.IO.Path.GetDirectoryName(fullFilePath);

            System.IO.Directory.CreateDirectory(folderPath);
            _editor.Part.Package.ExtractObject(url, fullFilePath);

            return fullFilePath;
        }
    }
}
