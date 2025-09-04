using CadEye.ViewCS;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;

namespace CadEye.View
{
    /// <summary>
    /// PdfContainer.xaml에 대한 상호 작용 논리
    /// </summary>
    /// 

    public partial class PdfContainer : UserControl
    {
        public Bridge _vm { get; set; }
        public PdfContainer()
        {
            InitializeComponent();
            _vm = Bridge.Instance;
            this.DataContext = _vm;
        }

        WindowsFormsHost allhost;
        public void SetHost(WindowsFormsHost host)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Pdf_Grid.Children.Add(host);
                });
                allhost = host;
            }
            catch
            {
                Debug.WriteLine("SetHost : Error");
            }
        }
        public void ResetHost()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Pdf_Grid.Children.Clear();
            });
        }
    }
}
