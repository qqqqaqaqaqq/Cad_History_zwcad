using CadEye.ViewCS;
using System.Windows;

namespace CadEye.View
{
    public partial class Cad_FileView : System.Windows.Controls.UserControl
    {
        private Bridge _vm { get; set; }

        public Cad_FileView()
        {
            InitializeComponent();
            _vm = Bridge.Instance;
            this.DataContext = _vm;
        }
        public void Unvisible_btn()
        {
            DB_Update_btn.Visibility = Visibility.Hidden;
            DB_Reset_btn.Visibility = Visibility.Hidden;
            DB_Write_btn.Visibility = Visibility.Hidden;
            DB_Read_btn.Visibility = Visibility.Visible;
            DB_Backup_btn.Visibility = Visibility.Hidden;
        }
    }
}