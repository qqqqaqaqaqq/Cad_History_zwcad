using CadEye.ViewCS;
using System.Windows.Controls;


namespace CadEye.View
{

    public partial class Cad_Text : UserControl
    {
        private Bridge _vm { get; set; }

        public Cad_Text()
        {
            InitializeComponent();
            _vm = Bridge.Instance;
            this.DataContext = _vm;
        }
    }
}
