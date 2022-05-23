// Copyright @ MyScript. All rights reserved.

using System;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using MyScript.IInk.UIReferenceImplementation.Extensions;

namespace MyScript.IInk.UIReferenceImplementation.UserControls
{
    public sealed partial class InkToolbar
    {
        public static readonly DependencyProperty EditorProperty =
            DependencyProperty.Register("Editor", typeof(Editor), typeof(InkToolbar),
                new PropertyMetadata(default(Editor)));

        public static readonly DependencyProperty IsActivePenEnabledProperty =
            DependencyProperty.Register("IsActivePenEnabled", typeof(bool), typeof(InkToolbar),
                new PropertyMetadata(true, OnIsActivePenEnabledValueChanged));

        public InkToolbar()
        {
            InitializeComponent();
        }

        /// <summary>
        ///     <inheritdoc cref="Editor" />
        /// </summary>
        public Editor Editor
        {
            get => GetValue(EditorProperty) as Editor;
            set => SetValue(EditorProperty, value);
        }

        public bool IsActivePenEnabled
        {
            get => (bool)GetValue(IsActivePenEnabledProperty);
            set => SetValue(IsActivePenEnabledProperty, value);
        }

        private static void OnIsActivePenEnabledValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is InkToolbar toolbar) || !(toolbar.Editor?.ToolController is ToolController controller)) return;
            var isActivePenEnabled = e.NewValue is bool b && b;
            var pointerTool = controller.GetToolForType(PointerType.PEN);
            controller.SetToolForType(PointerType.TOUCH, isActivePenEnabled ? PointerTool.HAND : pointerTool);
            // if active pen is activated and the hand tool is previously selected,
            // it will be disabled and the active tool will fall back to the pen tool.
            if (isActivePenEnabled && pointerTool == PointerTool.HAND)
                toolbar.Toolbar.ActiveTool = toolbar.Toolbar.GetToolButton(InkToolbarTool.BallpointPen);
        }

        private void OnActiveToolChanged(Windows.UI.Xaml.Controls.InkToolbar sender, object args)
        {
            if (!(Editor?.ToolController is ToolController controller)) return;
            var tool = sender.ActiveTool;
            switch (tool.ToolKind)
            {
                case InkToolbarTool.BallpointPen:
                case InkToolbarTool.Pencil:
                    OnPenActivated(controller, IsActivePenEnabled);
                    break;
                case InkToolbarTool.Highlighter:
                    OnHighlighterActivated(controller, IsActivePenEnabled);
                    break;
                case InkToolbarTool.Eraser:
                    OnEraserActivated(controller, IsActivePenEnabled);
                    break;
                case InkToolbarTool.CustomPen:
                    break;
                case InkToolbarTool.CustomTool:
                    if (tool == HandTool) OnHandToolActivated(controller);
                    else if (tool == SelectorTool) OnSelectorToolActivated(controller, IsActivePenEnabled);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnEraseAllClicked(Windows.UI.Xaml.Controls.InkToolbar sender, object args)
        {
            Editor?.Clear();
        }

        private void OnInkDrawingAttributesChanged(Windows.UI.Xaml.Controls.InkToolbar sender, object args)
        {
            Editor?.Apply(sender.InkDrawingAttributes);
        }

        private void OnInkToolbarPenButtonLoaded(object sender, RoutedEventArgs e)
        {
            if (!(sender is InkToolbarPenButton target)) return;
            Editor?.Apply(new InkDrawingAttributes
            {
                Color = target.SelectedBrush is SolidColorBrush brush ? brush.Color : default(Color),
                DrawAsHighlighter = target is InkToolbarHighlighterButton,
                Size = new Size(target.SelectedStrokeWidth, target.SelectedStrokeWidth)
            });
        }

        #region On Tool Activated (Eraser, Hand, Highlighter, Pen, Selector)

        private static void OnEraserActivated(ToolController controller, bool isActivePenEnabled)
        {
            controller.SetToolForType(PointerType.PEN, PointerTool.ERASER);
            controller.SetToolForType(PointerType.MOUSE, PointerTool.ERASER);
            controller.SetToolForType(PointerType.TOUCH,
                isActivePenEnabled ? PointerTool.HAND : PointerTool.ERASER);
        }

        private static void OnHandToolActivated(ToolController controller)
        {
            controller.SetToolForType(PointerType.MOUSE, PointerTool.HAND);
            controller.SetToolForType(PointerType.TOUCH, PointerTool.HAND);
            controller.SetToolForType(PointerType.PEN, PointerTool.HAND);
        }

        private static void OnHighlighterActivated(ToolController controller, bool isActivePenEnabled)
        {
            controller.SetToolForType(PointerType.PEN, PointerTool.HIGHLIGHTER);
            controller.SetToolForType(PointerType.MOUSE, PointerTool.HIGHLIGHTER);
            controller.SetToolForType(PointerType.TOUCH,
                isActivePenEnabled ? PointerTool.HAND : PointerTool.HIGHLIGHTER);
        }

        private static void OnPenActivated(ToolController controller, bool isActivePenEnabled)
        {
            controller.SetToolForType(PointerType.PEN, PointerTool.PEN);
            controller.SetToolForType(PointerType.MOUSE, PointerTool.PEN);
            controller.SetToolForType(PointerType.TOUCH,
                isActivePenEnabled ? PointerTool.HAND : PointerTool.PEN);
        }

        private static void OnSelectorToolActivated(ToolController controller, bool isActivePenEnabled)
        {
            controller.SetToolForType(PointerType.MOUSE, PointerTool.SELECTOR);
            controller.SetToolForType(PointerType.PEN, PointerTool.SELECTOR);
            if (!isActivePenEnabled) controller.SetToolForType(PointerType.TOUCH, PointerTool.SELECTOR);
        }

        #endregion
    }
}