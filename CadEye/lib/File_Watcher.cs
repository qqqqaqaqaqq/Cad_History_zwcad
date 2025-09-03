using CadEye.ViewCS;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CadEye.Lib
{
    public class File_Watcher
    {
        public Data_base _db = new Data_base();
        public File_Check file_check = new File_Check();
        public ZwCad_Lib cad = new ZwCad_Lib();
        private ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        private System.Timers.Timer _timer;
        private List<(FileSystemEventArgs, string)> source_list = new List<(FileSystemEventArgs, string)>();
        private ConcurrentQueue<FileSystemEventArgs> eventQueue = new ConcurrentQueue<FileSystemEventArgs>();
        private ConcurrentQueue<FileSystemEventArgs> eventQueue_repository = new ConcurrentQueue<FileSystemEventArgs>();
        private bool isCollecting = false;
        private readonly object _lock = new object();
        public Bridge vm
        {
            get { return Bridge.Instance; }
        }
        // --------------------------------------------------------------------------------- 파일 감시
        public void SetupWatcher(FileSystemWatcher _watcher)
        {
            Task.Run(Brdige_Queue);
            _watcher.Changed += (s, e) => Bridge_Event(s, e);
            _watcher.Created += (s, e) => Bridge_Event(s, e);
            _watcher.Deleted += (s, e) => Bridge_Event(s, e);
            _watcher.Renamed += (s, e) => Bridge_Event(s, e);
            _watcher.EnableRaisingEvents = true;
        }
        public async void Bridge_Event(object sender, FileSystemEventArgs e)
        {
            (bool, bool) ext_chk = vm.FilterExt(e.FullPath);
            bool read_chk = vm.Read_Respone(e.FullPath, "Bridge_Event", e.ChangeType.ToString());
            if (!read_chk)
            {
                if (System.IO.Path.GetExtension(e.FullPath).ToUpper() == ".DWG" || System.IO.Path.GetExtension(e.FullPath).ToUpper() == ".DWF")
                {
                    Deleted_Exception(e);
                }
                return;
            }
            else
            {
                source_list.Add((e, e.FullPath));
                if (!isCollecting)
                {
                    if (source_list.Count() > 0)
                    {
                        isCollecting = true;
                        await Task.Delay(300);
                        List<(FileSystemEventArgs, string)> target_list =
                            new List<(FileSystemEventArgs, string)>();
                        List<FileSystemEventArgs> filter_list = new List<FileSystemEventArgs>();
                        foreach (var list in source_list)
                        {
                            target_list.Add(list);
                        }
                        filter_list = Detected(target_list);
                        foreach (var list in filter_list.ToList())
                        {
                            lock (_lock)
                            {
                                eventQueue.Enqueue(list);
                            }
                        }
                        isCollecting = false;
                    }
                }
            }
        }
        public List<FileSystemEventArgs> Detected(List<(FileSystemEventArgs, string)> target_list)
        {
            Dictionary<string, FileSystemEventArgs> uniqueEvents = new Dictionary<string, FileSystemEventArgs>();

            foreach (var item in target_list)
            {
                string path = item.Item2;
                FileSystemEventArgs args = item.Item1;
                Ctrl_Z_Exception(item.Item1);
                uniqueEvents[path] = args;
            }

            foreach (var path in uniqueEvents.Keys)
            {
                source_list.RemoveAll(x => x.Item2 == path);
            }

            return uniqueEvents.Values.ToList();
        }

        public void Ctrl_Z_Exception(FileSystemEventArgs e)
        {
            (bool, bool) ext_chk = vm.FilterExt(e.FullPath);
            if (!ext_chk.Item2)
            {
                if (e.ChangeType == WatcherChangeTypes.Renamed)
                {
                    var re = e as RenamedEventArgs;
                    var file = new FileInfo(e.FullPath);
                    var target_node = DatabaseProvider.Child_Node.FindOne(x => x.File_FullName == re.OldFullPath);
                    if (target_node == null) return;

                    var source_node = new Child_File();
                    var filename = System.IO.Path.GetFileName(e.FullPath);
                    byte[] has = file_check.Hash_Allocated_Unique(e.FullPath);

                    DateTime access_time = new DateTime(
                       file.LastAccessTime.Year,
                       file.LastAccessTime.Month,
                       file.LastAccessTime.Day,
                       file.LastAccessTime.Hour,
                       file.LastAccessTime.Minute,
                       file.LastAccessTime.Second
                    );

                    source_node.Key = target_node.Key;
                    source_node.File_FullName = target_node.File_FullName;
                    source_node.File_Name = target_node.File_Name;
                    source_node.File_Directory = target_node.File_Directory;
                    source_node.HashToken = has;
                    source_node.AccesTime = access_time;
                    source_node.list = target_node.list;
                    source_node.Feature = target_node.Feature;
                    source_node.Event = target_node.Event;
                    source_node.Image = target_node.Image;

                    _db.Child_File_Table(source_node, null, DbAction.Update);
                }
            }
        }
        public void Deleted_Exception(FileSystemEventArgs e)
        {
            // Deleted
            DateTime time = new DateTime(
                DateTime.Now.Year,
                DateTime.Now.Month,
                DateTime.Now.Day,
                DateTime.Now.Hour,
                DateTime.Now.Minute,
                DateTime.Now.Second
            );
            var child_node = DatabaseProvider.Child_Node;
            var target_node = child_node.FindOne(x => x.File_FullName == e.FullPath);
            if (target_node == null) { return; }
            var foldername = System.IO.Path.GetDirectoryName(target_node.File_FullName);
            var relative = System.IO.Path.GetFileName(foldername);
            var relativefilename = System.IO.Path.Combine(relative, System.IO.Path.GetFileName(target_node.File_FullName));
            var source_node = new Child_File();

            source_node.Key = target_node.Key;
            source_node.File_FullName = e.FullPath;
            source_node.File_Name = target_node.File_Name;
            source_node.File_Directory = target_node.File_Directory;
            source_node.AccesTime = target_node.AccesTime;
            source_node.HashToken = target_node.HashToken;
            source_node.list = target_node.list;
            source_node.Feature = target_node.Feature;
            source_node.Event = target_node.Event;
            source_node.Image = target_node.Image;
            source_node.Detele_Check = 1;
            string Event = "Delete";

            source_node.Event.Add(new EventEntry()
            {
                Key = source_node.Key,
                Time = time,
                Type = Event,
                Description = $"Delete : {relativefilename}"
            });

            _db.Child_File_Table(source_node, null, DbAction.Update);
            string result = $"Delete Succed, {e.FullPath}";
            vm.Event_History_Add(result);
        }

        public async Task Brdige_Queue()
        {
            while (true)
            {
                if (eventQueue.TryDequeue(out var e))
                {
                    if (e.ChangeType == WatcherChangeTypes.Deleted) { }
                    string result = $"WorkFlow Progressing., {e.FullPath}";
                    vm.Event_History_Add(result);

                    DateTime time = new DateTime(
                        DateTime.Now.Year,
                        DateTime.Now.Month,
                        DateTime.Now.Day,
                        DateTime.Now.Hour,
                        DateTime.Now.Minute,
                        DateTime.Now.Second
                    );

                    (bool, bool) folder_chk = vm.FilterExt(e.FullPath);
                    if (folder_chk.Item2)
                    {
                        bool complete_chk = false;

                        switch (e.ChangeType)
                        {
                            case WatcherChangeTypes.Renamed:
                                complete_chk = Folder_B(e, time);
                                break;
                        }

                        if (!complete_chk)
                        {
                            result = $"Folder_WorkFlow Failed. {e.FullPath}";
                            vm.Event_History_Add(result);
                        }
                    }
                    else
                    {
                        bool complete_chk = false;
                        switch (e.ChangeType)
                        {
                            case WatcherChangeTypes.Created:
                                complete_chk = File_A(e, time);
                                break;
                            case WatcherChangeTypes.Changed:
                                complete_chk = File_A(e, time);
                                break;
                            case WatcherChangeTypes.Renamed:
                                complete_chk = File_B(e, time);
                                break;
                        }

                        if (!complete_chk)
                        {
                            result = $"File_WorkFlow Failed. {e.FullPath}";
                            vm.Event_History_Add(result);
                        }
                    }
                    await Task.Delay(100);
                    // return; 리턴쓰면 q 전체를 빠져나가서 오류걸림
                }
                else
                {
                    await Task.Delay(100);
                }
            }
        }
        private bool Folder_B(FileSystemEventArgs e, DateTime time)
        {
            try
            {
                var re = e as RenamedEventArgs;
                var target_nodes = DatabaseProvider.Child_Node.FindAll().Where(x => x.File_Directory == re.OldFullPath);
                var source_node = new Child_File();

                if (target_nodes.Count() == 0)
                {
                    string result = $"Folder Rename Result Succed : {e.FullPath}";
                    vm.Event_History_Add(result);
                    return true;
                }
                foreach (var target_node in target_nodes)
                {
                    string fullname = target_node.File_FullName.Replace(re.OldFullPath, e.FullPath);
                    var filename = System.IO.Path.GetFileName(fullname);
                    byte[] has = file_check.Hash_Allocated_Unique(fullname);

                    source_node.Key = target_node.Key;
                    source_node.File_FullName = fullname;
                    source_node.File_Name = filename;
                    source_node.File_Directory = e.FullPath;
                    source_node.HashToken = has;
                    source_node.list = target_node.list;
                    source_node.Feature = target_node.Feature;
                    source_node.Event = target_node.Event;
                    source_node.Image = target_node.Image;

                    string Event = "Folder Rename";
                    source_node.Event.Add(new EventEntry
                    {
                        Key = source_node.Key,
                        Time = time,
                        Type = Event,
                        Description = $"Pre Folder Name : {re.OldFullPath}"
                    });

                    string result = $"Folder Rename Result Succed : {e.FullPath}";
                    vm.Event_History_Add(result);

                    bool check = _db.Child_File_Table(source_node, null, DbAction.Update);
                    if (!check) return false;
                    File_Copy(source_node.File_FullName, time, source_node.Key, Event);
                    vm.File_input_Event();
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        private bool File_A(FileSystemEventArgs e, DateTime time)
        {
            try
            {
                bool read_chk = vm.Read_Respone(e.FullPath, "File_A", e.ChangeType.ToString());
                if (!read_chk)
                {
                    return false;
                }
                else
                {
                    var child_node = DatabaseProvider.Child_Node;
                    var source_node = new Child_File();
                    long key = 0;

                    string fileName = System.IO.Path.GetFileName(e.FullPath);
                    string Event = "";
                    string result = "";
                    string relative = "";
                    string relativefilename = "";
                    byte[] has;

                    has = file_check.Hash_Allocated_Unique(e.FullPath);

                    var file = new FileInfo(e.FullPath);
                    DateTime access_time = new DateTime(
                       file.LastAccessTime.Year,
                       file.LastAccessTime.Month,
                       file.LastAccessTime.Day,
                       file.LastAccessTime.Hour,
                       file.LastAccessTime.Minute,
                       file.LastAccessTime.Second
                    );


                    var target_node = child_node.FindOne(x => x.File_FullName == e.FullPath);
                    var target_node_hash = child_node.FindAll().FirstOrDefault(x => x.HashToken.SequenceEqual(has));
                    var target_node_access = child_node.FindAll().FirstOrDefault(x => x.HashToken.SequenceEqual(has) && Math.Abs((x.AccesTime - access_time).TotalSeconds) < 1);
                    var allkeys = child_node.FindAll().Select(x => x.Key);
                    string fullName = e.FullPath;
                    // Created
                    if (target_node == null && target_node_hash == null)
                    {

                        key = allkeys.Any() ? allkeys.Max() + 1 : 1;

                        source_node = SettingSourceNode(fullName, key, has);

                        source_node.Event = new List<EventEntry>();
                        source_node.Image = new List<ImageEntry>();

                        Event = "Creat";

                        source_node.Event.Add(new EventEntry()
                        {
                            Key = source_node.Key,
                            Time = time,
                            Type = Event,
                        });

                        result = $"File_A Create Succed, {e.FullPath}";
                        vm.Event_History_Add(result);

                        _db.Child_File_Table(source_node, null, DbAction.Insert);
                        File_Copy(source_node.File_FullName, time, source_node.Key, Event);
                        vm.File_input_Event();
                        return true;
                    }
                    // Moved
                    else if ((target_node == null && target_node_hash != null) && target_node_access != null)
                    {
                        key = target_node_access.Key;
                        var foldername = System.IO.Path.GetDirectoryName(e.FullPath);
                        relative = System.IO.Path.GetFileName(foldername);
                        relativefilename = System.IO.Path.Combine(relative, System.IO.Path.GetFileName(e.FullPath));

                        source_node = SettingSourceNode(fullName, key, has, target_node_access);

                        Event = "Move";

                        source_node.Event.Add(new EventEntry()
                        {
                            Key = source_node.Key,
                            Time = time,
                            Type = Event,
                            Description = $"Origin Address : {relativefilename}",
                        });

                        result = $"File_A Move Succed, {e.FullPath}";
                        vm.Event_History_Add(result);

                        _db.Child_File_Table(source_node, null, DbAction.Update);
                        File_Copy(source_node.File_FullName, time, source_node.Key, Event);
                        vm.File_input_Event();
                        return true;
                    }
                    // Copyed
                    else if ((target_node == null && target_node_hash != null) && target_node_access == null)
                    {
                        var foldername = System.IO.Path.GetDirectoryName(target_node_hash.File_FullName);
                        relative = System.IO.Path.GetFileName(foldername);
                        relativefilename = System.IO.Path.Combine(relative, System.IO.Path.GetFileName(target_node_hash.File_FullName));

                        key = allkeys.Any() ? allkeys.Max() + 1 : 1;

                        source_node = SettingSourceNode(fullName, key, has, target_node_hash);

                        {
                            source_node.list = new List<string>();
                            source_node.Feature = new List<string>();
                            source_node.Event = new List<EventEntry>();
                            source_node.Image = new List<ImageEntry>();
                        }

                        Event = "Copy";

                        source_node.Event.Add(new EventEntry()
                        {
                            Key = source_node.Key,
                            Time = time,
                            Type = Event,
                            Description = $"Origin File : {relativefilename}"
                        });

                        {
                            result = $"File_A Copy Succed, {e.FullPath}";
                            vm.Event_History_Add(result);

                            _db.Child_File_Table(source_node, null, DbAction.Insert);
                            File_Copy(source_node.File_FullName, time, source_node.Key, Event);
                            vm.File_input_Event();
                            return true;
                        }
                    }
                    // Restore
                    else if (target_node != null && target_node.Detele_Check == 1)
                    {
                        key = target_node.Key;

                        source_node = SettingSourceNode(fullName, key, has, target_node);

                        Event = "Restore";

                        source_node.Event.Add(new EventEntry()
                        {
                            Key = source_node.Key,
                            Time = time,
                            Type = Event,
                            Description = "Restore Completed"
                        });

                        {
                            result = $"File_A Restore Succed, {e.FullPath}";
                            vm.Event_History_Add(result);

                            _db.Child_File_Table(source_node, null, DbAction.Update);
                            File_Copy(source_node.File_FullName, time, source_node.Key, Event);
                            vm.File_input_Event();
                            return true;
                        }
                    }
                    // No-Changed
                    else if ((target_node != null && target_node.Detele_Check != 1) && target_node_hash != null)
                    {
                        key = target_node.Key;

                        source_node = SettingSourceNode(fullName, key, has, target_node);

                        Event = "No-Change";

                        source_node.Event.Add(new EventEntry()
                        {
                            Key = source_node.Key,
                            Time = time,
                            Type = Event,
                            Description = "HashToken matches"
                        });

                        result = $"File_A No-Change Succed, {e.FullPath}";
                        vm.Event_History_Add(result);

                        {
                            _db.Child_File_Table(source_node, null, DbAction.Update);
                            File_Copy(source_node.File_FullName, time, source_node.Key, Event);
                            vm.File_input_Event();
                            return true;
                        }
                    }
                    // Changed
                    else if ((target_node != null && target_node.Detele_Check != 1) && target_node_hash == null)
                    {
                        key = target_node.Key;

                        source_node = SettingSourceNode(fullName, key, has, target_node);
                        Event = "Change";

                        source_node.Event.Add(new EventEntry()
                        {
                            Key = source_node.Key,
                            Time = time,
                            Type = Event,
                        });

                        result = $"File_A Change Succed, {e.FullPath}";
                        vm.Event_History_Add(result);

                        {
                            _db.Child_File_Table(source_node, null, DbAction.Update);
                            File_Copy(source_node.File_FullName, time, source_node.Key, Event);
                            vm.File_input_Event();
                            return true;
                        }
                    }

                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
        public Child_File SettingSourceNode(string fullName, long key, byte[] has, Child_File target_node = null)
        {
            var file = new FileInfo(fullName);
            DateTime access_time = new DateTime(
               file.LastAccessTime.Year,
               file.LastAccessTime.Month,
               file.LastAccessTime.Day,
               file.LastAccessTime.Hour,
               file.LastAccessTime.Minute,
               file.LastAccessTime.Second
            );

            var source_node = new Child_File();
            source_node.Key = key;
            source_node.File_FullName = fullName;
            source_node.File_Name = System.IO.Path.GetFileName(fullName);
            source_node.File_Directory = System.IO.Path.GetDirectoryName(fullName);
            source_node.AccesTime = access_time;
            source_node.HashToken = has;
            if (target_node != null)
            {
                source_node.list = target_node.list;
                source_node.Feature = target_node.Feature;
                source_node.Event = target_node.Event;
                source_node.Image = target_node.Image;
            }
            else
            {
                source_node.list = new List<string>();
                source_node.Feature = new List<string>();
                source_node.Event = new List<EventEntry>();
                source_node.Image = new List<ImageEntry>();
            }
            source_node.Detele_Check = 0;
            return source_node;
        }
        private bool File_B(FileSystemEventArgs e, DateTime time)
        {
            try
            {
                bool read_chk = vm.Read_Respone(e.FullPath, "File_B");
                if (!read_chk)
                {
                    string result = $"Rename Result Filed : {e.FullPath}";
                    vm.Event_History_Add(result);

                    return false;
                }
                else
                {
                    var re = e as RenamedEventArgs;
                    var file = new FileInfo(e.FullPath);
                    var target_node = DatabaseProvider.Child_Node.FindOne(x => x.File_FullName == re.OldFullPath);
                    if (target_node == null) return false;

                    var source_node = new Child_File();
                    var filename = System.IO.Path.GetFileName(e.FullPath);
                    byte[] has = file_check.Hash_Allocated_Unique(e.FullPath);

                    source_node.Key = target_node.Key;
                    source_node.File_FullName = e.FullPath;
                    source_node.File_Name = filename;
                    source_node.File_Directory = System.IO.Path.GetDirectoryName(e.FullPath);
                    source_node.HashToken = has;
                    source_node.AccesTime = target_node.AccesTime;
                    source_node.list = target_node.list;
                    source_node.Feature = target_node.Feature;
                    source_node.Event = target_node.Event;
                    source_node.Image = target_node.Image;

                    string Event = "Rename";
                    source_node.Event.Add(new EventEntry
                    {
                        Key = source_node.Key,
                        Time = time,
                        Type = Event,
                        Description = $"Pre Name : {re.OldFullPath}"
                    });

                    string result = $"Rename Result Succed : {e.FullPath}";
                    vm.Event_History_Add(result);

                    _db.Child_File_Table(source_node, null, DbAction.Update);
                    File_Copy(source_node.File_FullName, time, source_node.Key, Event);
                    vm.File_input_Event();
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
        private void SafeFileCopy(string sourcePath, string destPath, int retries = 5, int delayMs = 500)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    File.Copy(sourcePath, destPath, true);
                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(delayMs);
                }
            }
            throw new IOException($"파일 복사 실패: {sourcePath}");
        }
        public void File_Copy(string fullpath, DateTime time, long key, string Event)
        {
            try
            {
                string result = "";
                bool read_chk = vm.Read_Respone(fullpath, "File_Copy");
                if (!read_chk)
                {
                    result = $"File Copy Failed  : {fullpath}";
                    vm.Event_History_Add(result);

                    return;
                }
                else
                {
                    var source_file = fullpath;
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string target_folder = System.IO.Path.Combine(baseDir, $"repository\\{Bridge.projectname}");
                    if (!Directory.Exists(target_folder))
                    {
                        Directory.CreateDirectory(target_folder);
                    }
                    string target_file = System.IO.Path.Combine(target_folder, $"{time:yyyy-MM-dd-HH-mm-ss}_{Event}_{key}.dwg");

                    const int maxRetries = 5;
                    const int delayMs = 2000;

                    for (int i = 0; i < maxRetries; i++)
                    {
                        try
                        {
                            using (FileStream stream = new FileStream(source_file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                SafeFileCopy(source_file, target_file);
                            }
                            return;
                        }
                        catch (IOException)
                        {
                            if (i == maxRetries - 1) throw;
                            Thread.Sleep(delayMs);
                        }
                    }
                }

                result = $"File Copy Succed  : {fullpath}";
                vm.Event_History_Add(result);

            }
            catch (Exception)
            {
                string result = $"File Copy Failed : {fullpath}";
                vm.Event_History_Add(result);

            }
        }
        // --------------------------------------------------------------------------------- 저장소 파일 감시
        public void SetupWatcher_repository(FileSystemWatcher _watcher)
        {
            Task.Run(Brdige_Queue_repository);
            _watcher.Created += (s, e) => Bridge_Event_repository(s, e);
            _watcher.EnableRaisingEvents = true;
        }
        public void Bridge_Event_repository(object sender, FileSystemEventArgs e)
        {
            eventQueue_repository.Enqueue(e);
        }
        public async Task Brdige_Queue_repository()
        {
            while (true)
            {
                if (eventQueue_repository.TryDequeue(out var e))
                {
                    switch (e.ChangeType)
                    {
                        case WatcherChangeTypes.Created:
                            Repository(e);
                            break;
                    }
                    await Task.Delay(100);
                }
                else
                {
                    await Task.Delay(100);
                }
            }
        }
        private async void Repository(FileSystemEventArgs e)
        {
            try
            {
                string result = "";
                bool read_chk = vm.Read_Respone(e.FullPath, "Repository");
                if (!read_chk)
                {
                    return;
                }
                else
                {
                    (DateTime, string, long) name_parts = file_name_parsing(e);
                    var source_node = new Child_File();


                    source_node.list = new List<string>();
                    source_node.Feature = new List<string>();
                    source_node.Event = new List<EventEntry>();
                    source_node.Image = new List<ImageEntry>();

                    int retry = 5;
                    while (retry > 0)
                    {
                        source_node = Extrude_PDF(e, source_node, name_parts.Item1);

                        if (source_node != null)
                            break;

                        await Task.Delay(200);
                        retry--;
                    }

                    var child_node = DatabaseProvider.Child_Node;
                    var target_node = child_node.FindOne(x => x.Key == name_parts.Item3);

                    if (File.Exists(target_node.File_FullName))
                    {
                        var temp_source_node = new ImageEntry();
                        temp_source_node = source_node.Image[0];
                        source_node.Key = target_node.Key;
                        source_node.File_FullName = target_node.File_FullName;
                        source_node.File_Name = target_node.File_Name;
                        source_node.AccesTime = target_node.AccesTime;
                        source_node.File_Directory = target_node.File_Directory;
                        source_node.HashToken = target_node.HashToken;
                        source_node.Event = target_node.Event;
                        source_node.Image = target_node.Image;
                        //===========================================
                        source_node.Feature = source_node.Feature;
                        source_node.list = source_node.list;
                        source_node.Image.Add(temp_source_node);
                    }

                    _db.Child_File_Table(source_node, null, DbAction.Update);
                }
                result = $"Repository Succed : {e.FullPath}";
                vm.Event_History_Add(result);
            }
            catch (Exception)
            {
                string result = $"Repository Failed : {e.FullPath}";
                vm.Event_History_Add(result);
            }
        }
        private (DateTime, string, long) file_name_parsing(FileSystemEventArgs e)
        {
            string fileName = System.IO.Path.GetFileName(e.FullPath);
            string[] parts = fileName.Split('_');
            if (parts.Length < 3)
            {
                Debug.WriteLine($"Repository_ProcessQueue : 파일 이름 형식 오류 - {fileName}");
                return (DateTime.Now, "", 0);
            }
            if (!DateTime.TryParseExact(parts[0], "yyyy-MM-dd-HH-mm-ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time))
            {
                Debug.WriteLine($"Repository_ProcessQueue : 날짜 파싱 실패 - {parts[0]}");
                return (DateTime.Now, "", 0);
            }
            string type = parts[1];
            if (!long.TryParse(parts[2].Split('.')[0], out long Number))
            {
                Debug.WriteLine($"Repository_ProcessQueue : 번호 파싱 실패 - {parts[2]}");
                return (DateTime.Now, "", 0);
            }

            return (time, type, Number);
        }
        private Child_File Extrude_PDF(FileSystemEventArgs e, Child_File source_node, DateTime time)
        {
            bool read_chk = vm.Read_Respone(e.FullPath, "Extrude_PDF");

            try
            {
                source_node = vm.Extrude_Indiviaul(source_node, e.FullPath);
                source_node = vm.Pdf_Bitmap_Indiviaul(time, source_node, e.FullPath);
                return source_node;
            }
            catch
            {
                Thread.Sleep(1000);
                return null;
            }
        }
        // ---------------------------------------------------------------------------------
    }
}


