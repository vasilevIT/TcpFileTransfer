using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using Library;
using System.Threading;
using System.Xml.Serialization;
using System.IO;
using System.Net;
namespace ServerBox
{
    class Program
    {
        //Сервер
        private static TcpListener tcpServer;
        //для отправки обновлений
        private static TcpListener tcpServer2;
        private static int localPort = 6000;
        private static bool blockThread = false;
        private static int counter_client =0;
        private static string LocalDir = @"C:\ServerMgupiBox";

        //Список клиентов для рассылки
        private static List<TcpClient> listClients = new List<TcpClient>();

        public static FilesList.FilesInformation LocalList = new FilesList.FilesInformation(); // Список файлов на текущем хосте
       // public static FilesList.FilesInformation RemoteList = new FilesList.FilesInformation(); // Список файлов, закаченный с сервера
       // public static FilesList.FilesInformation DifferencesList = new FilesList.FilesInformation(); // Список файлов, требуемый для закачки
        static void Main(string[] args)
        {
            // Получение имени компьютера.
            String host = System.Net.Dns.GetHostName();
            // Получение ip-адреса.
            System.Net.IPAddress ip = System.Net.Dns.GetHostByName(host).AddressList[0];
            Console.WriteLine(DateTime.Now + " IP адрес сервера:"+ip.ToString());
            Console.WriteLine(DateTime.Now + " Инициализация сервера. Пожалуйста, ждите...");
            tcpServer = new TcpListener(ip, localPort);
            tcpServer2 = new TcpListener(ip, localPort + 1);
            //tcpServer = new TcpListener(IPAddress.Parse("10.20.220.91"), localPort);
            //tcpServer2 = new TcpListener(IPAddress.Parse("10.20.220.91"), localPort + 1);
            try{
                if (!Directory.Exists(LocalDir))
                {
                    Directory.CreateDirectory(LocalDir);
                }
                LocalList.Initialize(LocalDir, LocalDir);
                Console.WriteLine(DateTime.Now + " Локальная папка: "+LocalDir);
                //FilesList.PrintList(LocalList);
                tcpServer.Start();
                tcpServer2.Start();
                
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка запуска сервера. " + ex.Message);
                Console.ReadKey();
                return;
            }
            ThreadPool.SetMaxThreads(Environment.ProcessorCount*5, 0);
            while (true)
            {
                Console.Write("\nWaiting for a connection... ");

                // При появлении клиента добавляем в очередь потоков его обработку.
                ThreadPool.QueueUserWorkItem(NewClient, tcpServer.AcceptTcpClient());
                ThreadPool.QueueUserWorkItem(NewClientUpdate, tcpServer2.AcceptTcpClient());
                // Выводим информацию о подключении.
                counter_client++;
                Console.Write("\nConnection №" + counter_client.ToString() + "!");
            }
        }

        //добавляем клиента для рассылки обновленией:)
        private static void NewClientUpdate(object obj)
        {

            TcpClient clientForUpdate = (TcpClient)obj;

            listClients.Add(clientForUpdate);
            Console.WriteLine(DateTime.Now+" Подключили нового клиента. <"+clientForUpdate.GetHashCode().ToString()+">");
           // clientForUpdate.Close();
        }
        private static void NewClient(object obj)
        {
            try
            {
                TcpClient client = (TcpClient)obj;
                //отправляем локальный список, чтобы клиент прислал разницу
                SendLocalFileList(client);
                //получаем список файлов, которых не хватает на сервере, но они есть у клиента
                GetFileList(client);
                try
                {
                    int i = 0;
                    while (true)
                    {
                        //if (i > 10) break;
                        //ловим что-то
                        //получаем список файлов, которых не хватает на сервере, но они есть у клиента
                        GetFileList(client);
                        //отправляем локальный список, чтобы клиент прислал разницу
                      //  SendLocalFileList(client);
                         
                        i++;
                    }
                    
                }
                catch (Exception ex2)
                {
                    Console.WriteLine("Клиент отключился "+ex2.Message);
                }
                
                client.GetStream().Close();
                client.Close();
                //listClients.Remove(client);
                
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
                 

        }


        // Отправка локальных файлов для проверки клиента
        public static void SendLocalFileList(TcpClient client)
        {
            XmlSerializer fileSerializer = new XmlSerializer(typeof(FilesList.FilesInformation));
            MemoryStream stream = new MemoryStream();
            NetworkStream tcpStream = client.GetStream();
            fileSerializer.Serialize(stream, LocalList);
            stream.Position = 0; 
            byte[] buffer = new byte[stream.Length];
            stream.Read(buffer, 0, (int)stream.Length);
            string streamStr = Encoding.Default.GetString(buffer);
            stream.Position = 0;
            while (true)
            {
                Byte[] bytes = new Byte[client.ReceiveBufferSize];
                int readByte = stream.Read(bytes, 0, bytes.Length);
                if (readByte <= 0)
                    break;
                tcpStream.Write(bytes, 0, readByte);
            }
            Thread.Sleep(200);
            Byte[] endSendBytes = Encoding.Default.GetBytes("ENDLIST");
            tcpStream.Write(endSendBytes, 0, endSendBytes.Length);
            // tcpStream.Close();
            stream.Close();
            Console.WriteLine(DateTime.Now + " Список локальных файлов отправлен. Длина списка "+LocalList.FilesList.Count.ToString());
            //получаем разницу файлов...
            GetFileDetails(client);


        }

        // Отправка локальных файлов для проверки клиента на обновления
        public static void SendLocalFileListForUpdate(TcpClient clientUpdate, FilesList.FilesInformation updateList)
        {
            XmlSerializer fileSerializer = new XmlSerializer(typeof(FilesList.FilesInformation));
            MemoryStream stream = new MemoryStream();
            NetworkStream tcpStream = clientUpdate.GetStream();
            fileSerializer.Serialize(stream, updateList);
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

                tcpStream.Write(bytes, 0, readByte);
            }
            Thread.Sleep(200);
            Byte[] endSendBytes = Encoding.Default.GetBytes("ENDLIST");
            tcpStream.Write(endSendBytes, 0, endSendBytes.Length);
            // tcpStream.Close();
            stream.Close();
            Console.WriteLine(DateTime.Now + " Список локальных файлов для обновления клиента отправлен. Длина списка " + updateList.FilesList.Count.ToString());
            //получаем разницу файлов...
            GetFileDetails(clientUpdate);


        }

        // Запрос файлов для загрузки
        public static void RequestFileList(TcpClient client, FilesList.FilesInformation DifferencesList ,FilesList.FilesInformation updateLocalList)
        {
            XmlSerializer fileSerializer = new XmlSerializer(typeof(FilesList.FilesInformation));
            MemoryStream stream = new MemoryStream();
            NetworkStream tcpStream = client.GetStream();
            fileSerializer.Serialize(stream, DifferencesList);
            stream.Position = 0;
            while (true)
            {
                Byte[] bytes = new Byte[client.ReceiveBufferSize];
                int readByte = stream.Read(bytes, 0, bytes.Length);
                if (readByte <= 0)
                    break;
                tcpStream.Write(bytes, 0, bytes.Length);
            }
            Thread.Sleep(200);
            Byte[] endSendBytes = Encoding.Default.GetBytes("ENDLIST");
            tcpStream.Write(endSendBytes, 0, endSendBytes.Length);
           // tcpStream.Close();
            stream.Close();
            Console.WriteLine(DateTime.Now + " Список файлов для загрузки отправлен. Размер " + DifferencesList.FilesList.Count.ToString());

            //Прием файлов, лол
            RecievFilesByList(client, DifferencesList, updateLocalList);
        }


        public delegate void AsyncMethodCaller(FilesList.FileElement file);
        //Получение файлов по списку
        private static void RecievFilesByList(TcpClient client, FilesList.FilesInformation DifferencesList, FilesList.FilesInformation updateLocalList)
        {

            try
            {
                NetworkStream tcpStream = client.GetStream();
                //Принимаем каждый файл в цикле
                foreach(FilesList.FileElement file in DifferencesList.FilesList)//for (int i = 0; i < DifferencesList.FilesList.Count; i++)
                {
                      //  int LengthList = 0;
                    /*
                        if (File.Exists(LocalDir + file.Path))
                        {
                            if (LocalList.FilesList.FindIndex(x => x.Path == file.Path) != -1)
                                continue;
                        }
                    */
                        //скачиваем файл
                        string tempPath = LocalDir + @"\" + client.GetHashCode().ToString() + file.Path;
                        FilesSender.SaveFile(client,LocalDir,file,true);


                       // byte[] successReciev = Encoding.Default.GetBytes("OKFILE");
                      //  tcpStream.Write(successReciev, 0, successReciev.Length);


                         //Если дата изменения файла была более новая, то меняем файл в списке
                         //Или если файл кто-то закачал раньше, но у него более старая дата
                        if (File.Exists(LocalDir + file.Path))
                        {
                            if (DateTime.Compare(File.GetLastWriteTime(LocalDir + file.Path), file.LastChange)>=0)//File.GetLastWriteTime(LocalDir + file.Path).CompareTo(file.LastChange) > 0)
                            {
                                File.Delete(tempPath);
                                Console.WriteLine(DateTime.Now + " Файл {0} не удалось сохранить. Существует более свежий файл.", tempPath.Replace(LocalDir, ""));
                                Console.WriteLine(" Файл на закачку {0} !====! файл на сервере {1} .", new string[] { tempPath.Replace(LocalDir, "") + file.LastChange, file.Path + File.GetLastWriteTime(LocalDir + file.Path).ToString() });
                            }
                            else
                            {

                                File.Delete(LocalDir + file.Path);
                                File.Move(tempPath, LocalDir + file.Path);
                                //LocalList.FilesList[index] = file;
                                int index = LocalList.FilesList.FindIndex(x => x.Path == file.Path);
                                if (index > -1)
                                {
                                    LocalList.FilesList[index] = file;
                                    int indexUpdate = updateLocalList.FilesList.FindIndex(x => x.Path == file.Path);
                                    if (index > -1)
                                    {
                                        updateLocalList.FilesList[indexUpdate] = file;
                                    }
                                }
                                //Устанавливаем дату последнего изменения
                                System.IO.File.SetLastWriteTime(LocalDir + file.Path, file.LastChange);
                                Console.WriteLine(DateTime.Now + " Скопировали {0} в {1}.", new string[] { tempPath, file.Path });
                            }
                        }
                        else
                        {

                            File.Move(tempPath, LocalDir + file.Path);
                            LocalList.FilesList.Add(file);
                            updateLocalList.FilesList.Add(file);
                            //Устанавливаем дату последнего изменения
                            System.IO.File.SetLastWriteTime(LocalDir + file.Path, file.LastChange);
                        }

                    }

                    if (Directory.Exists(LocalDir + @"\" + client.GetHashCode().ToString()))
                        Directory.Delete(LocalDir + @"\" + client.GetHashCode().ToString(), true);

                    int size = ("ENDFILES").Length;
                    byte[] endFiles = new byte[size];
                    int readByte2 = tcpStream.Read(endFiles, 0, endFiles.Length);
                    string answerEnd = Encoding.Default.GetString(endFiles).Replace("\0", "").Trim();
                    if (answerEnd == "ENDFILES")
                        Console.WriteLine("Все файлы из списка успешно скачены.");
                    else
                    {
                        Console.WriteLine("Не найден конец списка..." + answerEnd);
                        throw new Exception("Не найден конец списка..." + answerEnd);
                    }


                    foreach (FilesList.FolderElement folder in DifferencesList.FoldersList)
                    {

                        if (!Directory.Exists(LocalDir + folder.Path))
                        {
                            Directory.CreateDirectory(LocalDir + folder.Path);
                        }
                    }

            }
            catch(Exception ex)
            {
                Console.WriteLine("Ошибка при получении списка файлов. "+ex.Message);
            }

        }
        //отправка локалького списка
        public static void SendFileList(TcpClient client, FilesList LocalList)
        {
            XmlSerializer fileSerializer = new XmlSerializer(typeof(FilesList.FilesInformation));
            MemoryStream stream = new MemoryStream();
            fileSerializer.Serialize(stream, LocalList);
            stream.Position = 0;
            Byte[] bytes = new Byte[stream.Length];
            stream.Read(bytes, 0, Convert.ToInt32(stream.Length));
            NetworkStream tcpStream = client.GetStream();
            tcpStream.Write(bytes, 0, bytes.Length);
           // tcpStream.Close();
            stream.Close();
            Console.WriteLine(DateTime.Now + " Список файлов отправлен. Длина списка хз");
        }


        //получение удаленного списка
        public static void GetFileList(TcpClient client)
        {
            NetworkStream tcpStream = client.GetStream();
            XmlSerializer fileSerializer = new XmlSerializer(typeof(FilesList.FilesInformation));
            MemoryStream stream = new MemoryStream();
            FilesList.FilesInformation RemoteList = new FilesList.FilesInformation();
            stream = FilesSender.GetRemoteList(client);
            byte[] buffer = new byte[stream.Length];
            stream.Read(buffer, 0, (int)stream.Length);
            string st = Encoding.Default.GetString(buffer);
            int LengthList = (int)stream.Length;//длина списка в байтах
            /*
            while (true)
            {
                byte[] recievByte = new byte[client.ReceiveBufferSize];
                int readByte = tcpStream.Read(recievByte, 0, client.ReceiveBufferSize);
                string answer = Encoding.Default.GetString(recievByte).Replace("\0", "").Trim();
                if (answer == "ENDLIST")
                    break;

                int emptyByte = recievByte.Length - 1;
                while (recievByte[emptyByte] == 0)
                    emptyByte--;
                stream.Write(recievByte, 0, emptyByte + 1);
                LengthList += emptyByte + 1;
            }
            */
            stream.Position = 0;
            RemoteList = (FilesList.FilesInformation)fileSerializer.Deserialize(stream);
            FilesList.FilesInformation updateLocalList = new FilesList.FilesInformation(LocalList);
            List<FilesList.FileElement> tempList = RemoteList.FilesList.FindAll(x => x.OldPath != "" || x.Deleted);
            int i = 0;
            try
            {
                while (i < tempList.Count)//foreach(FilesList.FileElement file in )
                {
                    FilesList.FileElement file = tempList[i];
                    if (file.Deleted)
                    {
                        int index = LocalList.FilesList.FindIndex(x => x.Path == file.Path && x.Name == file.Name);
                        if (index != -1)
                        {
                            File.Delete(LocalDir + file.Path);

                            int indexInUpdate = updateLocalList.FilesList.FindIndex(x => x.Path == file.Path && x.Name == file.Name);
                            updateLocalList.FilesList[indexInUpdate].Deleted = true;//дерьмо
                            LocalList.FilesList.RemoveAt(index);
                            RemoteList.FilesList.Remove(file);
                            tempList.RemoveAt(i);
                            Console.WriteLine("Удалили файл " + file.Path);
                            continue;
                        }
                    }
                    if (file.OldPath != "")
                    {
                        int index = LocalList.FilesList.FindIndex(x => x.Path == file.OldPath);
                        int indexRemote = RemoteList.FilesList.FindIndex(x => x.Path == file.OldPath);
                        if (index != -1)
                        {
                            FilesList.FileElement newFile = new FilesList.FileElement();
                            newFile.Name = file.Name;
                            newFile.Path = file.Path;
                            newFile.LastChange = file.LastChange;
                            newFile.Length = file.Length;
                            updateLocalList.FilesList[index] = file;
                            LocalList.FilesList[index] = newFile;
                            if (indexRemote != -1)
                            {
                                RemoteList.FilesList[indexRemote] = newFile;
                            }
                            File.Move(LocalDir + file.OldPath, LocalDir + file.Path);
                            Console.WriteLine("Переименовали файл " + file.OldPath + " на " + file.Path);
                            continue;
                        }
                    }
                    i++;
                }

                List<FilesList.FolderElement> tempdDirList = RemoteList.FoldersList;//.FindAll(x => x.OldPath != "" || x.Deleted);
                i = 0;
                while (i < tempdDirList.Count)//foreach(FilesList.FileElement file in )
                {

                    FilesList.FolderElement folder = tempdDirList[i];

                    if (!Directory.Exists(LocalDir + folder.Path))
                    {
                        Directory.CreateDirectory(LocalDir + folder.Path);
                    }
                    if (folder.Deleted)
                    {
                        int index = LocalList.FilesList.FindIndex(x => x.Path == folder.Path);
                        int index2 = RemoteList.FilesList.FindIndex(x => x.Path == folder.Path);
                        if (index != -1)
                        {
                            if (Directory.Exists(LocalDir + folder.Path))
                            {
                                Directory.Delete(LocalDir + folder.Path);
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

                            if (File.Exists(LocalDir + folder.Path))
                            {
                                Directory.Move(LocalDir + folder.OldPath, LocalDir + folder.Path);
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
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка при анализе списка клиента "+ex.Message);
            }
            Console.WriteLine(DateTime.Now + " Принят список файлов размером " + LengthList + " байт");
            FilesList.FilesInformation DifferencesList = new FilesList.FilesInformation();
            FilesList.CalculateDifference(RemoteList, LocalList, DifferencesList);
            RequestFileList(client, DifferencesList, updateLocalList);
            Console.WriteLine(DateTime.Now + " Список файлов получен. Длина списка " + RemoteList.FilesList.Count.ToString());

            //рассылаем список на обновление всем клиентам
            Thread updateThread = new Thread(SendForAllClient, 6);
            updateThread.Start(updateLocalList);
        }

        // Получение списка файлов для загрузки
        public static void GetFileDetails(TcpClient clientRemote)
        {
            NetworkStream tcpStream = clientRemote.GetStream();
            XmlSerializer fileSerializer = new XmlSerializer(typeof(FilesList.FilesInformation));
            MemoryStream stream = FilesSender.GetRemoteList(clientRemote);
            FilesList.FilesInformation RemoteList = new FilesList.FilesInformation();
            RemoteList = (FilesList.FilesInformation)fileSerializer.Deserialize(stream);
            Console.WriteLine(DateTime.Now + " Принят список файлов клиента, требуемых для отправки ему. Размер списка "+RemoteList.FilesList.Count.ToString());
            //Отправка этого списка файлов
            SendFilesByList(clientRemote, RemoteList);
        }


        private static void SendFilesByList(TcpClient client,FilesList.FilesInformation RemoteList)
        {
            try
            {
                NetworkStream tcpStream = client.GetStream();
                //Отправляем каждый файл в цикле
                foreach (FilesList.FileElement file in RemoteList.FilesList)
                {
                    if (file.Deleted)
                    {
                        int index = LocalList.FilesList.FindIndex(x => x.Path == file.Path && x.Name == file.Name);
                        if (index != -1)
                        {
                            LocalList.FilesList.RemoveAt(index);
                            File.Delete(LocalDir+file.Path);
                            Console.WriteLine("Удалили файл " + file.Path);
                            continue;
                        }
                    }
                    if (file.OldPath != "")
                    {
                        int index = LocalList.FilesList.FindIndex(x => x.Path == file.OldPath && x.Name == file.OldName);
                        if (index != -1)
                        {
                            FilesList.FileElement newFile = new FilesList.FileElement();
                            newFile.Name = file.Name;
                            newFile.Path = file.Path;
                            newFile.LastChange = file.LastChange;
                            newFile.Length = file.Length;
                            LocalList.FilesList[index]= newFile;
                            File.Move(LocalDir + file.Path, LocalDir + newFile.Path);
                            Console.WriteLine("Переименовали файл "+file.Path+" на "+newFile.Path);
                            continue;
                        }
                    }
                    try{
                        FilesSender.SendFile(client, LocalDir,file);
                    }
                    catch (Exception ex){Console.WriteLine("Ошибка при отправке файла " + file.Path + " " + ex.Message); }
                }
                Thread.Sleep(200);
                byte[] endFiles = Encoding.Default.GetBytes("ENDFILES");
                tcpStream.Write(endFiles, 0, endFiles.Length);
                Console.WriteLine("Все файлы успешно отправлены.");

                foreach (FilesList.FolderElement folder in RemoteList.FoldersList)
                {

                    if (!Directory.Exists(LocalDir+folder.Path))
                    {
                        Directory.CreateDirectory(LocalDir + folder.Path);
                    }
                    if (folder.Deleted)
                    {
                        int index = LocalList.FilesList.FindIndex(x => x.Path == folder.Path);
                        int index2 = RemoteList.FilesList.FindIndex(x => x.Path == folder.Path);
                        if (index != -1)
                        {
                            if (Directory.Exists(LocalDir + folder.Path))
                            {
                                Directory.Delete(LocalDir + folder.Path);
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
                        continue;
                    }
                    else
                        if (folder.OldPath != "")
                        {

                            int index = LocalList.FoldersList.FindIndex(x => x.Path == folder.OldPath);
                            int indexRemote = RemoteList.FoldersList.FindIndex(x => x.Path == folder.OldPath);
                            if (index != -1)
                            {

                                if (File.Exists(LocalDir + folder.Path))
                                {
                                    Directory.Move(LocalDir + folder.OldPath, LocalDir + folder.Path);
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
                }

            }
            catch (Exception ex2)
            {
                Console.WriteLine("Ошибка при отправке файлов" + ex2.Message);
            }
        }

        //Рассылка изменений по всем остальным пользователем, кроме текущего
        private static void SendForAllClient(object obj)
        {

            Console.WriteLine(DateTime.Now + " Вошли в функцию отправки обновлений. Поток <{0}>", Thread.CurrentThread.ManagedThreadId.ToString());
            FilesList.FilesInformation updateList = (FilesList.FilesInformation)obj;
            if (updateList.FilesList.Count == 0)
            {

                Console.WriteLine(DateTime.Now + " Обновления не отправлены. Список пуст.");
                return;
            }
            while (blockThread)
            {
                Thread.Sleep(2000);
            }
            blockThread = true;
            for (int i = 0; i < listClients.Count; i++)
            {
                TcpClient currentClient = listClients[i];
                //отправляем локальный список, чтобы клиент прислал разницу
                 SendLocalFileListForUpdate(currentClient, updateList);
                Console.WriteLine(DateTime.Now + " Обновления отправлены {0}-му клиенту.", (i+1).ToString());

            }
            Console.WriteLine(DateTime.Now + " Обновления отправлены всем клиентам успешно.");
            blockThread = false;
        }
    }
}
