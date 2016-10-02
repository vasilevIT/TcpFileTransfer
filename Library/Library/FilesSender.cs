using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace Library
{
    public class FilesSender
    {
        public static void SendFile(TcpClient client,string LocalDir, FilesList.FileElement file)
        {

                 NetworkStream tcpStream = client.GetStream();
                 FileStream fs;
                while(true)
                {
                    try
                    {
                        fs = new FileStream(LocalDir + file.Path, FileMode.Open, FileAccess.Read);
                        break;
                    }
                    catch (Exception)
                    {
                        ;
                    }
                }
                long lengthFile = fs.Length;
                long lastlengthFile = fs.Length;
                int currentByte = 0;
                int tenPersent = (int)(lengthFile / 10);
                int curPersent = tenPersent;
                int count=10;
                int mb=10;//размер файла в МБ, после которого стоит выводить лог передачи
                while (true)
                {
                    byte[] bytes;
                    if (lastlengthFile > client.ReceiveBufferSize)
                    {
                        bytes = new byte[client.ReceiveBufferSize];
                        lastlengthFile -= client.ReceiveBufferSize;
                    }
                    else
                    {
                        bytes = new byte[lastlengthFile];
                    }
                    int readByte = fs.Read(bytes, 0, bytes.Length);
                    if (readByte <= 0)
                        break;
                    tcpStream.Write(bytes, 0, readByte);

                    if ((lengthFile / 1024 / 1024) > mb)//больше 10 МБ
                    {
                        currentByte += readByte;
                        if (currentByte > curPersent)
                        {
                            curPersent += tenPersent;
                            Console.WriteLine(DateTime.Now + " {1} Передано  {0}% поток {2}", new string[] { count.ToString(), file.Path, AppDomain.GetCurrentThreadId().ToString() });
                            count += 10;
                        }
                    }
                }
                fs.Close();
               // Console.WriteLine("Файл " + file.Path + " успешно отправлен.");
                Thread.Sleep(200);

              //  byte[] endFile = Encoding.Default.GetBytes("<---MYENDFILE--->");
               // tcpStream.Write(endFile, 0, endFile.Length);
            
                //подтверждение
            /*
                byte[] bytesAnswer = new byte[client.ReceiveBufferSize];
                tcpStream.Read(bytesAnswer, 0, bytesAnswer.Length);
                string answer = Encoding.Default.GetString(bytesAnswer).Replace("\0","").Trim();
                if (answer == "OKFILE")
                Console.WriteLine("Файл " + file.Path + " успешно отправлен.");
                else
                {
                    Console.WriteLine("Файл " + file.Path + " не отправлен. Ошибка.");
                }
            */
        }


        public static void SaveFile(TcpClient client, string LocalDir, FilesList.FileElement file,bool server = false)
        {
            try
            {
                NetworkStream tcpStream = client.GetStream();

                //записываем файл во временную директорию
                string tempPath;
                if (server)
                {
                    tempPath = LocalDir + @"\" + client.GetHashCode().ToString() + file.Path;
                    FilesList.CreateDir(LocalDir, @"\" + client.GetHashCode().ToString() + file.Path);
                }
                else
                    tempPath = LocalDir+file.Path;
                FilesList.CreateDir(LocalDir, file.Path);
                if (File.Exists(tempPath))
                {
                    if (DateTime.Compare(File.GetLastWriteTime(tempPath),file.LastChange) < 0)
                    {
                        //файл в директории более старый,чем в списке
                        File.Delete(tempPath);
                    }
                    else
                    {

                        //файл в списке более старый,чем в директории
                        long countbyte = file.Length;
                        while (true)
                        {
                            if (countbyte <= 0)
                            {
                                break;
                            }
                            byte[] recievByte;
                            int readByte;
                            if (client.ReceiveBufferSize <= countbyte)
                            {
                                //считываем размер буфера
                                recievByte = new byte[client.ReceiveBufferSize];
                                readByte = tcpStream.Read(recievByte, 0, recievByte.Length);
                               // fs.Write(recievByte, 0, readByte);
                                countbyte -= readByte;
                            }
                            else
                            {
                                //считываем оставшуюся часть файла
                                recievByte = new byte[countbyte];
                                readByte = tcpStream.Read(recievByte, 0, recievByte.Length);
                                countbyte -= readByte;
                               // fs.Write(recievByte, 0, readByte);
                            }
                        }
                        Console.WriteLine("Файл " + file.Path + " не закачен. Потому-что он уже существует и он более свежий.");
                        return;


                    }
                }
                using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
                {
                    long countbyte = file.Length;
                    while (true)
                    {
                        if (countbyte <= 0)
                        {
                            break;
                        }
                        byte[] recievByte;
                        int readByte;
                        if (client.ReceiveBufferSize <= countbyte)
                        {
                            //считываем размер буфера
                            recievByte = new byte[client.ReceiveBufferSize];
                            readByte = tcpStream.Read(recievByte, 0, recievByte.Length);
                            fs.Write(recievByte, 0, readByte);
                            countbyte -= readByte;
                        }
                        else
                        {
                            //считываем оставшуюся часть файла
                            recievByte = new byte[countbyte];
                            readByte = tcpStream.Read(recievByte, 0, recievByte.Length);
                            countbyte -= readByte;
                            fs.Write(recievByte, 0, readByte);
                        }
                    }

                   // Console.WriteLine("Файл " + file.Path + " успешно закачен. размер " + countbyte + " байт." + file.Length.ToString());
                }
            }
            catch (Exception ex2)
            {
                Console.WriteLine("Ошибка при отправке файлов" + ex2.Message);
            }
        }
        public static void CheckFiles(string LocalDir, FilesList.FilesInformation LocalList, FilesList.FilesInformation RemoteList)
        {
            //изменение и удаление некоторых элементов локального списка
            List<FilesList.FileElement> tempList = RemoteList.FilesList.FindAll(x => x.OldPath != "" || x.Deleted);
            int i = 0;
            while (i < tempList.Count)//foreach(FilesList.FileElement file in )
            {
                FilesList.FileElement file = tempList[i];
                if (file.Deleted)
                {
                    int index = LocalList.FilesList.FindIndex(x => x.Path == file.Path && x.Name == file.Name);
                    if (index != -1)
                    {
                        if (File.Exists(LocalDir + file.Path))
                        {
                            File.Delete(LocalDir + file.Path);
                            Console.WriteLine(DateTime.Now + " Удалили файл " + file.Path);
                        }
                        else
                        {

                            Console.WriteLine(DateTime.Now + " Не удалось удалить файл " + file.Path + " . Он уже удален.");
                        }
                        LocalList.FilesList.RemoveAt(index);

                    }

                    RemoteList.FilesList.Remove(file);
                    tempList.RemoveAt(i);
                    continue;
                }
                if (file.OldPath != "")
                {

                    int index = LocalList.FilesList.FindIndex(x => x.Path == file.OldPath);
                    int indexRemote = RemoteList.FilesList.FindIndex(x => x.Path == file.OldPath);
                    if (index != -1)
                    {

                        if (File.Exists(LocalDir + file.Path))
                        {
                            File.Move(LocalDir + file.OldPath, LocalDir + file.Path);
                            FilesList.FileElement newFile = new FilesList.FileElement();
                            newFile.Name = file.Name;
                            newFile.Path = file.Path;
                            newFile.LastChange = file.LastChange;
                            newFile.Length = file.Length;
                            LocalList.FilesList[index] = newFile;
                            if (indexRemote != -1)
                            {
                                RemoteList.FilesList[indexRemote] = newFile;
                            }
                            Console.WriteLine(DateTime.Now + " Переименовали файл " + file.OldPath + " на " + file.Path);
                            continue;
                        }
                        else
                        {

                            Console.WriteLine(DateTime.Now + " Не удалось переименовать файл " + file.OldPath + " на " + file.Path + " . Файл удален.");
                        }
                    }
                    else
                    {

                        Console.WriteLine(DateTime.Now + " Не удалось переименовать файл " + file.OldPath + " на " + file.Path + " . Файл уже переименован.");
                    }
                }
                i++;
            }
        }
        public static MemoryStream GetRemoteList(TcpClient client)
        {
            
            NetworkStream tcpStream = client.GetStream();
             MemoryStream stream = new MemoryStream();
            //FilesList.FilesInformation RemoteList = new FilesList.FilesInformation();
            int LengthList = 0;//длина списка в байтах
            while (true)
            {
                byte[] recievByte = new byte[client.ReceiveBufferSize];
                int readByte = tcpStream.Read(recievByte, 0, client.ReceiveBufferSize);
                string answer = Encoding.Default.GetString(recievByte).Replace("\0", "");//.Trim();
                int indexEnd = answer.IndexOf("ENDLIST", 0, answer.Length);
                if (indexEnd != (-1))
                {
                    stream.Write(recievByte,0,indexEnd);
                    break;
                }
                stream.Write(recievByte, 0, readByte);
                LengthList += readByte;
            }
            stream.Position = 0;
            return stream;
        }
    }
}
