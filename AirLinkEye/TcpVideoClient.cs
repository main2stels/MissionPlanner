using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.UI;

namespace MissionPlanner.AirLinkEye
{
    internal class TcpVideoClient
    {
        TcpListener tcpListener = new TcpListener(IPAddress.Any, 554); // сервер для прослушивания
        List<ClientVideoObject> clients = new List<ClientVideoObject>(); // все подключения
        private Action<byte[]> _sendToCameraAction;

        private Thread _listenThread;
        private AirLinkClient _airLinkClient;

        private UdpVideoClient _videoClient;
        private bool _isListenThread = true;
        public bool _isDisposed = false;

        public TcpVideoClient(Action<byte[]> sendToCameraAction, AirLinkClient airLinkClient)
        {
            _sendToCameraAction = sendToCameraAction;
            _airLinkClient = airLinkClient;
        }

        protected internal void RemoveConnection(string id)
        {
            // получаем по id закрытое подключение
            ClientVideoObject client = clients.FirstOrDefault(c => c.Id == id);
            // и удаляем его из списка подключений
            if (client != null) clients.Remove(client);
            client?.Close();
        }
        // прослушивание входящих подключений
        public void Start()
        {
            _listenThread = new Thread(new ThreadStart(Listen));
            _listenThread.Start();
            _isListenThread = true;
        }

        public void ReStart()
        {
            _isListenThread = true;
            _listenThread?.Abort();
            tcpListener.Stop();

            Thread.Sleep(30);
            tcpListener = new TcpListener(IPAddress.Any, 554);
            Thread.Sleep(30);
            clients = new List<ClientVideoObject>();
            _listenThread = new Thread(new ThreadStart(Listen));
            _listenThread.Start();
        }

        private void Listen()
        {
            try
            {
                tcpListener.Start();
                Console.WriteLine("Сервер запущен. Ожидание подключений...");

                while (_isListenThread)
                {
                    TcpClient tcpClient = tcpListener.AcceptTcpClient();

                    ClientVideoObject clientObject = new ClientVideoObject(tcpClient, this);

                    var cl = clients.ToArray();
                    foreach(var c in cl)
                    {
                        RemoveConnection(c.Id);
                    }
                    clients.Add(clientObject);
                    Task.Run(clientObject.ProcessAsync);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                if (!_isDisposed)
                {
                    ReStart();
                }
                else
                {
                    Disconnect();
                }
                //
            }
        }

        // трансляция сообщения подключенным клиентам
        public void BroadcastMessageAsync(byte[] message/*, string id*/)
        {
            var str = System.Text.Encoding.Default.GetString(message);

            if (str.Contains("Transport:"))
            {
                var pattern = @"server_port=([0-9]+)";
                var match = Regex.Match(str, pattern);

                var g1 = match.Groups[1].Value;
                var port1 = int.Parse(g1);

                var patternClient = @"client_port=([0-9]+)";
                var matchClient = Regex.Match(str, patternClient);

                var gc1 = matchClient.Groups[1].Value;
                var portClient = int.Parse(gc1);

                if (_videoClient == null)
                {
                    _videoClient = new UdpVideoClient(port1, _airLinkClient, portClient);
                }
                else
                {
                    try
                    {
                        _videoClient.Dispose();
                    }
                    catch (Exception ex )
                    {
                        Console.WriteLine(ex.Message);
                    }
                    _videoClient = new UdpVideoClient(port1, _airLinkClient, portClient);
                }
            }

            foreach (var client in clients)
            {
                //client.Writer.Write(message); //передача данных
                //client.Writer.Flush();
                client.Stream.Write(message, 0, message.Length);
                /*if (client.Id != id) // если id клиента не равно id отправителя
                {
                    client.Writer.Write(message); //передача данных
                    client.Writer.Flush();
                }*/
            }
        }

        public void BroadcastRtp(byte[] message)
        {
            if (_videoClient != null)
            {
                _videoClient.SendToGS(message);
            }
        }

        public void MessageFromGS(byte[] message)
        {
            _sendToCameraAction.Invoke(message);
        }

        // отключение всех клиентов
        protected internal void Disconnect()
        {
            foreach (var client in clients)
            {
                client.Close(); //отключение клиента
            }
            tcpListener.Stop(); //остановка сервера
            _isListenThread = false;
            _listenThread?.Abort();
        }

        protected internal void Dispose()
        {
            Disconnect();
            _isDisposed = true;

            _videoClient?.Dispose();
        }
    }
}
