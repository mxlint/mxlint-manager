using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace MxLintManager
{
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c) => !(bool)v;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => !(bool)v;
    }
}
