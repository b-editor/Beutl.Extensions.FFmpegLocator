using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Beutl.Extensions.FFmpegLocator;

public partial class LocateFFmpegPage : UserControl
{
    public LocateFFmpegPage()
    {
        InitializeComponent();
        outputTextBlock.SizeChanged += OnOutputTextBlockSizeChanged;
    }

    private void OnOutputTextBlockSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        outputScrollViewer.Offset = outputScrollViewer.Offset.WithY(outputTextBlock.Bounds.Height);
    }
}