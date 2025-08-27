using CadEye.Lib;
using CadEye.ViewCS;
using System.Windows.Controls;


namespace CadEye.View
{
    /// <summary>
    /// File_Watcher_View.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class File_Watcher_View : UserControl
    {
        public Bridge vm { get; set; }
        public Pdf_ium_Viewer PdfBit = new Pdf_ium_Viewer();

        public File_Watcher_View()
        {
            InitializeComponent();
            vm = Bridge.Instance;
            this.DataContext = vm;
        }


        private void Grid_Cell_Selected(object sender, SelectedCellsChangedEventArgs e)
        {
            if (FildData_Grid.SelectedItem is EventEntry selected)
            {
                vm.Pdf_Load_btn2(selected);
            }
        }
    }
}
