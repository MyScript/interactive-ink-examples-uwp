// Copyright @ MyScript. All rights reserved.

using System;
using System.Diagnostics;
using Windows.Devices.Input;
using Windows.UI.Input.Inking;
using MyScript.IInk.Graphics;
using MyScript.IInk.UIReferenceImplementation.Constants;

namespace MyScript.IInk.UIReferenceImplementation.Extensions
{
    public static class SDK
    {
        #region Editor

        public static void Apply(this Editor source, InkDrawingAttributes attributes)
        {
            if (!(source.ToolController is ToolController controller)) return;
            var dpi = source.Renderer?.DpiX ?? 96;
            var color = attributes.Color.ToNative().ToHex();
            var strokeWidth = attributes.DrawAsHighlighter ? attributes.Size.Height : attributes.Size.Width;
            var css =
                $"{StyleKeys.Color}: {color}; " +
                $"{StyleKeys.MyScriptPenWidth}: {strokeWidth.FromPixelToMillimeter(dpi)}";
            var pointerTool = attributes.DrawAsHighlighter ? PointerTool.HIGHLIGHTER : PointerTool.PEN;
            controller.SetToolStyle(pointerTool, css);
        }

        #endregion

        #region Pointer Type

        public static PointerType ToNative(this PointerDeviceType source)
        {
            switch (source)
            {
                case PointerDeviceType.Touch:
                    return PointerType.TOUCH;
                case PointerDeviceType.Pen:
                    return PointerType.PEN;
                case PointerDeviceType.Mouse:
                    return PointerType.MOUSE;
                default:
                    throw new ArgumentOutOfRangeException(nameof(source), source, null);
            }
        }

        #endregion

        #region Colors

        public static string ToHex(this Color source)
        {
            return $"#{source.R:X2}{source.G:X2}{source.B:X2}{source.A:X2}";
        }

        public static Color ToNative(this Windows.UI.Color source)
        {
            return new Color(source.R, source.G, source.B, source.A);
        }

        #endregion
    }
}