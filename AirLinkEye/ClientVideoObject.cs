using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MissionPlanner.AirLinkEye
{
    internal class ClientVideoObject
    {
        protected internal string Id { get; } = Guid.NewGuid().ToString();
        protected internal StreamWriter Writer { get; }
        protected internal StreamReader Reader { get; }

        TcpClient client;
        TcpVideoClient server; // объект сервера
        public NetworkStream Stream;

        public ClientVideoObject(TcpClient tcpClient, TcpVideoClient serverObject)
        {
            client = tcpClient;
            server = serverObject;
            // получаем NetworkStream для взаимодействия с сервером
            Stream = client.GetStream();
            // создаем StreamReader для чтения данных
            Reader = new StreamReader(Stream);
            // создаем StreamWriter для отправки данных
            Writer = new StreamWriter(Stream);
        }

        public async Task ProcessAsync()
        {
            int receivedZeroCount = 0;
            try
            {
                while (true)
                {
                    try
                    {
                        var buffer = new byte[7024];

                        int received = await Stream.ReadAsync(buffer, 0, 7024);

                        if (received > buffer.Length)
                        {
                            Console.WriteLine("error!!!!!");
                        }


                        //todo: exceptio, received ==0 RECOONECTED
                        if (received == 0)
                        {
                            Thread.Sleep(50);
                            receivedZeroCount++;

                            if(receivedZeroCount >= 4)
                            {
                                break;
                            }

                            continue;
                        } 
                            
                            

                        if (received == -1)
                            continue;


                        byte[] data = new byte[received];
                        
                        

                        Array.Copy(buffer, 0, data, 0, received);

                        
                       /* var str = System.Text.Encoding.Default.GetString(data);
                        if (str.Contains("554"))
                        {
                            str = str.Replace("554", "8554");
                            data = Encoding.Default.GetBytes(str);
                        }*/
                      

                        server.MessageFromGS(data);

                        //message = $"{userName}: {message}";
                        //Console.WriteLine(message);
                        //await server.BroadcastMessageAsync(message, Id);
                    }
                    catch (Exception e)
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                // в случае выхода из цикла закрываем ресурсы
                server.RemoveConnection(Id);
                try
                {
                    Close();
                }
                catch(Exception e)
                {
                    Console.WriteLine(e);
                }

                if (!server._isDisposed)
                {
                    //server.ReStart();
                }

            }
        }
        // закрытие подключения
        protected internal void Close()
        {
            Writer.Close();
            Reader.Close();
            client.Close();
        }
    }
}
