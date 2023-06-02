// Copyright @ MyScript. All rights reserved.

using System.Collections.Generic;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.System;
using Windows.Graphics.Display;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;

using MyScript.IInk.Graphics;
using MyScript.IInk.UIReferenceImplementation.Extensions;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236
namespace MyScript.IInk.UIReferenceImplementation.UserControls
{
    public sealed partial class EditorUserControl
    {
        public static readonly DependencyProperty EditorProperty =
            DependencyProperty.Register("Editor", typeof(Editor), typeof(EditorUserControl),
                new PropertyMetadata(default(Editor)));

        public Editor Editor
        {
            get => GetValue(EditorProperty) as Editor;
            set
            {
                SetValue(EditorProperty, value);
                if (!(value is Editor editor))return;
                Initialize(Editor);
            }
        }
    }

    public sealed partial class EditorUserControl : IRenderTarget
    {
        private ImageLoader _loader;
        private bool _smartGuideEnabled = true;

        public ImageLoader ImageLoader => _loader;
        public SmartGuideUserControl SmartGuide => smartGuide;

        private Layer _modelLayer;
        private Layer _captureLayer;

        private uint _nextOffscreenRenderId = 0;
        private IDictionary<uint, CanvasRenderTarget> _bitmaps = new Dictionary<uint, CanvasRenderTarget>();

        public bool SmartGuideEnabled
        {
            get
            {
                return _smartGuideEnabled;
            }

            set
            {
                EnableSmartGuide(value);
            }
        }

        private int _pointerId = -1;
        private bool _onScroll = false;
        private Graphics.Point _lastPointerPosition;
        private System.Int64 _eventTimeOffset = 0;

        public EditorUserControl()
        {
            InitializeComponent();

            var msFromEpoch = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var msFromBoot = System.Environment.TickCount;
            _eventTimeOffset = msFromEpoch - msFromBoot;
        }

        private void Initialize(Editor editor)
        {
            var engine = editor.Engine;

            var dpiX = Editor?.Renderer?.DpiX ?? 96;
            var dpiY = Editor?.Renderer?.DpiY ?? 96;

            var render = editor.Renderer;
            _modelLayer = new Layer(modelCanvas, this, LayerType.MODEL, render);
            _captureLayer = new Layer(captureCanvas, this, LayerType.CAPTURE, render);

            var tempFolder = engine.Configuration.GetString("content-package.temp-folder");
            _loader = new ImageLoader(Editor, tempFolder);

            _modelLayer.ImageLoader = _loader;
            _captureLayer.ImageLoader = _loader;

            float verticalMarginPX = 60;
            float horizontalMarginPX = 40;
            var verticalMarginMM = 25.4f * verticalMarginPX / dpiY;
            var horizontalMarginMM = 25.4f * horizontalMarginPX / dpiX;
            engine.Configuration.SetNumber("text.margin.top", verticalMarginMM);
            engine.Configuration.SetNumber("text.margin.left", horizontalMarginMM);
            engine.Configuration.SetNumber("text.margin.right", horizontalMarginMM);
            engine.Configuration.SetNumber("math.margin.top", verticalMarginMM);
            engine.Configuration.SetNumber("math.margin.bottom", verticalMarginMM);
            engine.Configuration.SetNumber("math.margin.left", horizontalMarginMM);
            engine.Configuration.SetNumber("math.margin.right", horizontalMarginMM);
        }

        /// <summary>Force inks layer to be redrawn</summary>
        public void Invalidate(LayerType layers)
        {
            if (!(Editor?.Renderer is Renderer renderer)) return;
            Invalidate(renderer, layers);
        }

        /// <summary>Force inks layer to be redrawn</summary>
        public void Invalidate(Renderer renderer, LayerType layers)
        {
            if ((layers & LayerType.MODEL) != 0)
                _modelLayer.Update();

            if ((layers & LayerType.CAPTURE) != 0)
                _captureLayer.Update();
        }

        /// <summary>Force ink layers to be redrawn according region</summary>
        public void Invalidate(Renderer renderer, int x, int y, int width, int height, LayerType layers)
        {
            if (height < 0)
                return;

            if ((layers & LayerType.MODEL) != 0)
                _modelLayer?.Update(x, y, width, height);

            if ((layers & LayerType.CAPTURE) != 0)
                _captureLayer?.Update(x, y, width, height);
        }

        public bool SupportsOffscreenRendering()
        {
            return true;
        }

        public float GetPixelDensity()
        {
            var info = DisplayInformation.GetForCurrentView();
            return info.RawPixelsPerViewPixel > 0 ? (float)info.RawPixelsPerViewPixel :
                info.ResolutionScale != ResolutionScale.Invalid ? (float)info.ResolutionScale / 100.0f : 1.0f;
        }

        public uint CreateOffscreenRenderSurface(int width, int height, bool alphaMask)
        {
            // Use DPI 96 to specify 1:1 dip <-> pixel mapping
            CanvasDevice device = CanvasDevice.GetSharedDevice();
            CanvasRenderTarget offscreen = new CanvasRenderTarget(device, width, height, 96);

            uint offscreenRenderId = _nextOffscreenRenderId++;
            _bitmaps.Add(offscreenRenderId, offscreen);
            return offscreenRenderId;
        }

        public void ReleaseOffscreenRenderSurface(uint surfaceId)
        {
            CanvasRenderTarget offscreen;
            if (!_bitmaps.TryGetValue(surfaceId, out offscreen))
                throw new System.NullReferenceException();

            _bitmaps.Remove(surfaceId);
            offscreen.Dispose();
        }

        public ICanvas CreateOffscreenRenderCanvas(uint offscreenID)
        {
            CanvasRenderTarget offscreen;
            if ( !_bitmaps.TryGetValue(offscreenID, out offscreen) )
                throw new System.NullReferenceException();

            return new Canvas(offscreen.CreateDrawingSession(), this, _loader);
        }

        public void ReleaseOffscreenRenderCanvas(ICanvas canvas)
        {
            // The previously created DrawingSession (in CreateOffscreenRenderCanvas) must be disposed
            // before we can ask the offscreen surface (CanvasRenderTarget) to recreate a new one.
            // So, we ask the canvas to dispose and set to null its DrawingSession; the canvas should be destroyed soon after.
            Canvas canvas_ = (Canvas)canvas;
            canvas_.DisposeSession();
        }

        public CanvasRenderTarget GetImage(uint offscreenID)
        {
            CanvasRenderTarget offscreen;
            if ( !_bitmaps.TryGetValue(offscreenID, out offscreen) )
                throw new System.NullReferenceException();

            return offscreen;
        }


        private void EnableSmartGuide(bool enable)
        {
            if (_smartGuideEnabled == enable)
                return;

            _smartGuideEnabled = enable;

            if (!_smartGuideEnabled && smartGuide != null)
                smartGuide.Visibility = Visibility.Collapsed;
        }

        public void OnResize(int width, int height)
        {
            Editor?.SetViewSize(width, height);
        }

        /// <summary>Resize editor when one canvas size has been changed </summary>
        private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender == captureCanvas)
            {
                OnResize((int)captureCanvas.ActualWidth, (int)captureCanvas.ActualHeight);
            }

            ((CanvasVirtualControl)(sender)).Invalidate();
        }

        /// <summary>Redrawing Canvas</summary>
        private void Canvas_OnRegionsInvalidated(CanvasVirtualControl sender, CanvasRegionsInvalidatedEventArgs args)
        {
            foreach (var region in args.InvalidatedRegions)
            {
                if (region.Width > 0 && region.Height > 0)
                {
                    var x = (int)System.Math.Floor(region.X);
                    var y = (int)System.Math.Floor(region.Y);
                    var width = (int)System.Math.Ceiling(region.X + region.Width) - x;
                    var height = (int)System.Math.Ceiling(region.Y + region.Height) - y;

                    if (sender == captureCanvas)
                        _captureLayer.OnPaint(x, y, width, height);
                    else if (sender == modelCanvas)
                        _modelLayer.OnPaint(x, y, width, height);
                }
            }
        }

        private System.Int64 GetTimestamp(Windows.UI.Input.PointerPoint point)
        {
            // Convert the timestamp (from boot time) to milliseconds
            // and add offset to get the time from EPOCH
            return _eventTimeOffset + (System.Int64)(point.Timestamp / 1000);
        }

        public int GetPointerId(RoutedEventArgs e)
        {
            if (e is PointerRoutedEventArgs)
                return (int)((PointerRoutedEventArgs)e).Pointer.PointerDeviceType;
            else if (e is HoldingRoutedEventArgs)
                return (int)((HoldingRoutedEventArgs)e).PointerDeviceType;

            return -1;
        }

        [System.Flags]
        public enum ContextualActions
        {
            NONE              = 0,
            ADD_BLOCK         = 1 << 0,     /// Add block. See <c>Editor.GetSupportedAddBlockTypes</c>.
            REMOVE            = 1 << 1,     /// Remove selection.
            CONVERT           = 1 << 2,     /// Convert. See <c>Editor.GetSupportedTargetConversionStates</c>.
            COPY              = 1 << 3,     /// Copy selection.
            OFFICE_CLIPBOARD  = 1 << 4,     /// Copy selection to Microsoft Office clipboard.
            PASTE             = 1 << 5,     /// Paste.
            IMPORT            = 1 << 6,     /// Import. See <c>Editor.GetSupportedImportMimeTypes</c>.
            EXPORT            = 1 << 7,     /// Export. See <c>Editor.GetSupportedExportMimeTypes</c>.
            FORMAT_TEXT       = 1 << 8      /// Change Text blocks format.
        }

        public ContextualActions GetAvailableActions(ContentBlock contentBlock)
        {
            if (contentBlock == null || Editor == null)
                return ContextualActions.NONE;

            var part = Editor.Part;
            if (part == null)
                return ContextualActions.NONE;

            var actions = ContextualActions.NONE;

            using (var rootBlock = Editor.GetRootBlock())
            {
                var isRoot = contentBlock.Id == rootBlock.Id;
                if (!isRoot && (contentBlock.Type == "Container"))
                    return ContextualActions.NONE;

                var onRawContent   = part.Type == "Raw Content";
                var onTextDocument = part.Type == "Text Document";

                var isEmpty = Editor.IsEmpty(contentBlock);

                var supportedTypes   = Editor.SupportedAddBlockTypes;
                var supportedExports = Editor.GetSupportedExportMimeTypes(onRawContent ? rootBlock : contentBlock);
                var supportedImports = Editor.GetSupportedImportMimeTypes(contentBlock);
                var supportedStates  = Editor.GetSupportedTargetConversionStates(contentBlock);
                var supportedFormats = Editor.GetSupportedTextFormats(contentBlock);

                var hasTypes   = (supportedTypes   != null) && supportedTypes.Any();
                var hasExports = (supportedExports != null) && supportedExports.Any();
                var hasImports = (supportedImports != null) && supportedImports.Any();
                var hasStates  = (supportedStates  != null) && supportedStates.Any();
                var hasFormats = (supportedFormats != null) && supportedFormats.Any();

                if (hasTypes && (!onTextDocument || isRoot))
                    actions |= ContextualActions.ADD_BLOCK;
                if (!isRoot)
                    actions |= ContextualActions.REMOVE;
                if (hasStates && !isEmpty)
                    actions |= ContextualActions.CONVERT;
                if (!onTextDocument || !isRoot)
                    actions |= ContextualActions.COPY;
                if (hasExports && supportedExports.Contains(MimeType.OFFICE_CLIPBOARD))
                    actions |= ContextualActions.OFFICE_CLIPBOARD;
                if (isRoot)
                    actions |= ContextualActions.PASTE;
                if (hasImports)
                    actions |= ContextualActions.IMPORT;
                if (hasExports)
                    actions |= ContextualActions.EXPORT;
                if (hasFormats)
                    actions |= ContextualActions.FORMAT_TEXT;
            }

            return actions;
        }

        public ContextualActions GetAvailableActions(ContentSelection contentSelection)
        {
            if (contentSelection == null || Editor == null || Editor.IsEmpty(contentSelection))
                return ContextualActions.NONE;

            var part = Editor.Part;
            if (part == null)
                return ContextualActions.NONE;

            var actions = ContextualActions.NONE;

            var supportedExports = Editor.GetSupportedExportMimeTypes(contentSelection);
            var supportedStates  = Editor.GetSupportedTargetConversionStates(contentSelection);
            var supportedFormats = Editor.GetSupportedTextFormats(contentSelection);

            var hasExports = (supportedExports != null) && supportedExports.Any();
            var hasStates  = (supportedStates  != null) && supportedStates.Any();
            var hasFormats = (supportedFormats != null) && supportedFormats.Any();

            // Erase
            actions |= ContextualActions.REMOVE;
            if (hasStates)
                actions |= ContextualActions.CONVERT;
            // Copy
            actions |= ContextualActions.COPY;
            if (hasExports && supportedExports.Contains(MimeType.OFFICE_CLIPBOARD))
                actions |= ContextualActions.OFFICE_CLIPBOARD;
            if (hasExports)
                actions |= ContextualActions.EXPORT;
            if (hasFormats)
                actions |= ContextualActions.FORMAT_TEXT;

            return actions;
        }

        public void CancelSampling(int pointerId)
        {
            Editor?.PointerCancel(pointerId);
            _pointerId = -1;
        }

        private bool HasPart()
        {
            return Editor?.Part != null;
        }

        private void Capture_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var uiElement = sender as UIElement;
            var p = e.GetCurrentPoint(uiElement);

            if (!HasPart())
                return;

            if (_pointerId != -1)
                return;

            // Consider left button only
            if ( (!p.Properties.IsLeftButtonPressed) || (p.Properties.PointerUpdateKind != Windows.UI.Input.PointerUpdateKind.LeftButtonPressed) )
                return;

            // Capture the pointer to the target.
            uiElement?.CapturePointer(e.Pointer);

            try
            {
                _pointerId = (int)e.Pointer.PointerId;
                _lastPointerPosition = new Graphics.Point((float)p.Position.X, (float)p.Position.Y);
                _onScroll = false;

                // Send pointer down event to the editor
                Editor?.PointerDown((float)p.Position.X, (float)p.Position.Y, GetTimestamp(p), p.Properties.Pressure, e.Pointer.PointerDeviceType.ToNative(), GetPointerId(e));
            }
            catch (System.Exception ex)
            {
                if (ex.HResult == (int)MyScript.IInk.ExceptionHResult.POINTER_SEQUENCE_ERROR)
                {
                    // Special case: pointerDown already called, discard previous and retry
                    Editor?.PointerCancel(GetPointerId(e));
                    Editor?.PointerDown((float)p.Position.X, (float)p.Position.Y, GetTimestamp(p), p.Properties.Pressure, e.Pointer.PointerDeviceType.ToNative(), GetPointerId(e));
                }
                else
                {
                    var dlg = new MessageDialog(ex.Message);
                    var dlgTask = dlg.ShowAsync();
                }
            }
            // Prevent most handlers along the event route from handling the same event again.
            e.Handled = true;
        }

        private void Capture_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var uiElement = sender as UIElement;
            var p = e.GetCurrentPoint(uiElement);

            if (!HasPart())
                return;

            if (_pointerId != (int)e.Pointer.PointerId)
                return;

            // Ignore pointer move when the pointing device is up
            if (!p.IsInContact)
                return;

            // Consider left button only
            if (!p.Properties.IsLeftButtonPressed)
                return;

            var pointerType = e.Pointer.PointerDeviceType.ToNative();
            var previousPosition = _lastPointerPosition;
            _lastPointerPosition = new Graphics.Point((float)p.Position.X, (float)p.Position.Y);

            var pointerTool = Editor.ToolController.GetToolForType(pointerType);
            if (!_onScroll && pointerTool == PointerTool.HAND)
            {
                var deltaMin = 3.0f;
                var deltaX = _lastPointerPosition.X - previousPosition.X;
                var deltaY = _lastPointerPosition.Y - previousPosition.Y;

                _onScroll = Editor.IsScrollAllowed() && ((System.Math.Abs(deltaX) > deltaMin) || (System.Math.Abs(deltaY) > deltaMin));

                if (_onScroll)
                {
                    // Entering scrolling mode, cancel previous pointerDown event
                    Editor?.PointerCancel(GetPointerId(e));
                }
            }

            if (_onScroll)
            {
                // Scroll the view
                var deltaX = _lastPointerPosition.X - previousPosition.X;
                var deltaY = _lastPointerPosition.Y - previousPosition.Y;
                Scroll(-deltaX, -deltaY);
            }
            else
            {
                var pointList = e.GetIntermediatePoints(uiElement);
                if (pointList.Count > 0)
                {
                    var events = new PointerEvent[pointList.Count];

                    // Intermediate points are stored in reverse order:
                    // Revert the list and send the pointer events all at once
                    int j = 0;
                    for (int i = pointList.Count - 1; i >= 0; i--)
                    {
                        var p_ = pointList[i];
                        events[j++] = new PointerEvent(PointerEventType.MOVE, (float)p_.Position.X, (float)p_.Position.Y, GetTimestamp(p_), p_.Properties.Pressure, pointerType, GetPointerId(e));
                    }

                    // Send pointer move events to the editor
                    try
                    {
                        Editor?.PointerEvents(events);
                    }
                    catch
                    {
                        // Don't show error for every move event
                    }
                }
            }

            // Prevent most handlers along the event route from handling the same event again.
            e.Handled = true;
        }

        private void Capture_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var uiElement = sender as UIElement;
            var p = e.GetCurrentPoint(uiElement);

            if (!HasPart())
                return;

            if (_pointerId != (int)e.Pointer.PointerId)
                return;

            // Consider left button only
            if ( (p.Properties.IsLeftButtonPressed) || (p.Properties.PointerUpdateKind != Windows.UI.Input.PointerUpdateKind.LeftButtonReleased) )
                return;

            var previousPosition = _lastPointerPosition;
            _lastPointerPosition = new Graphics.Point((float)p.Position.X, (float)p.Position.Y);

            if (_onScroll)
            {
                // Scroll the view
                var deltaX = _lastPointerPosition.X - previousPosition.X;
                var deltaY = _lastPointerPosition.Y - previousPosition.Y;
                Scroll(-deltaX, -deltaY);

                // Exiting scrolling mode
                _onScroll = false;
            }
            else
            {
                // Send pointer up event to the editor
                try
                {
                    Editor?.PointerUp((float)p.Position.X, (float)p.Position.Y, GetTimestamp(p), p.Properties.Pressure, e.Pointer.PointerDeviceType.ToNative(), GetPointerId(e));
                }
                catch
                {
                    // Don't show error for up event
                }
            }

            _pointerId = -1;

            // Release the pointer captured from the target
            uiElement?.ReleasePointerCapture(e.Pointer);

            // Prevent most handlers along the event route from handling the same event again.
            e.Handled = true;
        }

        private void Capture_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            var uiElement = sender as UIElement;
            var p = e.GetCurrentPoint(uiElement);

            if (!HasPart())
                return;

            if (_pointerId != (int)e.Pointer.PointerId)
                return;

            // When using mouse consider left button only
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if (!p.Properties.IsLeftButtonPressed)
                    return;
            }

            if (_onScroll)
            {
                // Exiting scrolling mode
                _onScroll = false;
            }
            else
            {
                // Send pointer cancel event to the editor
                Editor.PointerCancel(GetPointerId(e));
            }

            _pointerId = -1;

            // Prevent most handlers along the event route from handling the same event again.
            e.Handled = true;
        }

        private void Capture_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var uiElement = captureCanvas; //sender as UIElement;
            var properties = e.GetCurrentPoint(uiElement).Properties;

            if (!HasPart())
                return;

            if (properties.IsHorizontalMouseWheel == false)
            {
                var WHEEL_DELTA = 120;

                var controlDown = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
                var shiftDown = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);
                var wheelDelta = properties.MouseWheelDelta / WHEEL_DELTA;

                if (controlDown)
                {
                    if (wheelDelta > 0)
                        ZoomIn((uint)wheelDelta);
                    else if (wheelDelta < 0)
                        ZoomOut((uint)(-wheelDelta));
                }
                else
                {
                    var SCROLL_SPEED = 100;
                    var delta = (float)(-SCROLL_SPEED * wheelDelta);
                    var deltaX = shiftDown ? delta : 0.0f;
                    var deltaY = shiftDown ? 0.0f : delta;

                    Scroll(deltaX, deltaY);
                }
            }

            // Prevent most handlers along the event route from handling the same event again.
            e.Handled = true;
        }

        public void ResetView(bool forceInvalidate)
        {
            if (!(Editor?.Renderer is Renderer renderer) || !HasPart())
                return;

            // Reset view offset and scale
            renderer.ViewScale = 1;
            renderer.ViewOffset = new Graphics.Point(0, 0);

            // Get new view transform (keep only scale and offset)
            var tr = renderer.GetViewTransform();
            tr = new Graphics.Transform(tr.XX, tr.YX, 0, tr.XY, tr.YY, 0);

            // Compute new view offset
            var offset = new Graphics.Point(0, 0);

            if (Editor.Part.Type == "Raw Content")
            {
                // Center view on the center of content for "Raw Content" parts
                var contentBox = Editor.GetRootBlock().Box;
                var contentCenter = new Graphics.Point(contentBox.X + (contentBox.Width * 0.5f), contentBox.Y + (contentBox.Height * 0.5f));

                // From model coordinates to view coordinates
                contentCenter = tr.Apply(contentCenter.X, contentCenter.Y);

                var viewCenter = new Graphics.Point(Editor.ViewWidth * 0.5f, Editor.ViewHeight * 0.5f);
                offset.X = contentCenter.X - viewCenter.X;
                offset.Y = contentCenter.Y - viewCenter.Y;
            }
            else
            {
                // Move the origin to the top-left corner of the page for other types of parts
                var boxV = Editor.Part.ViewBox;

                offset.X = boxV.X;
                offset.Y = boxV.Y;

                // From model coordinates to view coordinates
                offset = tr.Apply(offset.X, offset.Y);
            }

            // Set new view offset
            Editor.ClampViewOffset(offset);
            renderer.ViewOffset = offset;

            if (forceInvalidate)
                Invalidate(renderer, LayerType.LayerType_ALL);
        }

        public void ZoomIn(uint delta)
        {
            if (!(Editor?.Renderer is Renderer renderer)) return;
            renderer.Zoom((float)delta * (110.0f / 100.0f));
            Invalidate(renderer, LayerType.LayerType_ALL);
        }

        public void ZoomOut(uint delta)
        {
            if (!(Editor?.Renderer is Renderer renderer)) return;
            renderer.Zoom((float)delta * (100.0f / 110.0f));
            Invalidate(renderer, LayerType.LayerType_ALL);
        }

        private void Scroll(float deltaX, float deltaY)
        {
            if (!(Editor?.Renderer is Renderer renderer)) return;
            var oldOffset = renderer.ViewOffset;
            var newOffset = new Graphics.Point(oldOffset.X + deltaX, oldOffset.Y + deltaY);

            Editor.ClampViewOffset(newOffset);

            renderer.ViewOffset = newOffset;
            Invalidate(renderer, LayerType.LayerType_ALL);
        }
    }
}
