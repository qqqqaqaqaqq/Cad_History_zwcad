using CadEye.ViewCS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CadEye.View
{
    /// <summary>
    /// Cad_Text_History.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Cad_Text_History : UserControl
    {
        private Bridge _vm { get; set; }


        public Cad_Text_History()
        {
            InitializeComponent();
            _vm = Bridge.Instance;
            this.DataContext = _vm;
        }

        private void Overlay_Tag(object sender, TextChangedEventArgs e)
        {
            if (Tag_SearchText.Text.Length > 0)
                PlaceHolder_Overlay_Tag.Visibility = Visibility.Hidden;
            else
                PlaceHolder_Overlay_Tag.Visibility = Visibility.Visible;
        }
        private void Overlay_FileName(object sender, TextChangedEventArgs e)
        {
            if (File_SearchText.Text.Length > 0)
                PlaceHolder_Overlay_FileName.Visibility = Visibility.Hidden;
            else
                PlaceHolder_Overlay_FileName.Visibility = Visibility.Visible;
        }
    }
}
