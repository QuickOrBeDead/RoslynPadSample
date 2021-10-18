namespace RoslynPadSample.Converters
{
    using System;
    using System.Globalization;
    using System.Windows.Data;
    using System.Windows.Markup;

    using Microsoft.CodeAnalysis.CodeActions;

    using RoslynPad.Roslyn.CodeActions;

    internal sealed class CodeActionsConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var result = value as CodeAction;
            return result?.GetCodeActions() ?? value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}