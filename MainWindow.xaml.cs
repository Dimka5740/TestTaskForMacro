using System;
using System.Net;
using System.IO;
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
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;

namespace TestTaskForMacro
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        VideoReader stream;
        string sourceOrig;
        public MainWindow()
        {
            InitializeComponent();
            stream = new VideoReader();
            stream.NewFrame += new NewFrameEventHandler(videoNewFrame);
            sourceOrig = "http://demo.macroscop.com:8080/mobile?login=root&";
        }

        private void videoNewFrame(object sender, Bitmap eventArgs)
        {
            System.Drawing.Image img = (Bitmap)eventArgs.Clone();
            MemoryStream ms = new MemoryStream();
            img.Save(ms, ImageFormat.Bmp);
            ms.Seek(0, SeekOrigin.Begin);
            BitmapImage bi = new BitmapImage();
            bi.BeginInit();
            bi.StreamSource = ms;
            bi.EndInit();

            bi.Freeze();
            Dispatcher.BeginInvoke(new ThreadStart(delegate
            {
                imageForFrame.Source = bi;
            }));
        }

        private void buttonDwnLd_Click(object sender, RoutedEventArgs e)
        {
            string fileName = "fileName.xml";
            WebClient client = new WebClient();

            client.Headers.Add("user-agent", "Mozilla/4.0");

            client.DownloadFile("http://demo.macroscop.com:8080/configex?login=root", fileName);

            lb.ItemsSource = XmlParser.Parser(fileName);
        }

        private void tb_MouseDown(object sender, MouseButtonEventArgs e)
        {
            string source = sourceOrig;
            TextBlock textBlockBuf = (TextBlock)e.Source;

            source += $"channel={textBlockBuf.Text}&resolutionX=640&resolutionY=480&fps=0";

            stream.Source = source;
            stream.Start();
        }

        private void buttonStop_Click(object sender, RoutedEventArgs e)
        {
            stream.Stop();
        }
    }
}
