using CadEye.ViewCS;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;

namespace CadEye.View
{
    /// <summary>
    /// PdfContainer2.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PdfContainer2 : UserControl
    {
        public Bridge vm { get; set; }
        public PdfContainer2()
        {
            InitializeComponent();
            vm = Bridge.Instance;
            this.DataContext = vm;
        }

        WindowsFormsHost allhost;
        public void SetHost(WindowsFormsHost host)
        {
            Pdf_Grid2.Children.Add(host);
            allhost = host;
        }
        public void ResetHost()
        {
            Pdf_Grid2.Children.Clear();
        }

        private async void Pdf_Compare(object sender, RoutedEventArgs e)
        {
            await vm.Pdf_Compare_btn();
        }
    }
}
