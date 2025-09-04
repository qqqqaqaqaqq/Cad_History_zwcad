using CadEye.ViewCS;
using System.Windows.Controls;


namespace CadEye.View
{
    /// <summary>
    /// AuthorityPage.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class AuthorityPage : UserControl
    {
        private Bridge _vm { get; set; }

        public AuthorityPage()
        {
            {
                InitializeComponent();
                _vm = Bridge.Instance;
                this.DataContext = _vm;
            }
        }
    }
}
