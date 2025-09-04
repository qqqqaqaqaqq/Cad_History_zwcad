using CadEye.ViewCS;
using System.Windows.Controls;


namespace CadEye.View
{
    /// <summary>
    /// File_Watcher_View.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class File_Watcher_View : UserControl
    {
        private Bridge _vm { get; set; }

        public File_Watcher_View()
        {
            InitializeComponent();
            _vm = Bridge.Instance;
            this.DataContext = _vm;
        }
    }
}
