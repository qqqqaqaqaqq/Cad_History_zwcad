using CadEye.ViewCS;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CadEye.View
{
    /// <summary>
    /// PdfContainer.xaml에 대한 상호 작용 논리
    /// </summary>
    /// 

    public partial class PdfContainer : UserControl
    {
        public Bridge vm { get; set; }
        public PdfContainer()
        {
            InitializeComponent();
            vm = Bridge.Instance;
            this.DataContext = vm;
        }

        WindowsFormsHost allhost;
        public void SetHost(WindowsFormsHost host)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Pdf_Grid.Children.Add(host);
            });
            allhost = host;
        }
        public void ResetHost()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Pdf_Grid.Children.Clear();
            });
        }
        public void Image_Form(object sender, RoutedEventArgs e)
        {
            vm.OpenPdfInNewWindow();
        }
    }
}
