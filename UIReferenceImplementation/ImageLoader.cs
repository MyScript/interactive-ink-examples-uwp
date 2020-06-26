// Copyright @ MyScript. All rights reserved.

using Microsoft.Graphics.Canvas;
using System;
using Windows.UI;
using System.Collections.Generic;

namespace MyScript.IInk.UIReferenceImplementation
{
    // LruImgCache
    public class LruImgCache
    {
        private LinkedList<string> _lru = new LinkedList<string>();
        private Dictionary<string, ImageNode> _cache = new Dictionary<string, ImageNode>();
        private int _maxBytes;
        private int _curBytes;

        // ImageNode
        public struct ImageNode
        {
            public ImageNode(CanvasBitmap image, int cost)
            {
                Image = image;
                Cost = cost;
            }

            public CanvasBitmap Image { get; }
            public int Cost { get; }
        }

        public LruImgCache(int maxBytes)
        {
            _maxBytes = maxBytes;
            _curBytes = 0;
        }

        public bool containsBitmap(string url)
        {
            return _cache.ContainsKey(url);
        }

        public CanvasBitmap getBitmap(string url)
        {
            // Update LRU
            _lru.Remove(url);
            _lru.AddFirst(url);

            return _cache[url].Image;
        }

        public void putBitmap(string url, Editor editor)
        {
            CanvasBitmap image = loadBitmap(url, editor);
            var imageBytes = image.GetPixelBytes().Length;

            // Too big for cache
            if (imageBytes > _maxBytes)
            {
                // Use fallback (cache it to avoid reloading it each time for size check)
                image = createFallbackBitmap();
                imageBytes = 4;
            }

            // Remove LRUs if max size reached
            while (_curBytes + imageBytes > _maxBytes)
            {
                string lruKey = _lru.Last.Value;
                ImageNode lruNode = _cache[lruKey];
                _curBytes -= lruNode.Cost;
                _cache.Remove(lruKey);
                _lru.RemoveLast();
            }

            // Add to cache
            _cache.Add(url, new ImageNode(image, imageBytes));
            _curBytes += imageBytes;
            _lru.AddFirst(url);
        }

        private CanvasBitmap loadBitmap(string url, Editor editor)
        {
            var dpi = Math.Max(editor.Renderer.DpiX, editor.Renderer.DpiY);
            var path = System.IO.Path.GetFullPath(url);

            try
            {
                CanvasBitmap image = null;
                var task = System.Threading.Tasks.Task.Run(async ()
                    => { image = await CanvasBitmap.LoadAsync(CanvasDevice.GetSharedDevice(), path, dpi); });
                if (task != null)
                    System.Threading.Tasks.Task.WaitAll(task);

                if (image != null)
                    return image;
            }
            catch
            {
                // Error: use fallback bitmap
            }

            // Fallback
            return createFallbackBitmap();
        }

        public static CanvasBitmap createFallbackBitmap()
        {
            // Fallback 1x1 bitmap
            var dpi = 96;
            var image = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), 1, 1, dpi);
            Color[] color = new Color[] { Color.FromArgb(255, 255, 255, 255) };
            image.SetPixelColors(color, 0, 0, 1, 1);

            return image;
        }
    }

    // ImageLoader
    public class ImageLoader
    {
        private Editor _editor;
        private LruImgCache _cache;
        private const int CACHE_MAX_BYTES = 200 * 1000000;  // 200M (in Bytes)

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
            _cache = new LruImgCache(CACHE_MAX_BYTES);
        }

        public CanvasBitmap getImage(string url, string mimeType)
        {
            CanvasBitmap image = null;

            lock (_cache)
            {
                if (!_cache.containsBitmap(url))
                    _cache.putBitmap(url, _editor);
                image = _cache.getBitmap(url);
            }

            if (image == null)
                image = LruImgCache.createFallbackBitmap();

            return image;
        }
    }
}
