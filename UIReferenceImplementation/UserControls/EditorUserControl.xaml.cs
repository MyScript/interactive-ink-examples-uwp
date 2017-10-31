// Copyright MyScript. All right reserved.

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.UI.Core;
using Windows.System;
using Windows.Graphics.Display;
using UIReferenceImplementation;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236
namespace MyScript.IInk.UIReferenceImplementation.UserControls
{
    public enum InputMode
    {
        AUTO = 0,
        TOUCH = 1,
        PEN = 2
    }

    public sealed partial class EditorUserControl : UserControl, IRenderTarget
    {
        private Engine _engine;
        private Editor _editor;
        private Renderer _renderer;

        public Engine Engine
        {
            get
            {
                return _engine;
            }

            set
            {
                _engine = value;
                Initialize();
            }
        }

        public Editor Editor
        {
            get
            {
                return _editor;
            }
        }

        public Renderer Renderer
        {
            get
            {
                return _renderer;
            }
        }

        private Layer _backgroundLayer;
        private Layer _modelLayer;
        private Layer _temporaryLayer;
        private Layer _captureLayer;

        private bool _onScroll = false;
        private bool _leftButtonPressed = false;
        private Graphics.Point _lastPointerPosition;

        public InputMode InputMode { get; set; }

        public EditorUserControl()
        {
            this.InitializeComponent();
            InputMode = InputMode.PEN;
        }

        public void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void Initialize()
        {
            var dpiX = DisplayInformation.GetForCurrentView().RawDpiX;
            var dpiY = DisplayInformation.GetForCurrentView().RawDpiY;

            // RawDpi properties can return 0 when the monitor doesn't provide physical dimensions and when the user is
            // in a clone or duplicate multiple -monitor setup.
            if (dpiX == 0 || dpiY == 0)
                dpiX = dpiY = 96;

            _renderer = _engine.CreateRenderer(dpiX, dpiY, this);

            _backgroundLayer = new Layer(backgroundCanvas, this, LayerType.BACKGROUND, _renderer);
            _modelLayer = new Layer(modelCanvas, this, LayerType.MODEL, _renderer);
            _temporaryLayer = new Layer(tempCanvas, this, LayerType.TEMPORARY, _renderer);
            _captureLayer = new Layer(captureCanvas, this, LayerType.CAPTURE, _renderer);

            _editor = _engine.CreateEditor(_renderer);
            _editor.SetViewSize((int)ActualWidth, (int)ActualHeight);
            _editor.SetFontMetricsProvider(new FontMetricsProvider(dpiX, dpiY));

            float verticalMarginPX = 60;
            float horizontalMarginPX = 40;
            float verticalMarginMM = 25.4f * verticalMarginPX / dpiY;
            float horizontalMarginMM = 25.4f * horizontalMarginPX / dpiX;
            _engine.Configuration.SetNumber("text.margin.top", verticalMarginMM);
            _engine.Configuration.SetNumber("text.margin.left", horizontalMarginMM);
            _engine.Configuration.SetNumber("text.margin.right", horizontalMarginMM);
            _engine.Configuration.SetNumber("math.margin.top", verticalMarginMM);
            _engine.Configuration.SetNumber("math.margin.bottom", verticalMarginMM);
            _engine.Configuration.SetNumber("math.margin.left", horizontalMarginMM);
            _engine.Configuration.SetNumber("math.margin.right", horizontalMarginMM);
        }

        /// <summary>Force inks layer to be redrawn</summary>
        public void Invalidate(LayerType layers)
        {
            Invalidate(_renderer, layers);
        }

        /// <summary>Force inks layer to be redrawn</summary>
        public void Invalidate(Renderer renderer, LayerType layers)
        {
            if ((layers & LayerType.BACKGROUND) != 0)
                _backgroundLayer.Update();
            if ((layers & LayerType.MODEL) != 0)
                _modelLayer.Update();
            if ((layers & LayerType.TEMPORARY) != 0)
                _temporaryLayer.Update();
            if ((layers & LayerType.CAPTURE) != 0)
                _captureLayer.Update();
        }

        /// <summary>Force ink layers to be redrawn according region</summary>
        public void Invalidate(Renderer renderer, int x, int y, int width, int height, LayerType layers)
        {
            if (height >= 0)
            {
                if ((layers & LayerType.BACKGROUND) != 0)
                    _backgroundLayer.Update(x, y, width, height);
                if ((layers & LayerType.MODEL) != 0)
                    _modelLayer.Update(x, y, width, height);
                if ((layers & LayerType.TEMPORARY) != 0)
                    _temporaryLayer.Update(x, y, width, height);
                if ((layers & LayerType.CAPTURE) != 0)
                    _captureLayer.Update(x, y, width, height);
            }
        }

        public void OnResize(int width, int height)
        {
            if (_editor != null)
                _editor.SetViewSize(width, height);
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
                    int x = (int)System.Math.Floor(region.X);
                    int y = (int)System.Math.Floor(region.Y);
                    int width = (int)System.Math.Ceiling(region.X + region.Width) - x;
                    int height = (int)System.Math.Ceiling(region.Y + region.Height) - y;

                    if (sender == captureCanvas)
                        _captureLayer.OnPaint(x, y, width, height);
                    else if (sender == tempCanvas)
                        _temporaryLayer.OnPaint(x, y, width, height);
                    else if (sender == modelCanvas)
                        _modelLayer.OnPaint(x, y, width, height);
                    else if (sender == backgroundCanvas)
                        _backgroundLayer.OnPaint(x, y, width, height);
                }
            }
        }

        private System.Int64 GetTimestamp(Windows.UI.Input.PointerPoint point)
        {
            // Convert the time to milliseconds
            return (System.Int64)point.Timestamp / 1000;
        }

        private PointerType GetPointerType(PointerRoutedEventArgs e)
        {
            switch (InputMode)
            {
                case InputMode.AUTO:
                    if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Pen)
                        return PointerType.PEN;
                    else
                        return PointerType.TOUCH;
                case InputMode.PEN:
                    return PointerType.PEN;
                case InputMode.TOUCH:
                    return PointerType.TOUCH;

                default:
                    return PointerType.PEN; // unreachable
            }
        }

        private void Capture_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var uiElement = sender as UIElement;
            var p = e.GetCurrentPoint(uiElement);

            // When using mouse consider left button only
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if (_leftButtonPressed || !p.Properties.IsLeftButtonPressed)
                    return;
                _leftButtonPressed = true;
            }

            var pointerType = GetPointerType(e);
            _lastPointerPosition = new Graphics.Point((float)p.Position.X, (float)p.Position.Y);
            _onScroll = false;

            // Send pointer down event to the editor
            _editor.PointerDown((float)p.Position.X, (float)p.Position.Y, GetTimestamp(p), p.Properties.Pressure, pointerType, (int)e.Pointer.PointerId);

            // Capture the pointer to the target.
            uiElement.CapturePointer(e.Pointer);

            // Prevent most handlers along the event route from handling the same event again.
            e.Handled = true;
        }

        private void Capture_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var uiElement = sender as UIElement;
            var p = e.GetCurrentPoint(uiElement);

            // Ignore pointer move when the pointing device is up
            if (!p.IsInContact)
                return;

            // When using mouse consider left button only
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if (!_leftButtonPressed)
                    return;
            }

            var pointerType = GetPointerType(e);
            var previousPosition = _lastPointerPosition;
            _lastPointerPosition = new Graphics.Point((float)p.Position.X, (float)p.Position.Y);

            if (!_onScroll && (pointerType == PointerType.TOUCH))
            {
                float deltaMin = 3.0f;
                float deltaX = _lastPointerPosition.X - previousPosition.X;
                float deltaY = _lastPointerPosition.Y - previousPosition.Y;

                _onScroll = _editor.IsScrollAllowed() && ((System.Math.Abs(deltaX) > deltaMin) || (System.Math.Abs(deltaY) > deltaMin));

                if (_onScroll)
                {
                    // Entering scrolling mode, cancel previous pointerDown event
                    _editor.PointerCancel((int)e.Pointer.PointerId);
                }
            }

            if (_onScroll)
            {
                // Scroll the view
                float deltaX = _lastPointerPosition.X - previousPosition.X;
                float deltaY = _lastPointerPosition.Y - previousPosition.Y;
                Scroll(-deltaX, -deltaY);
            }
            else
            {
                // Send pointer move event to the editor
                _editor.PointerMove((float)p.Position.X, (float)p.Position.Y, GetTimestamp(p), p.Properties.Pressure, pointerType, (int)e.Pointer.PointerId);
            }

            // Prevent most handlers along the event route from handling the same event again.
            e.Handled = true;
        }

        private void Capture_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var uiElement = sender as UIElement;
            var p = e.GetCurrentPoint(uiElement);

            // When using mouse consider left button only
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if (!_leftButtonPressed || p.Properties.IsLeftButtonPressed)
                    return;
                _leftButtonPressed = false;
            }

            var pointerType = GetPointerType(e);
            var previousPosition = _lastPointerPosition;
            _lastPointerPosition = new Graphics.Point((float)p.Position.X, (float)p.Position.Y);

            if (_onScroll)
            {
                // Scroll the view
                float deltaX = _lastPointerPosition.X - previousPosition.X;
                float deltaY = _lastPointerPosition.Y - previousPosition.Y;
                Scroll(-deltaX, -deltaY);

                // Exiting scrolling mode
                _onScroll = false;
            }
            else
            {
                // Send pointer move event to the editor
                _editor.PointerUp((float)p.Position.X, (float)p.Position.Y, GetTimestamp(p), p.Properties.Pressure, pointerType, (int)e.Pointer.PointerId);            
            }

            // Release the pointer captured from the target
            uiElement.ReleasePointerCapture(e.Pointer);

            // Prevent most handlers along the event route from handling the same event again.
            e.Handled = true;
        }

        private void Capture_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            var uiElement = sender as UIElement;
            var p = e.GetCurrentPoint(uiElement);

            // When using mouse consider left button only
            if (e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if (!_leftButtonPressed)
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
                _editor.PointerCancel((int)e.Pointer.PointerId);
            }

            // Prevent most handlers along the event route from handling the same event again.
            e.Handled = true;
        }

        private void Capture_PointerLost(object sender, PointerRoutedEventArgs e)
        {
        }

        private void Capture_PointerExited(object sender, PointerRoutedEventArgs e)
        {
        }

        private void Capture_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var uiElement = captureCanvas; //sender as UIElement;
            var properties = e.GetCurrentPoint(uiElement).Properties;

            if (properties.IsHorizontalMouseWheel == false)
            {
                int WHEEL_DELTA = 120;  // TODO : get from system ?

                var controlDown = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
                var shiftDown = CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Shif‌​t).HasFlag(CoreVirtualKeyStates.Down);
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
                    int SCROLL_SPEED = 10;
                    float delta = (float)(-SCROLL_SPEED * wheelDelta);
                    float deltaX = shiftDown ? delta : 0.0f;
                    float deltaY = shiftDown ? 0.0f : delta;

                    Scroll(deltaX, deltaY);
                }
            }

            // Prevent most handlers along the event route from handling the same event again.
            e.Handled = true;
        }

        public void ResetView(bool forceInvalidate)
        {
            _renderer.ViewScale = 1;
            _renderer.ViewOffset = new MyScript.IInk.Graphics.Point(0, 0);
            
            if (forceInvalidate)
                Invalidate(_renderer, LayerType.LayerType_ALL);
        }

        public void ZoomIn(uint delta)
        {
            _renderer.Zoom((float)delta * (110.0f / 100.0f));
            Invalidate(_renderer, LayerType.LayerType_ALL);
        }

        public void ZoomOut(uint delta)
        {
            _renderer.Zoom((float)delta * (100.0f / 110.0f));
            Invalidate(_renderer, LayerType.LayerType_ALL);
        }

        private void Scroll(float deltaX, float deltaY)
        {
            var oldOffset = _renderer.ViewOffset;
            var newOffset = new MyScript.IInk.Graphics.Point(oldOffset.X + deltaX, oldOffset.Y + deltaY);

            _editor.ClampViewOffset(newOffset);

            _renderer.ViewOffset = newOffset;
            Invalidate(_renderer, LayerType.LayerType_ALL);
        }
    }
}
