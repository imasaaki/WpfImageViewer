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
        private List<Image> imageList = new List<Image>();

        private List<BitmapImage> bitmapImageList = new List<BitmapImage>();

        private List<byte[]> pngImageList = new List<byte[]>();

        private enum LayoutType { 
            L1x1,
            L2x2,
            L3x3,
        }

        private static readonly IReadOnlyDictionary<LayoutType, int> LayoutImageCountMap = new Dictionary<LayoutType, int>()
        {
            { LayoutType.L1x1, 1},
            { LayoutType.L2x2, 4},
            { LayoutType.L3x3, 9},
        };

        private static readonly IReadOnlyDictionary<LayoutType, int> LayoutImageWidthMap = new Dictionary<LayoutType, int>()
        {
            { LayoutType.L1x1, 1},
            { LayoutType.L2x2, 2},
            { LayoutType.L3x3, 3},
        };

        private LayoutType currentLayoutType = LayoutType.L1x1;
        private int currentSelectedImageIndex = 0;
        private int currentPage = 0;
        private int totalPage = 0;

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

            this.ClearPage();
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
            this.ClearPage();
        }

        private void ReadAllImages(string path)
        {
            if (Directory.Exists(path) == false)
            {
                return;
            }

            this.pngImageList.Clear();

            var filePathList = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            filePathList = filePathList.Where(x => (x.EndsWith("jpg") || x.EndsWith("png") || x.EndsWith("JPG") || x.EndsWith("PNG"))).ToArray();

            foreach (var filePath in filePathList)
            {
                var bitmapImage = new BitmapImage(new Uri(filePath));

                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapImage));

                using (var stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    this.pngImageList.Add(stream.ToArray());
                }

                //using (var stream = File.OpenRead(filePath))
                //{
                //    this.Dispatcher.Invoke(() =>
                //    {
                //        var buffer = new byte[stream.Length];
                //        stream.Read(buffer, 0, buffer.Length);


                //        var bitmap = new BitmapImage();
                //        bitmap.BeginInit();
                //        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                //        bitmap.StreamSource = stream;
                //        bitmap.EndInit();
                //        this.bitmapImageList.Add(bitmap);


                //    });
                //}
            }
        }

        private void Window_App_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
               case Key.K:
                    this.MoveNextPage();
                    break;
               case Key.J:
                    this.MoveLastPage();
                    break;
                default:
                    break;
            }
        }

        private void MoveNextPage()
        {
            int displayCount = MainWindow.LayoutImageCountMap[this.currentLayoutType];

            if (this.pngImageList.Count < displayCount * this.currentPage)
            {
                return;
            } 
            else
            {
                this.currentPage++;
            }

            this.UpdateImageDisplay();
            this.UpdatePageInfo();
        }

        private void MoveLastPage()
        {
            if (0 == this.currentPage)
            {
                return;
            }
            else
            {
                this.currentPage--;
            }

            this.UpdateImageDisplay();
            this.UpdatePageInfo();
        }

        private void ClearPage()
        {
            this.currentPage = 0;
            this.totalPage = ((this.pngImageList.Count() - 1) / MainWindow.LayoutImageCountMap[this.currentLayoutType]);
            this.UpdatePageInfo();
            this.UpdateImageDisplay();
        }

        private void UpdatePageInfo()
        {
            var pageText = string.Format("{0:D4}/{1:D4}", this.currentPage.ToString(), this.totalPage.ToString());
            this.Label_Page.Content = pageText;
        }

        private void UpdateImageDisplay()
        {
            int displayCount = MainWindow.LayoutImageCountMap[this.currentLayoutType];

            for (int i = 0; i < displayCount; i++)
            {

                if (this.pngImageList.Count > displayCount * this.currentPage + i)
                {
                    using (var stream = new MemoryStream(this.pngImageList[displayCount * this.currentPage + i]))
                    {
                        BitmapDecoder decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        this.imageList[i].Source = decoder.Frames[0];
                    }
                }
                else
                {
                    this.imageList[i].Source = null;
                }

                //if (this.bitmapImageList.Count > displayCount * this.currentPage + i)
                //{
                //    this.imageList[i].Source = this.bitmapImageList[displayCount * this.currentPage + i];
                //}
                //else
                //{
                //    this.imageList[i].Source = null;
                //}



            }
        }
    }
}
