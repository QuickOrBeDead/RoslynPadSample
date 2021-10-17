namespace RoslynPadSample.Formatting
{
    using System;
    using System.Globalization;
    using System.Windows.Data;
    using System.Windows.Markup;

    using Microsoft.CodeAnalysis.CodeActions;

    using RoslynPad.Roslyn;
    using RoslynPad.Roslyn.CodeActions;

    internal sealed class CodeActionToGlyphConverter : MarkupExtension, IValueConverter
    {
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return ((CodeAction)value ?? throw new InvalidOperationException($"CodeActionToGlyphConverter value is null. Target type: {targetType.FullName}")).GetGlyph().ToImageSource();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}