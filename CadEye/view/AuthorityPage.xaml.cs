using CadEye.ViewCS;
using System.Windows.Controls;


namespace CadEye.View
{
    /// <summary>
    /// AuthorityPage.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class AuthorityPage : UserControl
    {
        public Bridge vm;

        public AuthorityPage()
        {
            InitializeComponent();
            vm = Bridge.Instance;
            this.DataContext = vm;
        }
    }
}
