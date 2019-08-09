using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Bulbasaur.DataFetcher
{
    class Program
    {
        static void Main(string[] args)
        {
            DownloadImageWithDataflow();

            Console.Write("\nPress Enter to exit ");
            Console.ReadLine();

        }

        static void DownloadImage()
        {
            Boolean rerun = true;
            while (rerun)
            {
                rerun = false;
                String[] directories = Directory.GetDirectories(@"Data");
                for (int i = 0; i < directories.Length; i++)
                {
                    Console.WriteLine("===================================================================");
                    Console.WriteLine("Downloading\t" + i.ToString() + "\t" + directories[i].Replace("Data\\", " ") + "...");
                    String path = Path.Combine(Directory.GetCurrentDirectory(), directories[i]);
                    DirectoryInfo directoryInfo = new DirectoryInfo(path);

                    String[] urls = File.ReadAllLines(Path.Combine(Directory.GetCurrentDirectory(), directories[i], "result.txt"));
                    List<String> failed = urls.ToList();
                    for (int j = 0; j < urls.Length; j++)
                    {
                        try
                        {
                            WebRequest request = HttpWebRequest.Create(urls[j]);
                            var task1 = Task.Factory.FromAsync(request.BeginGetResponse, request.EndGetResponse, null);
                            if (!task1.Wait(TimeSpan.FromSeconds(10)))
                            {
                                throw new TimeoutException();
                            }
                            WebResponse response = task1.Result;
                            Stream stream = response.GetResponseStream();
                            MemoryStream memoryStream = new MemoryStream();

                            var task2 = Task.Run(() =>
                            {
                                stream.CopyTo(memoryStream);
                            });
                            if (!task2.Wait(TimeSpan.FromSeconds(10)))
                            {
                                throw new TimeoutException();
                            }
                            Byte[] buffer = memoryStream.ToArray();
                            String extension = "." + response.ContentType.Split('/')[1];
                            Int32 count = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), directories[i])).Length;
                            File.WriteAllBytes(Path.Combine(path, count.ToString() + extension), buffer);
                            failed.Remove(urls[j]);
                            File.Delete(Path.Combine(Directory.GetCurrentDirectory(), directories[i], "result.txt"));
                            File.WriteAllLines(Path.Combine(Directory.GetCurrentDirectory(), directories[i], "result.txt"), failed);

                            response.Dispose();
                            stream.Dispose();
                            memoryStream.Dispose();


                            Console.WriteLine("[" + DateTime.Now.ToShortTimeString() + "]\t" + i.ToString() + "\t" + directories[i].Replace("Data\\", " ") + "\tSucceed!");
                        }
                        catch (Exception exception)
                        {
                            Console.WriteLine("[" + DateTime.Now.ToShortTimeString() + "]\t" + i.ToString() + "\t" + directories[i].Replace("Data\\", " ") + "\tError!");
                        }
                        finally
                        {
                            rerun = failed.Count != 0;
                        }
                    }


                    Console.WriteLine($"Tried: {urls.Length}\tFailed: {failed.Count}");
                    Console.WriteLine("\n\n");
                }
            }
        }

        static void DownloadImageWithDataflow()
        {
            Boolean rerun = true;
            while (rerun)
            {
                rerun = false;
                String[] allDirectories = Directory.GetDirectories(@"..\Data");
                TransformBlock<int, bool> taskBlock = new TransformBlock<int, bool>((index) =>
                {
                    bool taskResult = false;
                    String[] directories = Directory.GetDirectories(@"..\Data");
                    Console.WriteLine("Downloading\t" + index.ToString() + "\t" + directories[index].Replace("Data\\", " ") + "...");
                    Console.WriteLine("===================================================================");
                    String path = Path.Combine(Directory.GetCurrentDirectory(), directories[index]);
                    DirectoryInfo directoryInfo = new DirectoryInfo(path);

                    String[] urls = File.ReadAllLines(Path.Combine(Directory.GetCurrentDirectory(), directories[index], "result.txt"));
                    List<String> failed = urls.ToList();

                    TransformBlock<string, bool> downloadBlock = new TransformBlock<string, bool>((info) =>
                    {
                        try
                        {
                            string[] infos = info.Split('#');
                            WebRequest request = HttpWebRequest.Create(infos[0]);
                            var task1 = Task.Factory.FromAsync(request.BeginGetResponse, request.EndGetResponse, null);
                            if (!task1.Wait(TimeSpan.FromSeconds(10)))
                            {
                                throw new TimeoutException();
                            }
                            WebResponse response = task1.Result;
                            Stream stream = response.GetResponseStream();
                            MemoryStream memoryStream = new MemoryStream();

                            var task2 = Task.Run(() =>
                            {
                                stream.CopyTo(memoryStream);
                            });
                            if (!task2.Wait(TimeSpan.FromSeconds(10)))
                            {
                                throw new TimeoutException();
                            }
                            Byte[] buffer = memoryStream.ToArray();
                            String extension = "." + response.ContentType.Split('/')[1];
                            Int32 count = Directory.GetFiles(Path.Combine(Directory.GetCurrentDirectory(), infos[1])).Length;
                            File.WriteAllBytes(Path.Combine(infos[1], count.ToString() + extension), buffer);

                            response.Dispose();
                            stream.Dispose();
                            memoryStream.Dispose();


                            return true;
                        }
                        catch (Exception exception)
                        {
                            //Console.ForegroundColor = ConsoleColor.Red;
                            //Console.WriteLine(exception.Message);
                            //Console.ForegroundColor = ConsoleColor.White;
                            return false;
                        }
                    }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 20 });

                    for (int j = 0; j < urls.Length; j++)
                    {
                        downloadBlock.Post(urls[j] + "#" + directories[index]);
                    }

                    downloadBlock.Complete();

                    for (int j = 0; j < urls.Length; j++)
                    {
                        bool result = downloadBlock.Receive();
                        if (result)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss:SUCCEED")}]\t{directories[index]}\tNo.{j}");
                            Console.ForegroundColor = ConsoleColor.White;

                            failed.Remove(urls[j]);
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[{DateTime.Now.ToString("HH:mm:ss")}:FAIL]\t\t{directories[index]}\tNo.{j}");
                            Console.ForegroundColor = ConsoleColor.White;
                        }

                        taskResult = failed.Count != 0 | taskResult;
                        File.Delete(Path.Combine(Directory.GetCurrentDirectory(), directories[index], "result.txt"));
                        File.WriteAllLines(Path.Combine(Directory.GetCurrentDirectory(), directories[index], "result.txt"), failed);
                    }

                    downloadBlock.Completion.Wait();
                    Console.WriteLine($"{index.ToString()}\tTried: {urls.Length}\tFailed: {failed.Count}");
                    Console.WriteLine("\n\n");

                    return taskResult;
                }, new ExecutionDataflowBlockOptions() { MaxDegreeOfParallelism = 5 });

                for (int i = 0; i < allDirectories.Length; i++)
                {
                    taskBlock.Post(i);
                }
                taskBlock.Complete();

                for (int i = 0; i < allDirectories.Length; i++)
                {
                    rerun |= taskBlock.Receive();
                }

                taskBlock.Completion.Wait();
            }
        }

    }
}