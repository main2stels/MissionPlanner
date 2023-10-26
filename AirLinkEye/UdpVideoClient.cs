using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MissionPlanner.AirLinkEye
{
    internal class UdpVideoClient
    {
        public UdpClient _udpClient;
        private Thread _receiveThread;
        private AirLinkClient _airLinkClient;

        private IPEndPoint _videoClientIp;
        private int _startPort;

        private bool _isListening = true;

        public UdpVideoClient(int port, AirLinkClient airLinkClient, int startPort)
        {
            _udpClient = new UdpClient(port);
            _udpClient.Client.ReceiveBufferSize = 20000;

            _receiveThread = new Thread(new ThreadStart(ReceiveMessage));
            _receiveThread.Start();

            _airLinkClient = airLinkClient;
            _startPort = startPort;
        }

        private void ReceiveMessage()
        {
            IPEndPoint remoteIp = null;
            try
            {
                while (_isListening)
                {
                    byte[] data = null;
                    try
                    {
                        data = _udpClient.Receive(ref remoteIp); // получаем данные

                        if (_videoClientIp == null)
                        {
                            _videoClientIp = remoteIp;
                        }
                    }
                    catch (Exception e)
                    {
                        Thread.Sleep(10);
                    }

                    if (data == null)
                        continue;

                    _airLinkClient.SendRtpToCamera(data);
                    //_logger.Log($"Udp Video Receive!!! Lenght:{data.Length}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public void SendToGS(byte[] data)
        {
            if (_videoClientIp == null)
            {
                try
                {
                    lock (_udpClient)
                        _udpClient.Send(data, data.Length, "localhost", _startPort);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            try
            {
                lock (_udpClient)
                    _udpClient.Send(data, data.Length, _videoClientIp);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public void Dispose()
        {
            _isListening = false;
            _receiveThread?.Abort();
            _udpClient?.Dispose();
        }
    }
}
