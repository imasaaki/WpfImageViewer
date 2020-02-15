using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;

namespace WpfImageViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int MaxImageNum = 9;
        private const int MaxImageWidth = 1920;
        private const int MaxImageHeight = 1080;
        private List<Image> imageList = new List<Image>();

        private List<BitmapImage> bitmapImageList = new List<BitmapImage>();

        private enum LayoutType { 
            L1x1,
            L2x2,
            L3x3,
        }

        private LayoutType currentLayoutType = LayoutType.L1x1;
        private int currentSelectedImageIndex = 0;
        private int currentPage = 0;

        public MainWindow()
        {
            InitializeComponent();

            for (int i = 0; i < MainWindow.MaxImageNum; i++)
            {
                this.imageList.Add(new Image());
                this.imageList[i].Stretch = Stretch.Uniform;
            }

            this.ComboBox_ImageLayout.ItemsSource = Enum.GetValues(typeof(LayoutType)).Cast<LayoutType>();
            this.ComboBox_ImageLayout.SelectedIndex = (int)this.currentLayoutType;
        }

        private void ComboBox_ImageLayout_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.currentLayoutType = (LayoutType)this.ComboBox_ImageLayout.SelectedIndex;

            this.Grid_ImageDisplay.Children.Clear();
            this.Grid_ImageDisplay.ColumnDefinitions.Clear();
            this.Grid_ImageDisplay.RowDefinitions.Clear();

            int ImageCount = 0;

            switch (this.currentLayoutType) 
            {
                case LayoutType.L1x1:
                    ImageCount = 1;                
                    break;
                case LayoutType.L2x2:
                    ImageCount = 2;
                    break;
                case LayoutType.L3x3:
                    ImageCount = 3;
                    break;
                default:
                    break;
            }

            for (int i = 0; i < ImageCount; i++)
            {
                var columnDefinition = new ColumnDefinition() { Width = new GridLength(1.0, GridUnitType.Star) };
                var rowDefinition = new RowDefinition() { Height = new GridLength(1.0, GridUnitType.Star) };
                this.Grid_ImageDisplay.ColumnDefinitions.Add(columnDefinition);
                this.Grid_ImageDisplay.RowDefinitions.Add(rowDefinition);
            }

            for (int i = 0; i < ImageCount * ImageCount; i++)
            {
                Grid.SetRow(this.imageList[i], i % ImageCount);
                Grid.SetColumn(this.imageList[i], i / ImageCount);

                this.Grid_ImageDisplay.Children.Add(this.imageList[i]);

                if (this.bitmapImageList.Count > i)
                {
                    this.imageList[i].Source = this.bitmapImageList[i];
                }
            }
        }

        private void TextBlock_rootPath_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop, true))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
            e.Handled = true;

        }

        private void TextBlock_rootPath_Drop(object sender, DragEventArgs e)
        {
            var dropFiles = e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[];
            if (dropFiles == null) return;
            this.TextBlock_rootPath.Text = dropFiles[0];
        }

        private async void Button_rootPath_Click(object sender, RoutedEventArgs e)
        {
            this.Window_App.IsEnabled = false;

            var path = this.TextBlock_rootPath.Text;

            await Task.Run(()=> this.ReadAllImages(path));

            this.Window_App.IsEnabled = true;
        }

        private void ReadAllImages(string path)
        {
            var filePathList = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            filePathList = filePathList.Where(x => (x.EndsWith("jpg") || x.EndsWith("png"))).ToArray();

            foreach (var filePath in filePathList)
            {
                this.Dispatcher.Invoke(()=> this.bitmapImageList.Add(new BitmapImage(new Uri(filePath))));
            }
        }

        private void Window_App_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
               case Key.K:
                    this.MoveNextPage();
                    break;
               default:
                    break;
            }
        }

        private void MoveNextPage()
        {
            int ImageCount = 0;
            int moveCount = 0;

            switch (this.currentLayoutType)
            {
                case LayoutType.L1x1:
                    ImageCount = 1;
                    moveCount = this.currentPage * 1;
                    break;
                case LayoutType.L2x2:
                    ImageCount = 2;
                    moveCount = this.currentPage * 4;
                    break;
                case LayoutType.L3x3:
                    ImageCount = 3;
                    moveCount = this.currentPage * 9;
                    break;
                default:
                    break;
            }

            if (this.bitmapImageList.Count < moveCount)
            {
                return;
            } else
            {
                this.currentPage++;
            }

            for (int i = 0; i < ImageCount * ImageCount; i++)
            {
                if (this.bitmapImageList.Count > moveCount + i)
                {
                    this.imageList[i].Source = this.bitmapImageList[moveCount + i];
                }
                else
                {
                    this.imageList[i].Source = null;
                }
            }
        }
    }
}
