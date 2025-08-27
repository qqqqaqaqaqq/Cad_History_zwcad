using System.Windows.Forms; 

namespace CadEye.Lib
{
    class Open_folderdialog
    {
        public (string, bool) PathSetting()
        {
            using (var folder = new FolderBrowserDialog())
            {
                folder.Description = "폴더를 선택하세요";
                folder.ShowNewFolderButton = true;

                DialogResult result = folder.ShowDialog();

                if (result == DialogResult.OK)
                {
                    string path = folder.SelectedPath;
                    return (path, true);
                }
                else
                {
                    return ("Please check the folder path", false);
                }
            }
        }
    }
}
