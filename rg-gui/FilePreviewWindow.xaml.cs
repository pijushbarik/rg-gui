using System.Collections.Generic;
using System.Windows;

namespace rg_gui
{
    public partial class FilePreviewWindow : Window
    {
        public FilePreviewWindow(string fullPath, IReadOnlyList<MainWindow.ResultLine> lines)
        {
            InitializeComponent();

            HorizontalWheelScroll.Attach(this);

            Title = fullPath;
            gridPreview.ItemsSource = lines;
        }
    }
}
