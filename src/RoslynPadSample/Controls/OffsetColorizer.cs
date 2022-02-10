﻿namespace RoslynPadSample.Controls
{
    using System.Windows.Media;

    using ICSharpCode.AvalonEdit.Document;
    using ICSharpCode.AvalonEdit.Rendering;

    public sealed class OffsetColorizer : DocumentColorizingTransformer
    {
        public OffsetColorizer(Color color)
        {
            Brush = new SolidColorBrush(color);
        }

        public int StartOffset { get; set; }

        public int EndOffset { get; set; }

        public Brush Brush { get; }

        protected override void ColorizeLine(DocumentLine line)
        {
            if (line.Length == 0)
            {
                return;
            }

            if (line.Offset < StartOffset || line.Offset > EndOffset)
            {
                return;
            }

            int start = line.Offset > StartOffset ? line.Offset : StartOffset;
            int end = EndOffset > line.EndOffset ? line.EndOffset : EndOffset;

            ChangeLinePart(start, end, element => element.TextRunProperties.SetForegroundBrush(Brush));
        }
    }
}