// Copyright @ MyScript. All rights reserved.

using System;
using Windows.UI.Xaml.Data;

namespace MyScript.IInk.UIReferenceImplementation.Converters
{
    public class BoolNegationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is bool b && !b;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}