using CadEye.ViewCS;
using System.Windows.Controls;
using System.Windows.Forms.Integration;

namespace CadEye.View
{
    /// <summary>
    /// PdfContainer2.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class PdfContainer2 : UserControl
    {
        private Bridge _vm { get; set; }

        public PdfContainer2()
        {
            InitializeComponent();
            _vm = Bridge.Instance;
            this.DataContext = _vm;
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
    }
}
