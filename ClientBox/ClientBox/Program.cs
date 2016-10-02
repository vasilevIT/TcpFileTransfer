using System;
using System.Collections.Generic;
using System.Text;
using Library;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Xml.Serialization;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace ClientBox
{
    class Program
    {
        private static int localPort = 666;
       // private static UdpClient client;
        private static IPEndPoint endPoint;
        private static FileStream fs;
        private static int bufferSize = 2048;
        //Клиент
        private static TcpClient client = new TcpClient();
        //Получение всяких обновлений с сервера
        private static TcpClient client2 = new TcpClient();
        private static string LocalDirName = @"";
        // Отправка списка файлов
        private static IPAddress remoteIPAddress = IPAddress.Parse("127.0.0.1");
        private static int remotePort = 6000;

        // Получение списка файлов
        private static IPEndPoint endPoint2;
        private static int receivingListPort = 7000;
        private static Byte[] receiveBytes = new Byte[0];
        private static IPEndPoint RemoteIpEndPoint = null;

        public static FilesList.FilesInformation LocalList = new FilesList.FilesInformation(); // Список файлов на текущем хосте
        public static FilesList.FilesInformation RemoteList = new FilesList.FilesInformation(); // Список файлов, закаченный с сервера
        public static FilesList.FilesInformation DifferencesList = new FilesList.FilesInformation(); // Список файлов, требуемый для закачки

        public static FileSystemWatcher watcher;
        private static bool blockThread=false;

        //Таймер, по которому будет срабатывать синхронизация(например через 5 секунд после добавления файлов в папку)
        private static System.Timers.Timer myTimer = new System.Timers.Timer();

        //Входная функция программы
        static void Main(string[] args)
        {
            Console.WriteLine("Введите название папки, которую вы бы хотели синхронизировать");
            LocalDirName = @"C:\" + Console.ReadLine();
            Console.WriteLine(DateTime.Now + " Инициализация каталога. Пожалуйста, ждите...");
            Console.WriteLine(DateTime.Now + " Локальная папка: " + LocalDirName);
            if (!Directory.Exists(LocalDirName))
                Directory.CreateDirectory(LocalDirName);
            LocalList.Initialize(LocalDirName, LocalDirName);
            Console.WriteLine(DateTime.Now + " MgupiBox запущен.");
           // FilesList.PrintList(LocalList);
            string ipStr = "";
            Console.Write("IP-адрес сервера:");
            ipStr = Console.ReadLine();
            if (ipStr == "")
            {
                // Получение имени компьютера.
                String host = System.Net.Dns.GetHostName();
                // Получение ip-адреса.
                System.Net.IPAddress ip = System.Net.Dns.GetHostByName(host).AddressList[0];
                ipStr = ip.ToString();// "127.0.0.1";
                Console.WriteLine("Будет использован локальный адрес: "+ipStr);
            }
            endPoint = new IPEndPoint(remoteIPAddress, remotePort);
            try
            {
                client.Connect(ipStr, remotePort);
                client2.Connect(ipStr, remotePort+1);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Не удалось подключиться. " + ex.Message);
                Console.ReadLine();
                return;
            }
            Thread thread1 = new Thread(GetUpdate);
            thread1.Start();
            Thread thread2 = new Thread(Sending);
            thread2.Start();

            //Получаем список файлов с сервера, чтобы закачать отсутствующие файлы
            GetFileList(client);
            //Отправляем список на сервер, чтобы добавить на него отсутствующие файлы
            SendFileList(client,LocalList);
            // Получение списка файлов
           // endPoint2 = new IPEndPoint(remoteIPAddress, receivingListPort);
            Run();
        }
        //Запуск FileWatcher'a
        public static void Run()
        {
            watcher = new FileSystemWatcher();
            watcher.EnableRaisingEvents = false;
            //watcher.Path = args[1];
          
           watcher.Path = LocalDirName;
            /* Watch for changes in LastAccess and LastWrite times, and
               the renaming of files or directories. */
            watcher.NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite
               | NotifyFilters.FileName | NotifyFilters.DirectoryName;
            // Only watch text files.
            //watcher.Filter = "*.txt";
            watcher.Filter = "";


            watcher.IncludeSubdirectories = true;

            // Add event handlers.
            watcher.Renamed += new RenamedEventHandler(OnRenamed);
            //watcher.Changed += new FileSystemEventHandler(OnChanged);
            watcher.Created += new FileSystemEventHandler(OnCreated);
            watcher.Error += new ErrorEventHandler(OnError);
            watcher.Deleted += new FileSystemEventHandler(OnDeleted);


            // Begin watching.
            watcher.IncludeSubdirectories = true;
            watcher.EnableRaisingEvents = true;


            string cmd;
            do
            {
                Console.WriteLine("Press \'q\' to quit");
               cmd = Console.ReadLine();
            }
            while (cmd != "q");

            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        //получение удаленного списка
        public static void GetFileList(TcpClient clientUpdate)
        {
            NetworkStream tcpStream = clientUpdate.GetStream();
            XmlSerializer fileSerializer = new XmlSerializer(typeof(FilesList.FilesInformation));
            MemoryStream stream = FilesSender.GetRemoteList(clientUpdate);
         //   byte[] buffer = new byte[stream.Length];
           // stream.Read(buffer, 0, (int)stream.Length);
         //   string temp = Encoding.Default.GetString(buffer);
         //   Console.WriteLine("СПИСОК С ОБНОВЛЕНИЯМИ");
          //  Console.Write(temp);
            stream.Position = 0;
            RemoteList = (FilesList.FilesInformation)fileSerializer.Deserialize(stream);
            //изменение и удаление некоторых элементов локального списка
            List<FilesList.FileElement> tempList = RemoteList.FilesList.FindAll(x => x.OldPath != "" || x.Deleted);
            int i = 0;
            while (i < tempList.Count)//foreach(FilesList.FileElement file in )
            {
                FilesList.FileElement file = tempList[i];
                if (file.Deleted)
                {
                    int index = LocalList.FilesList.FindIndex(x => x.Path == file.Path);
                    int index2 = RemoteList.FilesList.FindIndex(x => x.Path == file.Path);
                    if (index != -1)
                    {
                        if (File.Exists(LocalDirName + file.Path))
                        {
                            File.Delete(LocalDirName + file.Path);
                            Console.WriteLine(DateTime.Now + " Удалили файл " + file.Path);
                        }
                        else
                        {

                            Console.WriteLine(DateTime.Now + " Не удалось удалить файл " + file.Path+" . Он уже удален.");
                        }
                        LocalList.FilesList.RemoveAt(index);
                      //  tempList.RemoveAt(i);
                       // continue;

                    }

                    if (index2 != -1)
                    {
                        RemoteList.FilesList.RemoveAt(index2);
                    }
                    tempList.RemoveAt(i);
                    continue;
                }
                if (file.OldPath != "")
                {

                    int index = LocalList.FilesList.FindIndex(x => x.Path == file.OldPath);
                    int indexRemote = RemoteList.FilesList.FindIndex(x => x.Path == file.OldPath);
                    if (index != -1)
                    {

                        if (File.Exists(LocalDirName + file.Path))
                        {
                            File.Move(LocalDirName + file.OldPath, LocalDirName + file.Path);
                            FilesList.FileElement newFile = new FilesList.FileElement();
                            newFile.Name = file.Name;
                            newFile.Path = file.Path;
                            newFile.LastChange = file.LastChange;
                            newFile.Length = file.Length;
                            LocalList.FilesList[index] = newFile;
                            //RemoteList.FilesList[index2] = newFile;
                            //File.Rename(LocalDir + file.Path);
                            if (indexRemote != -1)
                            {
                                RemoteList.FilesList[indexRemote]=newFile;
                            }
                            Console.WriteLine(DateTime.Now + " Переименовали файл " + file.OldPath + " на " + file.Path);
                            continue;
                        }
                        else
                        {

                            Console.WriteLine(DateTime.Now+ " Не удалось переименовать файл " + file.OldPath + " на " + file.Path+" . Файл удален.");
                        }
                    }
                    else
                    {

                        Console.WriteLine(DateTime.Now + " Не удалось переименовать файл " + file.OldPath + " на " + file.Path + " . Файл уже переименован.");
                    }
                }
                i++;
            }
            List<FilesList.FolderElement> tempdDirList = RemoteList.FoldersList.FindAll(x => x.OldPath != "" || x.Deleted);
            i = 0;
            while (i < tempdDirList.Count)//foreach(FilesList.FileElement file in )
            {

                FilesList.FolderElement folder = tempdDirList[i];

                if (!Directory.Exists(folder.Path))
                {
                    Directory.CreateDirectory(LocalDirName + folder.Path);
                }
                if (folder.Deleted)
                {
                    int index = LocalList.FilesList.FindIndex(x => x.Path == folder.Path);
                    int index2 = RemoteList.FilesList.FindIndex(x => x.Path == folder.Path);
                    if (index != -1)
                    {
                        if (Directory.Exists(LocalDirName + folder.Path))
                        {
                            Directory.Delete(LocalDirName + folder.Path);
                            Console.WriteLine(DateTime.Now + " Удалили директорию " + folder.Path);
                        }
                        else
                        {

                            Console.WriteLine(DateTime.Now + " Не удалось удалить директорию " + folder.Path + " . Она уже удален.");
                        }
                        LocalList.FoldersList.RemoveAt(index);
                        //  tempList.RemoveAt(i);
                        // continue;

                    }

                    if (index2 != -1)
                    {
                        RemoteList.FoldersList.RemoveAt(index2);
                    }
                    tempdDirList.RemoveAt(i);
                    continue;
                }
                if (folder.OldPath != "")
                {

                    int index = LocalList.FoldersList.FindIndex(x => x.Path == folder.OldPath);
                    int indexRemote = RemoteList.FoldersList.FindIndex(x => x.Path == folder.OldPath);
                    if (index != -1)
                    {

                        if (File.Exists(LocalDirName + folder.Path))
                        {
                            Directory.Move(LocalDirName + folder.OldPath, LocalDirName + folder.Path);
                            FilesList.FolderElement newFile = new FilesList.FolderElement();
                            newFile.Name = folder.Name;
                            newFile.Path = folder.Path;
                            newFile.LastChange = folder.LastChange;
                            LocalList.FoldersList[index] = newFile;
                            //RemoteList.FilesList[index2] = newFile;
                            //File.Rename(LocalDir + file.Path);
                            if (indexRemote != -1)
                            {
                                RemoteList.FoldersList[indexRemote] = newFile;
                            }
                            Console.WriteLine(DateTime.Now + " Переименовали директорию " + folder.OldPath + " на " + folder.Path);
                          //  continue;
                        }
                        else
                        {

                            Console.WriteLine(DateTime.Now + " Не удалось переименовать директорию " + folder.OldPath + " на " + folder.Path + " . Директория удален.");
                        }
                    }
                    else
                    {

                        Console.WriteLine(DateTime.Now + " Не удалось переименовать дир " + folder.OldPath + " на " + folder.Path + " . Дир уже переименован.");
                    }
                }
                i++;
            }
            Console.WriteLine(DateTime.Now + " Принят список файлов сервера ");
            FilesList.FilesInformation DifferencesList = new FilesList.FilesInformation();
            FilesList.CalculateDifference(RemoteList, LocalList, DifferencesList);
            Console.WriteLine(DateTime.Now + "Размер списка на загрузку - "+DifferencesList.FilesList.Count.ToString());
            //запрашиваем отсутствующие файлы с сервера
            RequestFileList(clientUpdate, DifferencesList);
        }
        // Запрос файлов для загрузки
        public static void RequestFileList(TcpClient clientUpdate, FilesList.FilesInformation DifferencesList)
        {
            XmlSerializer fileSerializer = new XmlSerializer(typeof(FilesList.FilesInformation));
            MemoryStream stream = new MemoryStream();
            NetworkStream tcpStreamTemp = clientUpdate.GetStream();
            fileSerializer.Serialize(stream, DifferencesList);
            stream.Position = 0;
            long streamLen = stream.Length;
            while (true)
            {
                Byte[] bytes;
                if (streamLen > clientUpdate.ReceiveBufferSize)
                {
                    bytes = new Byte[clientUpdate.ReceiveBufferSize];
                }
                else
                {
                    bytes = new Byte[streamLen];
                }
                int readByte = stream.Read(bytes, 0, bytes.Length);
                streamLen -= readByte;

                if (readByte <= 0)
                    break;

                tcpStreamTemp.Write(bytes, 0, readByte);
            }
            Thread.Sleep(200);
            Byte[] endSendBytes = Encoding.Default.GetBytes("ENDLIST");
            tcpStreamTemp.Write(endSendBytes, 0, endSendBytes.Length);
            stream.Close();
            Console.WriteLine(DateTime.Now + " Список файлов для загрузки отправлен. Размер списка " + DifferencesList.FilesList.Count.ToString());
            //Прием файлов, лол
            RecievFilesByList(clientUpdate, DifferencesList);
        }

        //Получение файлов по списку
        private static void RecievFilesByList(TcpClient clientUpdate, FilesList.FilesInformation DifferencesList)
        {
            try
            {
                NetworkStream tcpStreamTemp = clientUpdate.GetStream();
                //Принимаем каждый файл в цикле
                foreach (FilesList.FileElement file in DifferencesList.FilesList)//for (int i = 0; i < DifferencesList.FilesList.Count; i++)
                {
                    try
                    {
                        if (file.Deleted) continue;
                        int LengthList = 0;
                        FilesList.CreateDir(LocalDirName, file.Path);

                        file.state = 2;
                        int index = LocalList.FilesList.FindIndex(x => x.Path == file.Path);
                        if (index > -1)
                        {
                            if (LocalList.FilesList[index].LastChange.CompareTo(file.LastChange) <= 0)
                                LocalList.FilesList[index] = file;
                        }
                        else//иначе добавляем как новый
                        {
                            LocalList.FilesList.Add(file);
                        }
                        FilesSender.SaveFile(clientUpdate, LocalDirName, file);
                        //Устанавливаем дату последнего изменения
                        System.IO.File.SetLastWriteTime(LocalDirName + file.Path, file.LastChange);
                        //Если дата изменения файла была более новая, то меняем файл в списке
                        
                    }
                    catch (Exception exx)
                    {
                        Console.WriteLine("Ошибка при скачивании файла "+file.Path+" ;"+exx.Message);
                    }

                }
                int size = ("ENDFILES").Length;
                byte[] endFiles = new byte[size];
                int readByte2 = tcpStreamTemp.Read(endFiles, 0, endFiles.Length);
                string answerEnd = Encoding.Default.GetString(endFiles).Replace("\0", "").Trim();
                if (answerEnd == "ENDFILES")
                    Console.WriteLine("Все файлы из списка успешно скачены.");
                else
                {
                    Console.WriteLine("Все файлы из списка не скачены. Ошибка. "+answerEnd);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при получении списка файлов. " + ex.Message);
            }
        }
        //отправка локалького списка
        public static void SendFileList(TcpClient clientUpdate, FilesList.FilesInformation ListForSend)
        {
            XmlSerializer fileSerializer = new XmlSerializer(typeof(FilesList.FilesInformation));
            MemoryStream stream = new MemoryStream();
            fileSerializer.Serialize(stream, ListForSend);
            stream.Position = 0;
            NetworkStream tcpStream = clientUpdate.GetStream();
            long streamLen = stream.Length;
            while (true)
            {
                Byte[] bytes;
                if (streamLen > clientUpdate.ReceiveBufferSize)
                {
                    bytes = new Byte[clientUpdate.ReceiveBufferSize];
                }
                else
                {
                    bytes = new Byte[streamLen];
                }
                int readByte = stream.Read(bytes, 0, bytes.Length);
                streamLen -= readByte;

                if (readByte <= 0)
                    break;

                tcpStream.Write(bytes, 0, readByte);
            }
            Thread.Sleep(200);
            Byte[] endSendBytes = Encoding.Default.GetBytes("ENDLIST");
            tcpStream.Write(endSendBytes, 0, endSendBytes.Length);
            stream.Close();
            Console.WriteLine(DateTime.Now + " Список файлов отправлен. Размер списка " + ListForSend.FilesList.Count.ToString());
            GetFileDetails();
        }
        
        //Добавление события по срабатыванию таймера(чтобы не дублировались OnChanged
        private static void t_Elapsed(Object sender, System.Timers.ElapsedEventArgs e)
        {
            watcher.Created += new FileSystemEventHandler(OnCreated);
            ((System.Timers.Timer)sender).Stop();
             Console.WriteLine("The Elapsed event was raised at {0}", e.SignalTime);
        }
        //Ошибка воучера
        private static void OnError(object source, ErrorEventArgs e)
        {
            Console.WriteLine(e.GetException());
        }
        //Срабатывание по таймеру
        private static void test_Elapsed(Object sender, System.Timers.ElapsedEventArgs e)
        {
           // int temp_count = count_list;
            myTimer.Enabled = false;
            ((System.Timers.Timer)sender).Stop();
           // Thread sendThread = new Thread(sendChanges,10);
          //  sendThread.Start();
        }
        //отправляем изменения на сервер
        /*
        private static void sendChanges()
        {
            //Получаем список файлов с сервера, чтобы закачать отсутствующие файлы
            // 
            Console.WriteLine(DateTime.Now + " Поток " + "<" + Thread.CurrentThread.ManagedThreadId.ToString() + ">" + " запущен.");
            
            while (blockThread)
            {
                Thread.Sleep(2000);
            }
            blockThread = true;
            
            Console.WriteLine(DateTime.Now + " Поток " + "<" + Thread.CurrentThread.ManagedThreadId.ToString() + ">" + " начал работу.");
            int count = 100;// LocalList.FilesList.FindAll(x => x.state == 0).Count;
            //Отправляем список на сервер, чтобы добавить на него отсутствующие файлы

            //Получаем первые 100 локальных файлов( будет отправлять по 100 штук за раз)
            
            FilesList.FilesInformation ListForSend = new FilesList.FilesInformation();
            if (LocalList.FilesList.FindAll(x => x.state == 0).Count >= 100)
            {
                if (LocalList.FoldersList.FindAll(x => x.state == 0).Count >= 100)
                    ListForSend = new FilesList.FilesInformation(LocalList.FilesList.FindAll(x => x.state == 0).GetRange(0, 100), LocalList.FoldersList.FindAll(x => x.state == 0).GetRange(0, 100));
                else

                    ListForSend = new FilesList.FilesInformation(LocalList.FilesList.FindAll(x => x.state == 0).GetRange(0, 100), LocalList.FoldersList.FindAll(x => x.state == 0).GetRange(0, LocalList.FoldersList.FindAll(x => x.state == 0).Count));
            }
            else
                ListForSend = new FilesList.FilesInformation(LocalList.FilesList.FindAll(x => x.state == 0).GetRange(0, LocalList.FilesList.FindAll(x => x.state == 0).Count), LocalList.FoldersList.FindAll(x => x.state == 0).GetRange(0, LocalList.FoldersList.FindAll(x => x.state == 0).Count));

            foreach (FilesList.FileElement file in ListForSend.FilesList)//for (int i = 0; i < count; i++)
            {
                int index = LocalList.FilesList.FindIndex(x=> x == file);
                if (!File.Exists(LocalDirName + file.Path))
                {
                    LocalList.FilesList[index].Deleted = true;
                    file.Deleted = true;
                }
                LocalList.FilesList[index].LastChange = File.GetLastWriteTime(LocalDirName + file.Path);
                file.LastChange = LocalList.FilesList[index].LastChange;
            } 
            SendFileList(client, ListForSend);
            //если есть не отправленные файлы - повторяем
            if (LocalList.FilesList.FindAll(x => x.state == 0).Count>0)
            {
                Console.WriteLine(DateTime.Now + " Поток " + "<" + Thread.CurrentThread.ManagedThreadId.ToString() + ">" + " Еще раз отправил изменения(рекурсия).");
                blockThread = false;
                sendChanges();
            }
            //очистка локального списка
            LocalClear();
            Console.WriteLine(DateTime.Now + " Поток " + "<" + Thread.CurrentThread.ManagedThreadId.ToString() + ">" + " закончил работу.");
            blockThread = false;
        }
        */
        //удаляем пометки о событияих renamed/deleted
        private static void LocalClear()
        {
            List<FilesList.FileElement> tempList = LocalList.FilesList.FindAll(x=> x.Deleted || x.OldPath!="" || x.OldName!="");
            int i = 0;
            while(i<tempList.Count)
            {
                FilesList.FileElement file = tempList[i];
                if (file.Deleted)
                {
                    LocalList.FilesList.Remove(file);
                    tempList.Remove(file);
                    continue;
                }
                if (file.OldPath != "")
                {
                    int index = LocalList.FilesList.FindIndex(x => x.Path == file.Path && x.Name == file.Name);
                    LocalList.FilesList[index].OldPath = "";
                    LocalList.FilesList[index].OldName = "";
                }
                i++;
            }
        }
        //Добавление файла
        private static void OnCreated(object source, FileSystemEventArgs e)
        {

            if (!myTimer.Enabled)
            {
                myTimer.Elapsed += test_Elapsed;
                myTimer.AutoReset = true;
                myTimer.Enabled = true;
                myTimer.Interval = 5000;
                myTimer.Start();
            }
            else
                myTimer.Interval = 5000;
            if (!File.Exists(e.FullPath)) // Папка
            {
                AsyncAddFoldersToListCaller caller = new AsyncAddFoldersToListCaller(AsyncAddFoldersToList);
                IAsyncResult result = caller.BeginInvoke(e.FullPath, null, null);
                caller.EndInvoke(result);
            }
            else
            {
                AsyncMethodCaller caller = new AsyncMethodCaller(AsyncAddFilesToList);
                IAsyncResult result = caller.BeginInvoke(e.FullPath, null, null);
                caller.EndInvoke(result);
            }
        }

        //Асинхронное добавление папки в спиисок
        public delegate void AsyncAddFoldersToListCaller(string FullPath);
        public static void AsyncAddFoldersToList(string FullPath)
        {

            DirectoryInfo dr = new DirectoryInfo(FullPath);
            FilesList.FolderElement folder = new FilesList.FolderElement();
            folder.Name = dr.Name;
            folder.Path = dr.FullName.Replace(LocalDirName, "");
            if (LocalList.FoldersList.FindIndex(x => x.Path == folder.Path) == -1)
            LocalList.FoldersList.Add(folder);
        }

        //Асинхронное добавление файла в список
        public delegate void AsyncMethodCaller(string FullPath);
        public static void AsyncAddFilesToList(string FullPath)
        {

            FilesList.FileElement file = new FilesList.FileElement();
            FileInfo fi = new FileInfo(FullPath);
            file.Name = fi.Name;
            file.Path = fi.FullName.Replace(LocalDirName, "");
                file.LastChange = fi.LastWriteTime;
           // file.Length = fi.Length;
            if(LocalList.FilesList.FindIndex(x=>x.Path==file.Path)==-1)
             LocalList.FilesList.Add(file);
        }
        //изменение файла
        private static void OnChanged(object source, FileSystemEventArgs e)
        {
            if (File.Exists(e.FullPath)) // Файл
            {
                
                FileInfo fi = new FileInfo(e.FullPath);
                FilesList.FileElement file = LocalList.FilesList.Find(x => x.Path == e.FullPath.Replace(LocalDirName, ""));
                int index = LocalList.FilesList.FindIndex(x => x.Path == e.FullPath.Replace(LocalDirName, ""));
                file.LastChange = fi.LastWriteTime;
                LocalList.FilesList[index] = file;
                Console.WriteLine("Время: " + DateTime.Now);
                Console.WriteLine("Изменение файла");
                Console.WriteLine("Путь: " + e.FullPath);
                Console.WriteLine("Тип изменения: " + e.ChangeType);
                Console.WriteLine("Длина файла: " + fi.Length + " бит");
              //  Console.WriteLine("MD5-хеш файла: " + ComputeMD5Checksum(e.FullPath));
                Console.WriteLine("Последнее изменение файла: " + fi.LastWriteTime);
                Console.WriteLine();
            }
            else
            {
                DirectoryInfo fi = new DirectoryInfo(e.FullPath);
                FilesList.FolderElement folder = LocalList.FoldersList.Find(x => x.Path == e.FullPath.Replace(LocalDirName, ""));
                folder.LastChange = fi.LastWriteTime;
                int index = LocalList.FoldersList.FindIndex(x => x.Path == e.FullPath.Replace(LocalDirName, ""));
                LocalList.FoldersList[index] = folder;
                Console.WriteLine("Время: " + DateTime.Now);
                Console.WriteLine("Изменение папки");
                Console.WriteLine("Тип изменения: " + e.ChangeType);
                Console.WriteLine("Путь: " + e.FullPath);
                Console.WriteLine();
            }
        }
        //Переименование файла
        private static void OnRenamed(object source, RenamedEventArgs e)
        {

            if (!myTimer.Enabled)
            {
                myTimer.Elapsed += test_Elapsed;
                myTimer.AutoReset = true;
                myTimer.Enabled = true;
                myTimer.Interval = 500;
                myTimer.Start();
            }
            else
                myTimer.Interval = 500;

            if (File.Exists(e.FullPath)) // Файл
            {
                FileInfo fi = new FileInfo(e.FullPath);
                Console.WriteLine("Время: " + DateTime.Now);
                Console.WriteLine("Directory: " + fi.Directory);
                Console.WriteLine("Переименование файла");
                Console.WriteLine("Путь: " + e.FullPath);
                Console.WriteLine("Имя файла: " + e.Name);
                Console.WriteLine("Старое имя файла: " + e.OldName);
                Console.WriteLine("Тип изменения: " + e.ChangeType);
                Console.WriteLine("Длина файла: " + fi.Length + " бит");
               // Console.WriteLine("MD5-хеш файла: " + ComputeMD5Checksum(e.FullPath));
                Console.WriteLine("Последнее изменение файла: " + fi.LastWriteTime);
                Console.WriteLine();

                FilesList.FileElement newFile = new FilesList.FileElement();
                newFile.Name = fi.Name;
                newFile.Path = fi.FullName.Replace(LocalDirName, "");
                newFile.OldPath = e.OldFullPath.Replace(LocalDirName, "");
                newFile.OldName = e.OldName;
                newFile.LastChange = fi.LastWriteTime;
                newFile.Length = fi.Length;
                int index = LocalList.FilesList.FindIndex(x => x.Path == e.OldFullPath.Replace(LocalDirName, ""));

                if (index != -1)
                LocalList.FilesList[index] = newFile;
            }
            else
            {
                DirectoryInfo di = new DirectoryInfo(e.FullPath);
                FilesList.FolderElement folder = new FilesList.FolderElement();
                folder.Name = di.Name;
                folder.Path = di.FullName.Replace(LocalDirName, "");
                folder.OldPath = e.OldFullPath.Replace(LocalDirName,"");
                folder.LastChange = di.LastWriteTime;
                int index = LocalList.FoldersList.FindIndex(x => x.Path == e.OldFullPath.Replace(LocalDirName, ""));
                if (index != -1)
                LocalList.FoldersList[index] = folder;
                Console.WriteLine("Время: " + DateTime.Now);
                Console.WriteLine("Переименование папки");
                Console.WriteLine("Тип изменения: " + e.ChangeType);
                Console.WriteLine();
            }
        }

        //Удаление файла

        private static void OnDeleted(object source, FileSystemEventArgs e)
        {
            if (!myTimer.Enabled)
            {
                myTimer.Elapsed += test_Elapsed;
                myTimer.AutoReset = true;
                myTimer.Enabled = true;
                myTimer.Interval = 1500;
                myTimer.Start();
            }
            else
                myTimer.Interval = 1500;
            Console.WriteLine("Время: " + DateTime.Now);
            Console.WriteLine("Удаление файла или папки");
            Console.WriteLine("Путь: " + e.FullPath);
            Console.WriteLine("Тип изменения: " + e.ChangeType);
            //Console.WriteLine("Длина файла: " + fi.Length + " бит");
            //Console.WriteLine("MD5-хеш файла: " + ComputeMD5Checksum(e.FullPath));
            //Console.WriteLine("Последнее изменение файла: " + fi.LastWriteTime);
            Console.WriteLine();

            FilesList.FileElement newFile = new FilesList.FileElement();
            newFile.Name = e.Name;
            newFile.Path = e.FullPath.Replace(LocalDirName, "");
            newFile.Deleted = true;
            int index = LocalList.FilesList.FindIndex(x => x.Path == newFile.Path);
            if (index > -1)
            {
                LocalList.FilesList[index] = newFile;
            }
            else
            {
                FilesList.FolderElement folder = new FilesList.FolderElement();
                folder.Name = e.Name;
                folder.Path = e.FullPath.Replace(LocalDirName, "");
                folder.Deleted = true;
                index = LocalList.FoldersList.FindIndex(x => x.Path == newFile.Path);
                if (index > -1)
                    LocalList.FoldersList[index] = folder;
            }
        }

          // Получение списка файлов для отправки
        public static void GetFileDetails()
        {
            NetworkStream tcpStream = client.GetStream();
            XmlSerializer fileSerializer = new XmlSerializer(typeof(FilesList.FilesInformation));
            MemoryStream stream = FilesSender.GetRemoteList(client);// new MemoryStream();
            /*
            int LengthList = 0;//длина списка в байтах
            while (true)
            {
                byte[] recievByte = new byte[client.ReceiveBufferSize];
                int readByte = tcpStream.Read(recievByte, 0, client.ReceiveBufferSize);
                string answer = Encoding.Default.GetString(recievByte).Replace("\0", "").Trim();
                int indexEnd = answer.IndexOf("ENDLIST", 0, answer.Length);
                if (indexEnd != (-1))
                {
                    stream.Write(recievByte, 0, indexEnd);
                    break;
                }
                stream.Write(recievByte, 0, readByte);
                LengthList += readByte;
            }
            */
            stream.Position = 0;
            byte[] buffer = new byte[stream.Length];
            stream.Read(buffer, 0, (int)stream.Length);
            string streamStr = Encoding.Default.GetString(buffer);
            stream.Position = 0;
            FilesList.FilesInformation RemoteList = new FilesList.FilesInformation();
            RemoteList = (FilesList.FilesInformation)fileSerializer.Deserialize(stream);
            //Console.WriteLine(DateTime.Now + " Принят список файлов для отправки размером " + stream.Length + " байт");
            Console.WriteLine(DateTime.Now + " Список файлов для отправки получен. Размер списка "+RemoteList.FilesList.Count.ToString());
            
            //Отправка этого списка файлов лол
            SendFilesByList(RemoteList);
        }
        private static void SendFilesByList(FilesList.FilesInformation RemoteList)
        {
            try
            {
                NetworkStream tcpStream = client.GetStream();
                //Отправляем каждый файл в цикле
                foreach (FilesList.FileElement file in RemoteList.FilesList)//for (int i = 0; i < DifferencesList.FilesList.Count; i++)
                {try{
                        //Отправка файла
                        FilesSender.SendFile(client, LocalDirName, file);
                        LocalList.FilesList[LocalList.FilesList.FindIndex(x => x.Path == file.Path)].state = 2;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Ошибка при отправке файла " + file.Path + " " + ex.Message);
                    }

                }
                byte[] endFiles = Encoding.Default.GetBytes("ENDFILES");
                tcpStream.Write(endFiles, 0, endFiles.Length);
                foreach (FilesList.FolderElement folder in RemoteList.FoldersList)
                {
                    LocalList.FoldersList[LocalList.FoldersList.FindIndex(x => x.Path == folder.Path)].state = 2;
                }
                Console.WriteLine("Все файлы успешно отправлены.");
            }
            catch (Exception ex2)
            {
                Console.WriteLine("Ошибка при отправке файлов" + ex2.Message);
            }
        }
        private static void GetUpdate()
        {
            try{
                int i = 1;
            while (true)
            {
                //ожидаем обновлений
                //client2
                GetFileList(client2);
                Console.WriteLine(DateTime.Now + " Обновления получены {0} раз.",i.ToString());
                i++;
            }
            }
            catch(Exception ex)
            {
                Console.WriteLine(DateTime.Now+" Ошибка при получении обновлений."+ex.Message);
            }
        }
        private static void Sending()
        {
            while (true)
            {
                int count;
                if ((LocalList.FilesList.Count) > 500)
                    count = (int)(LocalList.FilesList.Count / 10);//100;// LocalList.FilesList.FindAll(x => x.state == 0).Count;
                else
                    count = 100;
                //Отправляем список на сервер, чтобы добавить на него отсутствующие файлы

                //Получаем первые 100 локальных файлов( будет отправлять по 100 штук за раз)

                FilesList.FilesInformation ListForSend = new FilesList.FilesInformation();
                if (LocalList.FilesList.FindAll(x => x.state == 0).Count >= count)
                {
                    if (LocalList.FoldersList.FindAll(x => x.state == 0).Count >= count)
                        ListForSend = new FilesList.FilesInformation(LocalList.FilesList.FindAll(x => x.state == 0).GetRange(0, count), LocalList.FoldersList.FindAll(x => x.state == 0).GetRange(0, count));
                    else

                        ListForSend = new FilesList.FilesInformation(LocalList.FilesList.FindAll(x => x.state == 0).GetRange(0, count), LocalList.FoldersList.FindAll(x => x.state == 0).GetRange(0, LocalList.FoldersList.FindAll(x => x.state == 0).Count));
                }
                else
                    ListForSend = new FilesList.FilesInformation(LocalList.FilesList.FindAll(x => x.state == 0).GetRange(0, LocalList.FilesList.FindAll(x => x.state == 0).Count), LocalList.FoldersList.FindAll(x => x.state == 0).GetRange(0, LocalList.FoldersList.FindAll(x => x.state == 0).Count));

                if (ListForSend.FilesList.Count == 0 /* && ListForSend.FoldersList.Count == 0 */ )
                {
                    Thread.Sleep(5000);
                    continue;
                }
                foreach (FilesList.FileElement file in ListForSend.FilesList)//for (int i = 0; i < count; i++)
                {
                    int index = LocalList.FilesList.FindIndex(x => x == file);
                    if (!File.Exists(LocalDirName + file.Path))
                    {
                        LocalList.FilesList[index].Deleted = true;
                        file.Deleted = true;
                    }
                    LocalList.FilesList[index].LastChange = File.GetLastWriteTime(LocalDirName + file.Path);
                    file.LastChange = LocalList.FilesList[index].LastChange;
                    file.Length = (new FileInfo(LocalDirName+file.Path)).Length;
                }

                SendFileList(client, ListForSend);

                foreach (FilesList.FileElement file in ListForSend.FilesList)
                {

                    int index = LocalList.FilesList.FindIndex(x => x.Path == file.Path);
                    if (index!=-1)
                        LocalList.FilesList[index].state = 2;
                }
                //очистка локального списка
                LocalClear();
            }
        }
    }
}
