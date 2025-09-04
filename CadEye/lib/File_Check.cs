using CadEye.ViewCs;
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
        public Bridge _vb
        {
            get { return Bridge.Instance; }
        }
        public FunctionCollection _functionCollection
        {
            get { return FunctionCollection.Instance; }
        }

        private Data_base _db = new Data_base();
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
                var existingFiles = childdb.FindAll().Select(x => new { x.File_FullName, x.HashToken }).ToList();

                Parallel.ForEach(fileSystemEntries, options, file =>
                {
                    if (file.Extension.ToUpper() == ".DWG" || file.Extension.ToUpper() == ".DXF")
                    {
                        if ((file.Attributes & FileAttributes.Directory) == 0)
                        {
                            byte[] hash = Hash_Allocated_Unique(file.FullName);

                            bool check = existingFiles.Any(x => x.File_FullName == file.FullName && x.HashToken.SequenceEqual(hash));
                            if (check) { return; }

                            var source_node = new Child_File();

                            source_node.File_FullName = file.FullName;
                            source_node.File_Name = file.Name;
                            source_node.File_Directory = Path.GetDirectoryName(file.FullName);
                            source_node.AccesTime = file.LastAccessTime;
                            source_node.HashToken = hash;
                            source_node.Event = new List<EventEntry>();
                            source_node.Image = new List<ImageEntry>();

                            item_insert.Add(source_node);
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
            bool check = _functionCollection.Read_Respone(fullName, "Hash_Allocated_Unique_Filename");
            if (!check) { return null; }
            {
                var foldername = _vb.folderpath;
                var fileName = Path.GetFileName(fullName);

                if (fileName.StartsWith("~$"))
                    return Array.Empty<byte>();

                byte[] contentHash = null;
                int retries = 3;

                while (retries-- > 0)
                {
                    try
                    {
                        using (var stream = new FileStream(fullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
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
