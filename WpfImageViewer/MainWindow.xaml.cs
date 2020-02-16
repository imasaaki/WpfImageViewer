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
using System.IO.Compression;

namespace WpfImageViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int MaxImageNum = 9;
        private List<Image> imageList = new List<Image>();
        private List<Border> borderList = new List<Border>();
        private List<Viewbox> viewBoxList = new List<Viewbox>();
        private List<System.Windows.Shapes.Path> rectangleList = new List<System.Windows.Shapes.Path>();
        private List<RectangleGeometry> rectangleGeometryList1 = new List<RectangleGeometry>();
        private List<RectangleGeometry> rectangleGeometryList2 = new List<RectangleGeometry>();
        private List<GeometryGroup> geometryGroupList = new List<GeometryGroup>();

        private List<byte[]> pngImageList = new List<byte[]>();
        private List<string> pathList = new List<string>();
        private List<Rect> cropRectList = new List<Rect>();

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
        private int currentSelectedIndexInSingleDisplay = 0;
        private int currentPage = 0;
        private int totalPage = 0;

        public MainWindow()
        {
            InitializeComponent();

            for (int i = 0; i < MainWindow.MaxImageNum; i++)
            {
                this.imageList.Add(new Image());
                this.imageList[i].Stretch = Stretch.Uniform;

                this.borderList.Add(new Border());
                this.viewBoxList.Add(new Viewbox());
                this.viewBoxList[i].Stretch = Stretch.Uniform;
                this.rectangleList.Add(new System.Windows.Shapes.Path());
                this.viewBoxList[i].Child = this.rectangleList[i];

                this.rectangleList[i].Fill = new SolidColorBrush(Color.FromArgb(100, 125, 125, 125));

                this.rectangleGeometryList1.Add(new RectangleGeometry());
                this.rectangleGeometryList2.Add(new RectangleGeometry());
                this.geometryGroupList.Add(new GeometryGroup() { FillRule = FillRule.EvenOdd });
                this.geometryGroupList[i].Children.Add(this.rectangleGeometryList1[i]);
                this.geometryGroupList[i].Children.Add(this.rectangleGeometryList2[i]);

                this.rectangleList[i].Data = this.geometryGroupList[i];
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

            int ImageCount = MainWindow.LayoutImageWidthMap[this.currentLayoutType];

            for (int i = 0; i < ImageCount; i++)
            {
                var columnDefinition = new ColumnDefinition() { Width = new GridLength(1.0, GridUnitType.Star) };
                var rowDefinition = new RowDefinition() { Height = new GridLength(1.0, GridUnitType.Star) };
                this.Grid_ImageDisplay.ColumnDefinitions.Add(columnDefinition);
                this.Grid_ImageDisplay.RowDefinitions.Add(rowDefinition);
            }

            for (int i = 0; i < ImageCount * ImageCount; i++)
            {
                Grid.SetColumn(this.imageList[i], i % ImageCount);
                Grid.SetColumn(this.borderList[i], i % ImageCount);
                Grid.SetColumn(this.viewBoxList[i], i % ImageCount);
                Grid.SetRow(this.imageList[i], i / ImageCount);
                Grid.SetRow(this.borderList[i], i / ImageCount);
                Grid.SetRow(this.viewBoxList[i], i / ImageCount);

                this.Grid_ImageDisplay.Children.Add(this.imageList[i]);
                this.Grid_ImageDisplay.Children.Add(this.borderList[i]);
                this.Grid_ImageDisplay.Children.Add(this.viewBoxList[i]);
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
            this.Window_App.Focus();
            this.ClearPage();
        }

        private void ReadAllImages(string path)
        {
            if (Directory.Exists(path) == false)
            {
                return;
            }

            this.ClearData();

            var filePathList = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            filePathList = filePathList.Where(x => (x.EndsWith("jpg") || x.EndsWith("png") || x.EndsWith("JPG") || x.EndsWith("PNG"))).ToArray();

            this.pathList.AddRange(filePathList);

            foreach (var filePath in filePathList)
            {
                this.cropRectList.Add(new Rect());

                var bitmapImage = new BitmapImage(new Uri(filePath));

                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapImage));

                using (var stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    this.pngImageList.Add(stream.ToArray());
                }
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
                case Key.I:
                    this.ForwardSelection();
                    break;
                case Key.U:
                    this.BackwordSelection();
                    break;
                default:
                    break;
            }
        }

        private void MoveNextPage()
        {
            int displayCount = MainWindow.LayoutImageCountMap[this.currentLayoutType];

            if (displayCount * (this.currentPage + 1) < this.pathList.Count)
            {
                this.currentPage++;
            } 
            else
            {
                return;
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

        private void ForwardSelection()
        {
            int displayCount = MainWindow.LayoutImageCountMap[this.currentLayoutType];

            if (this.currentSelectedIndexInSingleDisplay < displayCount - 1)
            {
                this.currentSelectedIndexInSingleDisplay++;
            }

            this.UpdateImageSelection();
        }

        private void BackwordSelection()
        {
            if (this.currentSelectedIndexInSingleDisplay > 0)
            {
                this.currentSelectedIndexInSingleDisplay--;
            }

            this.UpdateImageSelection();
        }

        private void ClearData()
        {
            this.pngImageList.Clear();
            this.pathList.Clear();
            this.cropRectList.Clear();
        }

        private void ClearPage()
        {
            this.currentPage = 0;
            this.totalPage = (Math.Max(0, (this.pathList.Count() - 1)) / MainWindow.LayoutImageCountMap[this.currentLayoutType]);
            this.currentSelectedIndexInSingleDisplay = 0;
            this.UpdatePageInfo();
            this.UpdateImageDisplay();
            this.UpdateImageSelection();
        }

        private void UpdatePageInfo()
        {
            var pageText = string.Format("{0:D4}/{1:D4}", this.currentPage.ToString(), this.totalPage.ToString());
            this.Label_Page.Content = pageText;
        }

        private void UpdateImageDisplay()
        {
            int displayCount = MainWindow.LayoutImageCountMap[this.currentLayoutType];

            // 画像を描画
            for (int i = 0; i < displayCount; i++)
            {
                if (this.pathList.Count > displayCount * this.currentPage + i)
                {
                    using (var stream = new MemoryStream(this.pngImageList[displayCount * this.currentPage + i]))
                    {
                        BitmapDecoder decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        this.imageList[i].Source = decoder.Frames[0];

                        this.rectangleGeometryList1[i].Rect = new Rect(0, 0, this.imageList[i].Source.Width, this.imageList[i].Source.Height);
                        this.rectangleGeometryList2[i].Rect = new Rect(100, 100, 100, 100);
                    }
                }
                else
                {
                    this.imageList[i].Source = null;
                    this.rectangleGeometryList1[i].Rect = new Rect(0, 0, 0, 0); ;
                    this.rectangleGeometryList2[i].Rect = new Rect(0, 0, 0, 0); ;
                }
            }
        }

        private void UpdateImageSelection()
        {
            int displayCount = MainWindow.LayoutImageCountMap[this.currentLayoutType];

            // 画像を描画
            for (int i = 0; i < displayCount; i++)
            {
                this.borderList[i].BorderThickness = new Thickness(0.0d);

                if (this.currentSelectedIndexInSingleDisplay == i)
                {
                    this.borderList[i].BorderThickness = new Thickness(2.0d);
                    this.borderList[i].BorderBrush = new SolidColorBrush(Color.FromArgb(200, 212, 38, 180));
                }
            }
        }
    }
}
