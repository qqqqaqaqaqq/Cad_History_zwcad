using CadEye.Lib;
using CadEye.ViewCS;
using LiteDB;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace CadEye.ViewCs
{
    public class FunctionCollection
    {
        public Bridge _vb
        {
            get { return Bridge.Instance; }
        }

        private static FunctionCollection _instance;
        public static FunctionCollection Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new FunctionCollection();
                }
                return _instance;
            }
        }

        public Data_base _db = new Data_base();
        File_Watcher watcher = new File_Watcher();
        public ZwCad_Lib cad = new ZwCad_Lib();
        public File_Check file_check = new File_Check();
        public Pdf_ium_Viewer PdfBit = new Pdf_ium_Viewer();
        public FileSystemWatcher _watcher;
        public FileSystemWatcher _watcher_repository;


        public async Task MainView_Start_Load()
        {
            await Task.Delay(1);
            Lib.File_Check filecheck = new Lib.File_Check();
            ConcurrentBag<Child_File> item_insert = filecheck.AllocateData(_vb.folderpath);
            _db.Child_File_Table(null, item_insert, DbAction.AllUpsert);
        }

        public async Task Reset()
        {
            _db.Child_File_Table(null, null, DbAction.DeleteAll);
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string folder = System.IO.Path.Combine(baseDir, $"repository\\{_vb.projectname}");
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _vb.Host1 = new WindowsFormsHost();
                _vb.Host2 = new WindowsFormsHost();
                _vb.Pdf1.ResetHost();
                _vb.Pdf2.ResetHost();
            });
        }

        public async Task File_View_input()
        {
            var child_db = DatabaseProvider.Child_Node;
            var child_nodes = child_db.FindAll();

            var fileInfos = await Task.Run(() =>
            {
                try
                {
                    var input_file = child_nodes.Select(node => new FileInfoItem
                    {
                        Key = node.Key,
                        FilePath = node.File_FullName
                    }).ToList();

                    return input_file;
                }
                catch (LiteDB.LiteException ex)
                {
                    Debug.WriteLine($"[FILE_INPUT ERROR] {ex.Message}");
                    return new List<FileInfoItem>();
                }
            });

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                _vb.Files.Clear();
                foreach (var file in fileInfos)
                {
                    _vb.Files.Add(file);
                }
            });
        }

        public async Task Exclude_FileINFO()
        {
            var child_db = DatabaseProvider.Child_Node;
            var child_nodes = child_db.FindAll();

            if (child_nodes == null) return;

            DateTime time = DateTime.Now;
            time = new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, time.Second);

            await Task.Run(() =>
            {
                try
                {
                    foreach (var child_node in child_nodes)
                    {
                        if (!File.Exists(child_node.File_FullName)) continue;

                        byte[] has = file_check.Hash_Allocated_Unique(child_node.File_FullName);

                        if (child_node.Event.Count() > 0)
                        {
                            continue;
                        }

                        Child_File source_node = watcher.SettingSourceNode(child_node.File_FullName, child_node.Key, has, child_node);
                        string Event = "First Create";

                        source_node.Event.Add(new EventEntry()
                        {
                            Key = source_node.Key,
                            Time = time,
                            Type = Event,
                        });

                        _db.Child_File_Table(source_node, null, DbAction.Update);
                        watcher.File_Copy(child_node.File_FullName, time, child_node.Key, Event);
                    }
                }
                catch (Exception ex) 
                {
                    _vb.Event_History.Add(ex.Message);
                }
            });
        }

        public async Task<bool> TryPrepareTempFiles()
        {
            Random random = new Random();
            string relative_Path = AppDomain.CurrentDomain.BaseDirectory;
            _vb.sourceDbFile = System.IO.Path.Combine(relative_Path, $"{_vb.projectname}.db");
            _vb.sourceLogFile = System.IO.Path.Combine(relative_Path, $"{_vb.projectname}-log.db");

            for (int attempt = 0; attempt < 100; attempt++)
            {
                DatabaseProvider.Dispose();
                await Task.Delay(100);

                int rnd = random.Next(3000);
                _vb.targetFileName = $"{_vb.projectname}_{rnd}";
                _vb.targetDbFile = System.IO.Path.Combine(relative_Path, $"{_vb.targetFileName}.db");
                _vb.targetLogFile = System.IO.Path.Combine(relative_Path, $"{_vb.targetFileName}-log.db");

                if (!File.Exists(_vb.targetDbFile) && !File.Exists(_vb.targetLogFile)) return true;

                await Task.Delay(100);
            }
            return false;
        }

        public async Task<bool> Db_Read_Test()
        {
            await Task.Delay(50);
            try
            {
                var test = DatabaseProvider.Child_Node.FindOne(Query.All());
                return true;
            }
            catch (LiteDB.LiteException ex)
            {
                Debug.WriteLine($"[READ MODE SAFETY CHECK FAILED] {ex.Message}");
                return false;
            }
        }

        public async Task DeleteFileSafely()
        {
            if (!File.Exists(_vb.user_file) && !File.Exists(_vb.user_log))
                return;

            const int maxRetries = 10;
            const int delayMs = 200;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    File.Delete(_vb.user_file);
                    File.Delete(_vb.user_log);
                    if (!File.Exists(_vb.user_file) && !File.Exists(_vb.user_log))
                        break;
                }
                catch (IOException)
                {
                    await Task.Delay(delayMs);
                }
                catch (UnauthorizedAccessException)
                {
                    await Task.Delay(delayMs);
                }
            }
        }

        public async Task<bool> SafeCopy(string source, string destination)
        {
            const int retries = 5;

            for (int i = 0; i < retries; i++)
            {
                try
                {
                    using (var src = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var dest = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await src.CopyToAsync(dest);
                    }

                    return true;
                }
                catch (IOException)
                {
                    await Task.Delay(50);
                }
            }

            Debug.WriteLine($"[SAFE COPY ERROR] {source} -> {destination} failed after {retries} retries.");
            return false;
        }

        public async Task<string> File_Select()
        {
            string path = "";
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                OpenFileDialog fileDialog = new OpenFileDialog();
                fileDialog.Multiselect = false;

                DialogResult result = fileDialog.ShowDialog();
                path = fileDialog.FileName;
            });
            return path;
        }

        public async Task<string> Folder_Select()
        {
            string path = "";
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                using (var folder = new FolderBrowserDialog())
                {
                    folder.Description = "폴더를 선택하세요";
                    folder.ShowNewFolderButton = true;

                    DialogResult result = folder.ShowDialog();

                    path = folder.SelectedPath;
                }
            });
            return path;
        }

        public async Task Folder_Open()
        {
            try
            {
                var child_db = DatabaseProvider.Child_Node;
                var child_node = child_db.FindOne(x => x.Key == _vb.selected_key);

                var folder = Path.GetDirectoryName(child_node.File_FullName);
                await Task.Delay(100);

                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = folder,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Open_Folder : {ex}");
            }
        }

        public void File_Open()
        {
            try
            {
                var child_db = DatabaseProvider.Child_Node;
                var item = child_db.FindOne(x => x.Key == _vb.selected_key);

                Debug.WriteLine(item.File_FullName);
                if (item == null)
                {
                    Debug.WriteLine($"파일 '{item.File_FullName}'을(를) 찾을 수 없습니다.");
                    return;
                }

                string path = item.File_FullName;

                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = path,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"파일 열기 실패: {ex.Message}");
                    }
                }
                else
                {
                    Debug.WriteLine($"파일 경로가 유효하지 않음: {path}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Dubliclick_Open : {ex.Message}");
            }
        }

        public async Task Tag_Enter_SearchAsync(string text)
        {
            try
            {
                var searchText = string.IsNullOrWhiteSpace(text) ? "" : text.Replace(" ", "");

                var filesWithKey = await Task.Run(() =>
                {
                    var child_db = DatabaseProvider.Child_Node;
                    var child_nodes = child_db.FindAll();

                    if (!string.IsNullOrWhiteSpace(searchText))
                    {
                        return child_nodes
                            .Where(node => node.Feature != null &&
                                   node.Feature.Any(f => f != null && f.Replace(" ", "")
                                        .IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0))
                            .Select(node => new FileInfoItem
                            {
                                Key = node.Key,
                                FilePath = node.File_FullName
                            })
                            .ToList();
                    }
                    else
                    {
                        return child_nodes
                            .Select(node => new FileInfoItem
                            {
                                Key = node.Key,
                                FilePath = node.File_FullName
                            })
                            .ToList();
                    }
                });

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                  {
                      _vb.Files.Clear();
                      foreach (var file in filesWithKey)
                      {
                          _vb.Files.Add(file);
                      }
                  });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Tag_Enter_SearchAsync : {ex.Message}");
            }
        }

        public async Task LoadPdf()
        {
            var child_db = DatabaseProvider.Child_Node;
            var child_node = await Task.Run(() => child_db.FindOne(x => x.Key == _vb.selectedFileinfo.Key));
            if (child_node == null)
            {
                return;
            }
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
              {
                  if (child_node.Image.Count == 0)
                  {
                      _vb.Pdf1.ResetHost();
                      _vb.Pdf2.ResetHost();
                      return;
                  }
                  else
                  {
                      _vb.Host1 = PdfBit.Pdf_Created(child_node.Image[child_node.Image.Count - 1].Data);
                      _vb.Main_Image = child_node.Image[child_node.Image.Count - 1].Data;
                  }
                  _vb.Pdf1.ResetHost();
                  _vb.Pdf2.ResetHost();
                  _vb.Pdf1.SetHost(_vb.Host1);
              });
        }

        public async Task Data_View()
        {
            var child_db = DatabaseProvider.Child_Node;
            var file_nodes = await Task.Run(() => child_db.FindOne(x => x.Key == _vb.selectedFileinfo.Key));
            if (file_nodes == null) { return; }
            if (file_nodes != null)
            {
                if (file_nodes.Event != null)
                {
                    foreach (var list in file_nodes.Event)
                    {
                        _vb.File_History.Add(list);
                    }
                }
            }
        }

        public async Task TagRef_Container()
        {
            var child_db = DatabaseProvider.Child_Node;
            var child_node = await Task.Run(() => child_db.FindOne(x => x.Key == _vb.selectedFileinfo.Key));
            if (child_node == null) return;

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _vb.Tag.Clear();
                _vb.Ref.Clear();

                if (child_node.Feature != null)
                {
                    foreach (var feature in child_node.Feature)
                    {
                        _vb.Tag.Add(feature);
                    }
                }
                if (child_node.list != null)
                {
                    foreach (var list in child_node.list)
                    {
                        _vb.Ref.Add(list);
                    }
                }
            });
        }

        public async Task LoadPdf_Compare(EventEntry selected)
        {
            var _db = DatabaseProvider.Child_Node;

            var parentNode = await Task.Run(() => _db.FindAll()
              .FirstOrDefault(x => x.Key == selected.Key && x.Image.Any(ev => ev.Time == selected.Time)));

            if (parentNode == null || selected.Type == "Delete" || selected.Type == "Deleted")
            {
                return;
            }
            if (parentNode != null && selected.Type != "Delete" && selected.Type != "Deleted")
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    int eventIndex = parentNode.Image.FindIndex(ev => ev.Time == selected.Time);

                    if (eventIndex >= 0 && eventIndex < parentNode.Image.Count)
                    {
                        var imageData = parentNode.Image[eventIndex].Data;
                        _vb.Compare_Image = imageData;
                        _vb.Host2 = PdfBit.Pdf_Created(imageData);
                        _vb.Pdf1.SetHost(_vb.Host1);
                        _vb.Pdf2.SetHost(_vb.Host2);
                    }
                    else return;
                });
            }
        }

        public async Task LoadPdf_Current()
        {
            var differences = PdfBit.GetDifferences(_vb.Compare_Image, _vb.Main_Image);
            byte[] pdf_result = null;

            pdf_result = await Task.Run(() =>
            {
                using (var bmp = PdfiumViewer.PdfDocument.Load(new MemoryStream(_vb.Compare_Image)).Render(0, 2000, 2000, true))
                {
                    return PdfBit.AnnotatePdf(_vb.Compare_Image, differences, bmp.Width, bmp.Height);
                }
            });

            if (pdf_result == null) return;

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var window = new System.Windows.Window
                {
                    Title = "PDF Viewer",
                    Width = 800,
                    Height = 600
                };
                _vb.Host2 = PdfBit.Pdf_Created(pdf_result);

                var grid = new Grid();
                grid.Children.Add(_vb.Host2);
                window.Content = grid;
                window.Show();
            });
        }

        public async Task OpenPdfInNewWindow()
        {
            var child_db = DatabaseProvider.Child_Node;
            var child_node = await Task.Run(() => child_db.FindOne(x => x.Key == _vb.selectedFileinfo.Key));
            var window = new System.Windows.Window
            {
                Title = "PDF Viewer",
                Width = 800,
                Height = 600
            };
            if (child_node == null)
            {
                return;
            }
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _vb.New_Host1 = PdfBit.Pdf_Created(child_node.Image[child_node.Image.Count - 1].Data);
                var grid = new Grid();
                grid.Children.Add(_vb.New_Host1);
                window.Content = grid;
                window.Show();
            });
        }

        public async Task Pdf_Reset()
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _vb.Pdf1.ResetHost();
                _vb.Pdf2.ResetHost();
            });
        }


        // ====================================================

        public void FolderWatcher()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }
            _watcher = new FileSystemWatcher(_vb.folderpath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.CreationTime
                             | NotifyFilters.DirectoryName
                             | NotifyFilters.FileName
                             | NotifyFilters.LastWrite
                             | NotifyFilters.LastAccess
            };
            _watcher.InternalBufferSize = 64 * 1024;
            watcher.SetupWatcher(_watcher);
        }
        public void FolderWatcher_repository()
        {
            if (_watcher_repository != null)
            {
                _watcher_repository.EnableRaisingEvents = false;
                _watcher_repository.Dispose();
                _watcher_repository = null;
            }
            _watcher_repository = new FileSystemWatcher(_vb.repository_path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.CreationTime
                             | NotifyFilters.FileName
                             | NotifyFilters.LastWrite
            };
            _watcher_repository.InternalBufferSize = 64 * 1024;
            watcher.SetupWatcher_repository(_watcher_repository);
        }
        public bool Read_Respone(string path, string point, string eventname = null)
        {
            int retry = 100;
            while (retry-- > 0)
            {
                if (eventname == "Deleted")
                {
                    return false;
                }

                (bool, bool) check_ext = FilterExt(path);
                if (!check_ext.Item1)
                    return false;
                if (check_ext.Item2)
                    return true;
                else if (File.Exists(path))
                {
                    try
                    {
                        using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            return true;
                        }
                    }
                    catch (IOException) { }
                }

                Thread.Sleep(100);
                Debug.WriteLine($"[{point}] 파일 접근 실패, 남은 시도: {retry}");
            }

            Debug.WriteLine($"[{point}] 최종 실패: 파일 접근 불가");
            return false;
        }
        public (bool, bool) FilterExt(string path)
        {
            try
            {
                var attr = File.GetAttributes(path);
                if (attr.HasFlag(FileAttributes.Directory))
                    return (true, true);
                else if (System.IO.Path.GetExtension(path).ToUpper() == ".DWG" || System.IO.Path.GetExtension(path).ToUpper() == ".DWF")
                    return (true, false);
                else
                    return (false, false);
            }
            catch
            {
                if (System.IO.Path.GetExtension(path).ToUpper() == ".DWG" || System.IO.Path.GetExtension(path).ToUpper() == ".DWF")
                    return (true, false);
                else
                    return (false, true);
            }
        }
        public void Event_History_Add(string evt)
        {
            try
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    _vb.Event_History.Add(evt);
                });
            }
            catch (Exception)
            {
                Debug.WriteLine("Event_History_Add Error");
            }
        }
        public void Path_Setting()
        {
            Lib.Open_folderdialog folderoepn = new Lib.Open_folderdialog();
            (string, bool) path = folderoepn.PathSetting();
            _vb.folderpath = path.Item1;
            _vb.projectname = System.IO.Path.GetFileName(_vb.folderpath);
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _vb.repository_path = System.IO.Path.Combine(baseDir, $"repository");

            if (!Directory.Exists(_vb.repository_path))
            {
                Directory.CreateDirectory(_vb.repository_path);
            }
            _vb.pathset = path;
        }
        public void Pdf_Bitmap(DateTime time)
        {
            try
            {
                var child_db = DatabaseProvider.Child_Node;
                var child_nodes = child_db.FindAll().Select(x => x.File_FullName);
                if (child_nodes == null) { return; }
                foreach (string path in child_nodes)
                {
                    var node = child_db.FindOne(x => x.File_FullName == path);
                    if (!File.Exists(path)) { continue; }
                    var file = new FileInfo(path);
                    var source_node = Pdf_Bitmap_Indiviaul(time, node, node.File_FullName);

                    _db.Child_File_Table(source_node, null, DbAction.Upsert);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Pdf_Bitmap : {ex.Message}");
            }
        }

        // ====================================================

        public Child_File Pdf_Bitmap_Indiviaul(DateTime time, Child_File source_node, string source_file)
        {
            bool check = Read_Respone(source_file, "Pdf_Bitmap_Indiviaul");
            if (!check) { return null; }
            else
            {
                if (System.IO.Path.GetExtension(source_file).ToUpper() == ".DWG" || System.IO.Path.GetExtension(source_file).ToUpper() == ".DXF")
                {
                    string pathpdf = System.IO.Path.ChangeExtension(source_file, ".pdf");
                    byte[] data = PdfBit.RenderPdfPage(pathpdf);

                    source_node.Image.Add(new ImageEntry
                    {
                        Key = source_node.Key,
                        Data = data,
                        Time = time
                    });

                    if (source_node.Image.Count() == 0)
                        Debug.WriteLine("1");
                    File.Delete(pathpdf);
                    return source_node;
                }
                else { return null; }
            }
        }

        public Child_File Extrude_Indiviaul(Child_File source_node, string source_file)
        {
            bool check = Read_Respone(source_file, "Extrude_Indiviaul");
            if (!check) { return null; }
            else
            {
                (List<string>, List<string>) autocad_text = cad.WorkFlow_Zwcad(source_file);

                source_node.Feature = autocad_text.Item1;
                source_node.list = autocad_text.Item2;

                return source_node;
            }
        }

    }
}
