using CadEye.ViewCS;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace CadEye.Lib
{
    public class File_Check
    {
        private Data_base _db = new Data_base();
        public Bridge vm
        {
            get { return Bridge.Instance; }
        }
        public void Setting()
        {
            _db.Child_File_Table(null, null, DbAction.DeleteAll);
        }
        public ConcurrentBag<Child_File> AllocateData(string path)
        {
            if (path == null) { return null; }
            try
            {
                var childdb = DatabaseProvider.Child_Node;
                var dirInfo = new DirectoryInfo(path);
                var fileSystemEntries = dirInfo.EnumerateFileSystemInfos("*", SearchOption.AllDirectories)
                    .Where(f => (f.Attributes & FileAttributes.ReparsePoint) == 0)
                    .ToList();
                var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount };
                var item_insert = new ConcurrentBag<Child_File>();
                var existingFiles = childdb.FindAll().Select(x => new { x.File_Path, x.HashToken }).ToList();
                Parallel.ForEach(fileSystemEntries, options, file =>
                {
                    if (file.Extension.ToUpper() == ".DWG" || file.Extension.ToUpper() == ".DXF")
                    {
                        if ((file.Attributes & FileAttributes.Directory) == 0)
                        {
                            byte[] hash = Hash_Allocated_Unique(file.FullName);

                            bool check = existingFiles.Any(x => x.File_Path == file.FullName && x.HashToken.SequenceEqual(hash));
                            if (check) { return; }

                            var node = new Child_File();

                            node.File_Path = file.FullName;
                            node.File_Name = file.Name;
                            node.HashToken = hash;
                            node.Event = new List<EventEntry>();
                            node.Image = new List<ImageEntry>();

                            item_insert.Add(node);
                        }
                    }
                });
                return item_insert;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
            }
        }
        public byte[] Hash_Allocated_Unique(string fullName)
        {
            bool check = vm.Read_Respone(fullName, "Hash_Allocated_Unique");
            if (!check) { return null; }
            {
                var fileName = Path.GetFileName(fullName);

                if (fileName.StartsWith("~$"))
                    return Array.Empty<byte>();

                byte[] contentHash = null;
                int retries = 3;

                while (retries-- > 0)
                {
                    try
                    {
                        using (var stream = new FileStream(fullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var sha = SHA256.Create())
                        {
                            contentHash = sha.ComputeHash(stream);
                        }
                        break;
                    }
                    catch (IOException)
                    {
                        if (retries == 0)
                        {
                            Debug.WriteLine($"파일 잠김: {fullName}");
                            return Array.Empty<byte>();
                        }
                        Task.Delay(100).Wait();
                    }
                }

                string combined = fileName + Convert.ToBase64String(contentHash);
                using (var sha = SHA256.Create())
                {
                    return sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined));
                }
            }
        }
    }
}
