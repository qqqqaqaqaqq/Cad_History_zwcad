using System.Windows.Forms;

namespace CadEye.Lib
{
    public class Open_filedialog
    {
        public static string Open_file()
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            fileDialog.Multiselect = false;

            DialogResult result = fileDialog.ShowDialog();
            string path = fileDialog.FileName;
            return path;
        }
    }
}

