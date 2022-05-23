// Copyright @ MyScript. All rights reserved.

namespace MyScript.IInk.UIReferenceImplementation.Extensions
{
    public static class Primitives
    {
        private const float MillimetersPerInch = 25.4f;

        public static float FromPixelToMillimeter(this double source, float dpi)
        {
            return ((float)source).FromPixelToMillimeter(dpi);
        }

        public static float FromMillimeterToPixel(this float source, float dpi)
        {
            // DPI: dots or pixels per inch
            // => dpi = pixels / inch
            // => pixels = dpi * inch
            var inch = source / MillimetersPerInch;
            return dpi * inch;
        }

        public static float FromPixelToMillimeter(this float source, float dpi)
        {
            // DPI: dots or pixels per inch
            // => dpi = pixels / inch
            // => inch = pixels / dpi
            var inch = source / dpi;
            return inch * MillimetersPerInch;
        }
    }
}