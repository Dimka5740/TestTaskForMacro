using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestTaskForMacro
{
    public delegate void NewFrameEventHandler(object sender, Bitmap eventArgs);
    public class VideoReader
    {
        // URL
        private string source;
        // Размер буффера
        private const int bufSize = 1024 * 1024;
        // Размер "отрезка" считываемого из буффера
        private const int readSize = 1024;

        private Thread thread = null;
        private ManualResetEvent stopEvent = null;
        private ManualResetEvent reloadEvent = null;

        private string userAgent = "Mozilla/5.0";

        /// <summary>
        /// Событие для создания нового фрейма
        /// </summary>
        public event NewFrameEventHandler NewFrame;

        public string Source
        {
            get { return source; }
            set
            {
                source = value;
                if (thread != null)
                    reloadEvent.Set();
            }
        }

        /// <summary>
        /// Проверка активности видеопотока
        /// </summary>
        public bool IsRunning
        {
            get
            {
                if (thread != null)
                {
                    if (thread.Join(0) == false)
                        return true;

                    Free();
                }
                return false;
            }
        }

        /// <summary>
        /// Конструктор
        /// </summary>
        public VideoReader() { }

        private int Find(byte[] array, byte[] needle, int startIndex, int sourceLength)
        {
            int needleLen = needle.Length;
            int index;

            while (sourceLength >= needleLen)
            {
                // Поиск первого элемента подмассива
                index = Array.IndexOf(array, needle[0], startIndex, sourceLength - needleLen + 1);

                // Если не нашли, то возврат 
                if (index == -1)
                    return -1;

                int i, p;
                
                for (i = 0, p = index; i < needleLen; i++, p++)
                {
                    if (array[p] != needle[i])
                    {
                        break;
                    }
                }

                if (i == needleLen)
                {
                    return index;
                }

                // Продолжаем искать
                sourceLength -= (index - startIndex + 1);
                startIndex = index + 1;
            }
            return -1;
        }

        public void Start()
        {
            if (!IsRunning)
            {
                if ((source == null) || (source == string.Empty))
                    throw new ArgumentException("Video source is not found.");

                stopEvent = new ManualResetEvent(false);
                reloadEvent = new ManualResetEvent(false);

                thread = new Thread(new ThreadStart(WorkerThread));
                thread.Name = source;
                thread.Start();
            }
        }

        public void WaitForStop()
        {
            if (thread != null)
            {
                thread.Join();

                Free();
            }
        }

        public void Stop()
        {
            if (this.IsRunning)
            {
                stopEvent.Set();
                thread.Abort();
                WaitForStop();
            }
        }

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        private void Free()
        {
            thread = null;

            stopEvent.Close();
            stopEvent = null;
            reloadEvent.Close();
            reloadEvent = null;
        }

        private void WorkerThread()
        {
            // Буффер для хранения данных из потока
            byte[] buffer = new byte[bufSize];
            // Начало jpeg фрейма?
            byte[] jpegStart = new byte[] { 0xFF, 0xD8, 0xFF };
            int jpegStartLength = jpegStart.Length;

            // Необходим для преобразования данных из буффера в изображение
            ASCIIEncoding encoding = new ASCIIEncoding();

            while (!stopEvent.WaitOne(0, false))
            {
                reloadEvent.Reset();
                // Переменная для запроса к серверу
                HttpWebRequest request = null;
                // Переменная для ответа сервера
                WebResponse response = null;
                // Переменная для видеопотока с сервера
                Stream stream = null;
                // Разделитель
                byte[] boundary = null;
                int boundaryLen;
                // Переменные для чтения из буффера
                int read, todo = 0, total = 0, pos = 0;
                int start = 0, stop = 0;
                bool startIsFinded = false;

                try
                {
                    request = (HttpWebRequest)WebRequest.Create(source);
                    
                    if (userAgent != null)
                    {
                        request.UserAgent = userAgent;
                    }
                  
                    response = request.GetResponse();

                    boundary = new byte[] { 0xFF, 0xD8, 0xFF };//encoding.GetBytes("--myboundary");
                    boundaryLen = boundary.Length;

                    stream = response.GetResponseStream();

                    // Пока не вызовется Stop() или не сменится источник Source
                    while ((!stopEvent.WaitOne(0, false)) && (!reloadEvent.WaitOne(0, false)))
                    {
                        if (total > bufSize - readSize)
                        {
                            total = pos = todo = 0;
                        }

                        // Считывание куска из потока
                        if ((read = stream.Read(buffer, total, readSize)) == 0)
                            throw new ApplicationException();

                        total += read;
                        todo += read;

                        // Поиск начала фрейма
                        if ((startIsFinded == false) && (todo >= jpegStartLength))
                        {
                            start = Find(buffer, jpegStart, pos, todo);
                            if (start != -1)
                            {
                                // Найдено
                                pos = start + jpegStartLength;
                                todo = total - pos;
                                startIsFinded = true;
                            }
                            else
                            {
                                todo = jpegStartLength - 1;
                                pos = total - todo;
                            }
                        }

                        // Поиск конца фрейма
                        while ((startIsFinded == true) && (todo != 0) && (todo >= boundaryLen))
                        {
                            stop = Find(buffer, boundary, pos, todo);

                            if (stop != -1)
                            {
                                pos = stop;
                                todo = total - pos;

                                // Создаем изображение и помещаем в параметр события
                                if ((NewFrame != null) && (!stopEvent.WaitOne(0, false)))
                                {
                                    Bitmap bitmap = (Bitmap)Bitmap.FromStream(new MemoryStream(buffer, start, stop - start));
                                    
                                    NewFrame(this, (bitmap));
                                    
                                    bitmap.Dispose();
                                    bitmap = null;
                                }

                                pos = stop + boundaryLen;
                                todo = total - pos;
                                Array.Copy(buffer, pos, buffer, 0, todo);

                                total = todo;
                                pos = 0;
                                startIsFinded = false;
                            }
                            else
                            {
                                if (boundaryLen != 0)
                                {
                                    todo = boundaryLen - 1;
                                    pos = total - todo;
                                }
                                else
                                {
                                    todo = 0;
                                    pos = total;
                                }
                            }
                        }
                    }
                }
                catch (ThreadAbortException)
                {
                    break;
                }
                finally
                {
                    // Закрываем соединения
                    if (request != null)
                    {
                        request.Abort();
                        request = null;
                    }
                    if (stream != null)
                    {
                        stream.Close();
                        stream = null;
                    }
                    if (response != null)
                    {
                        response.Close();
                        response = null;
                    }
                }
            }
        }
    }
}
