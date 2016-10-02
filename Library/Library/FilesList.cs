using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace Library
{
    public class FilesList
    {
        [Serializable]
        public class FilesInformation
        {
            public List<FileElement> FilesList = new List<FileElement>();
            public List<FolderElement> FoldersList = new List<FolderElement>();

            public FilesInformation()
            {
                ;
            }
            public FilesInformation(FilesInformation list)
            {
                FilesList = new List<FileElement>(list.FilesList);
                FoldersList = new List<FolderElement>(list.FoldersList);
            }

            public FilesInformation(List<FileElement> files,List<FolderElement> folders)
            {
                FilesList = new List<FileElement>(files);
                FoldersList = new List<FolderElement>(folders);
            }
            public void Initialize(string LocalDir=@"C:\TempBox",string dirname="")
            {
                if (Directory.Exists(dirname))
                    if (dirname!=LocalDir)
                        if (FoldersList.FindIndex(x => x.Path == dirname.Replace(LocalDir, "")) == -1)
                        {
                            FolderElement folder = new FolderElement();
                            DirectoryInfo dir = new DirectoryInfo(dirname);
                            folder.Path = dir.FullName.Replace(LocalDir, "");
                            folder.Name = dir.Name;
                            folder.LastChange = dir.LastWriteTime;
                            folder.state = 2;
                            FoldersList.Add(folder);
                        }
               string[] files = Directory.GetFiles(dirname);
               foreach (string filePath in files)
               {
                   FileElement file = new FileElement();
                   FileInfo fi = new FileInfo(filePath);
                   file.Name = fi.Name;
                   file.Path = fi.FullName.Replace(LocalDir,"");
                   file.LastChange = fi.LastWriteTime;
                   file.Length = fi.Length;
                   file.Hash = ComputeMD5Checksum(filePath);
                   file.state = 2;
                   FilesList.Add(file);
               }
                /*
               if (dirname != LocalDir)
               {
                   FilesList.FolderElement folder = new FilesList.FolderElement();
                   DirectoryInfo di = new DirectoryInfo(dirname);
                   folder.Name = di.Name;
                   folder.Path = di.FullName.Replace(LocalDir,"");
                   FoldersList.Add(folder);
               }
                */
               string[] directories = Directory.GetDirectories(dirname);
               foreach (string dirPath in directories)
               {
                   this.Initialize(LocalDir,dirPath);
               }
              // Console.WriteLine("Иниализация прошла успешно.Найдено "+FilesList.Count+" файлов.");
            }
        }
        [Serializable]
        public class FolderElement
        {
            public string Name { get; set; } // Имя
            public string Path { get; set; } // Путь
            public string OldPath { get; set; } // Путь
            public bool Deleted { get; set; } // Удален
            public int state { get; set; } // состояние 0-локальный, 1-синхронизация, 2 - глобальный
            public DateTime LastChange { get; set; } // Последнее изменение
            public FolderElement()
            {
                OldPath = "";
                Deleted = false;
                state = 0;
            }
        }
        [Serializable]
        public class FileElement
        {
            public string Name { get; set; } // Имя
            public string OldName { get; set; } // Имя
            public string Path { get; set; } // Путь
            public string OldPath { get; set; } // Старый путь,чтобы можно было найти файл на сервере и поменять имя, например
            public DateTime LastChange { get; set; } // Последнее изменение
            public long Length { get; set; } // Размер файла
            public string Hash { get; set; } // MD5-хэш файла
            public bool Deleted { get; set; } // Пометка на удаление
            public int state { get; set; } // состояние 0-локальный, 1-синхронизация, 2 - глобальный
            public FileElement()
            {
                OldPath = "";
                OldName = "";
                Deleted = false;
                state = 0;
            }
        }

        //Создание несуществующих директорий по пути файла
        public static void CreateDir(string LocalDirPath,string path)
        {
            try
            {
                string[] arrPath = path.Split('\\');
                string temp = LocalDirPath + @"\";
                int ii = 0;
                foreach (string value in arrPath)
                {
                    if (ii == (arrPath.Length - 1))
                        break;
                    if (value != "")
                    {
                        //Console.Write("разбиваем путь к файлу: " + value);
                        temp += value;
                        if (!Directory.Exists(temp))
                        {
                            Directory.CreateDirectory(temp);
                        }
                        temp += @"\";
                    }
                    ii++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при создании директорий. "+ex.Message);
            }
        }
        //хеширование
        public static string ComputeMD5Checksum(string path)
        {
            return "";//пока убрал
            FileStream fs;
            while(true){
                try
                {
                    fs = System.IO.File.OpenRead(path);
                    break;
                }
                catch(Exception)
                {
                    ;
                }
            }
                MD5 md5 = new MD5CryptoServiceProvider();
                byte[] fileData = new byte[fs.Length];
                fs.Read(fileData, 0, (int)fs.Length);
                byte[] checkSum = md5.ComputeHash(fileData);
                string result = BitConverter.ToString(checkSum).Replace("-", String.Empty);
                return result;
           // fs.Close();
        }
        // Сравнение файлов. При равенстве возвращает true, при неравенстве - false
        public static int FileEquality(FileElement file1, FileElement file2)
        {
            if (file2 != null)
            {
                if ( /* file1.Name == file2.Name && */
                    file1.Path == file2.Path &&
                    (DateTime.Compare(file1.LastChange, file2.LastChange) <= 0) &&//file1.LastChange.CompareTo(file2.LastChange)<=0)/*file1.LastChange >= file2.LastChange*/ &&
                    file1.Length == file2.Length /*&&
                    file1.Hash == file2.Hash*/


                    )
                {
                    return 2;
                }
                else
                {
                    Console.WriteLine("Файлы "+file1.Path+" и "+file2.Path+" не равны");
                }
            }
            return 1;
        }

        // Составление списка недостающих файлов для отправки
        // То, что не хватает в RemoteList
        public static void CalculateDifference(FilesInformation LocalList, FilesInformation RemoteList, FilesInformation DifferencesList)
        {
            // Отсев недостающих файлов
            int flag;
            foreach (FileElement file1 in LocalList.FilesList)
            {
                flag = 1;
                FileElement file2 = RemoteList.FilesList.Find(x => (x.Path == file1.Path));
                if (file2 == null)
                {
                    ;
                }
                else
                    flag = FileEquality(file1, file2);

                if (flag==1)
                    DifferencesList.FilesList.Add(file1);

               
            }
            Console.WriteLine("Требуемый для закачки список составлен");
        }

        // В случае добавление возвращает true, в случае наличия дубликата - false
        public static bool AddElementToList(FolderElement obj, FilesInformation list)
        {
            foreach (FolderElement element in list.FoldersList)
                if (obj == element)
                    return false;
            list.FoldersList.Add(obj);
            return true;
        }

        // В случае добавление возвращает true, в случае наличия дубликата - false
        public static bool AddElementToList(FileElement obj, FilesInformation list)
        {
            foreach (FileElement element in list.FilesList)
                if (obj == element)
                    return false;
            list.FilesList.Add(obj);
            return true;
        }

        public static void PrintList(FilesInformation FI)
        {
            foreach (FileElement file in FI.FilesList)
            {
                Console.WriteLine("Файл");
                Console.WriteLine("Путь: " + file.Path);
                Console.WriteLine("Имя: " + file.Name);
                Console.WriteLine("Старый путь: " + file.OldPath);
                Console.WriteLine("Старое имя: " + file.OldName);
                Console.WriteLine("Дата последнего изменения: " + file.LastChange);
                Console.WriteLine("Размер файла: " + file.Length);
                Console.WriteLine("Хэш: " + file.Hash);
                Console.WriteLine("Удален: " + file.Deleted.ToString());
                Console.WriteLine();
            }
            foreach (FolderElement folder in FI.FoldersList)
            {
                Console.WriteLine("Папка");
                Console.WriteLine("Путь: " + folder.Path);
                Console.WriteLine("Имя: " + folder.Name);
                Console.WriteLine();
            }
        }
        public static void FillLocalList(FilesInformation LocalList)
        {
            LocalList.FilesList.Clear();
            /*
            FileElement file1 = new FileElement();
            file1.Name = "Диплом.doc";
            file1.Path = "C:\\DropBox\\Диплом.doc";
            file1.LastChange = DateTime.Parse("14.11.2015 21:08:43");
            file1.Length = 3023;
            file1.Hash = "H6SN5ZOF3WMB6S8FLA56DOWP4SCZMFFWP53FD";
            AddElementToList(file1, LocalList);

            FileElement file2 = new FileElement();
            file2.Name = "Курсовик.doc";
            file2.Path = "C:\\DropBox\\Курсовик.doc";
            file2.LastChange = DateTime.Parse("23.07.2014 13:19:04");
            file2.Length = 2079;
            file2.Hash = "HSO6BN5F7EIFON7DKFOE873JMFHSOLW7FXM6D";
            AddElementToList(file2, LocalList);

            FileElement file3 = new FileElement();
            file3.Name = "1.txt";
            file3.Path = "C:\\DropBox\\1.txt";
            file3.LastChange = DateTime.Parse("10.05.2016 11:30:25");
            file3.Length = 5;
            file3.Hash = "FK7SNC6QPFU6XN60SLEPPA6S8VMGS6EPG644ND";
            AddElementToList(file3, LocalList);

            FileElement file4 = new FileElement();
            file4.Name = "3.txt";
            file4.Path = "C:\\DropBox\\3.txt";
            file4.LastChange = DateTime.Parse("27.10.2015 07:14:57");
            file4.Length = 31;
            file4.Hash = "J7BN6S0KFE70LDTEMMS7DLQM7ZPOFLHPSQ7EMS";
            AddElementToList(file4, LocalList);

            FolderElement folder1 = new FolderElement();
            folder1.Name = "Новая папка";
            folder1.Path = "C:\\DropBox\\Новая папка";
            AddElementToList(folder1, LocalList);

            FolderElement folder2 = new FolderElement();
            folder2.Name = "Новая папка 2";
            folder2.Path = "C:\\DropBox\\Новая папка 2";
            AddElementToList(folder2, LocalList);

            FolderElement folder3 = new FolderElement();
            folder3.Name = "Курсовик";
            folder3.Path = "C:\\DropBox\\Курсовик";
            AddElementToList(folder3, LocalList);
            */
            Console.WriteLine("Локальный список заполнен");
        }
        public static void FillRemoteList(FilesInformation RemoteList)
        {
            RemoteList.FilesList.Clear();
            /*
            FileElement file1 = new FileElement();
            file1.Name = "Диплом.doc";
            file1.Path = "C:\\DropBox\\Диплом.doc";
            file1.LastChange = DateTime.Parse("14.11.2015 21:08:43");
            file1.Length = 3023;
            file1.Hash = "H6SN5ZOF3WMB6S8FLA56DOWP4SCZMFFWP53FD";
            AddElementToList(file1, RemoteList);

            FileElement file2 = new FileElement();
            file2.Name = "Курсовик.doc";
            file2.Path = "C:\\DropBox\\Курсовик.doc";
            file2.LastChange = DateTime.Parse("23.07.2014 13:19:04");
            file2.Length = 2079;
            file2.Hash = "HSO6BN5F7EIFON7DKFOE873JMFHSOLW7FXM6D";
            AddElementToList(file2, RemoteList);

            FileElement file3 = new FileElement();
            file3.Name = "1.txt";
            file3.Path = "C:\\DropBox\\1.txt";
            file3.LastChange = DateTime.Parse("10.05.2016 11:30:25");
            file3.Length = 5;
            file3.Hash = "FK7SNC6QPFU6XN60SLEPPA6S8VMGS6EPG644ND";
            AddElementToList(file3, RemoteList);

            FileElement file4 = new FileElement();
            file4.Name = "2.txt";
            file4.Path = "C:\\DropBox\\2.txt";
            file4.LastChange = DateTime.Parse("14.03.2016 21:20:04");
            file4.Length = 16;
            file4.Hash = "D8HFTE67MXFQPFI7DGQPFYEMDUFN63MG8VOL";
            AddElementToList(file4, RemoteList);

            FolderElement folder1 = new FolderElement();
            folder1.Name = "Новая папка";
            folder1.Path = "C:\\DropBox\\Новая папка";
            AddElementToList(folder1, RemoteList);

            FolderElement folder2 = new FolderElement();
            folder2.Name = "Новая папка 2";
            folder2.Path = "C:\\DropBox\\Новая папка 2";
            AddElementToList(folder2, RemoteList);

            FolderElement folder3 = new FolderElement();
            folder3.Name = "Курсовик";
            folder3.Path = "C:\\DropBox\\Курсовик";
            AddElementToList(folder3, RemoteList);

            FolderElement folder4 = new FolderElement();
            folder4.Name = "Новая папка 3";
            folder4.Path = "C:\\DropBox\\Новая папка 3";
            AddElementToList(folder4, RemoteList);
            */
            Console.WriteLine("Удаленный список заполнен");
        }
    }
}
