using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;

namespace MissionPlanner.Comms
{
    public class AirLinkEYEUdpSerial : UdpSerial
    {
        private static int _startPort = 14550;
        private string _airLinkLogin = "";
        private Func<byte[], byte[], byte[]> _airLinkLoginSendFunc;
        private Action _eyeConnectionStart;
        private Action _eyeConnectionClose;
        private string _hostName = "air-link.space";
        private int _hostPort = 10000;

        public override string PortName
        {
            get => _airLinkLogin + " EYE" + ":" + Port;
            set { }
        }

        public AirLinkEYEUdpSerial(Func<byte[], byte[], byte[]> airLinkLoginSendFunc, Action eyeConnectionStart, Action eyeConnectionClose)
        {
            _airLinkLoginSendFunc = airLinkLoginSendFunc;
            _eyeConnectionStart = eyeConnectionStart;
            _eyeConnectionClose = eyeConnectionClose;
        }

        public AirLinkEYEUdpSerial(UdpClient client, Func<byte[], byte[], byte[]> airLinkLoginSendFunc) : base(client)
        {
            _airLinkLoginSendFunc = airLinkLoginSendFunc;
        }

        public override void Open()
        {
            if (client.Client.Connected || IsOpen)
            {
                log.Info("UDPSerial socket already open");
                return;
            }

            client.Close();

            var dest = Port;

            dest = OnSettings("UDP_port" + ConfigRef, dest);

            var airlinkLogin = "";
            var airlinkPas = "";

            if (inputboxreturn.Cancel == OnInputBoxShow("AIR-LINK connect",
                                    "Enter Login", ref airlinkLogin)) return;

            if (inputboxreturn.Cancel == OnInputBoxShow("AIR-LINK connect",
                                "Enter Password", ref airlinkPas)) return;

            _airLinkLogin = airlinkLogin;

            Port = (++_startPort).ToString();

            OnSettings("UDP_port" + ConfigRef, Port, true);

            //######################################

            try
            {
                if (client != null) client.Close();
            }
            catch
            {
            }

            client = new UdpClient(int.Parse(Port));

            var login = Encoding.UTF8.GetBytes(airlinkLogin);
            var pass = Encoding.UTF8.GetBytes(airlinkPas);

            var msg = _airLinkLoginSendFunc.Invoke(login, pass);

            while (true)
            {
                client.Send(msg, msg.Length, _hostName, _hostPort);
                Thread.Sleep(1500);

                if (CancelConnect)
                {
                    try
                    {
                        client.Close();
                    }
                    catch
                    {
                    }

                    return;
                }

                if (BytesToRead > 0)
                    break;
            }

            if (BytesToRead == 0)
                return;

            try
            {
                // reset any previous connection
                RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

                client.Receive(ref RemoteIpEndPoint);
                log.InfoFormat("UDPSerial connecting to {0} : {1}", RemoteIpEndPoint.Address, RemoteIpEndPoint.Port);
                EndPointList.Add(RemoteIpEndPoint);
                _isopen = true;
                _eyeConnectionStart.Invoke();
            }
            catch (Exception ex)
            {
                if (client != null && client.Client.Connected) client.Close();
                log.Info(ex.ToString());
                //CustomMessageBox.Show("Please check your Firewall settings\nPlease try running this command\n1.    Run the following command in an elevated command prompt to disable Windows Firewall temporarily:\n    \nNetsh advfirewall set allprofiles state off\n    \nNote: This is just for test; please turn it back on with the command 'Netsh advfirewall set allprofiles state on'.\n", "Error");
                throw new Exception("The socket/UDPSerial is closed " + ex);
            }
        }

        public new void Close()
        {
            _eyeConnectionClose.Invoke();
            base.Close();
        }
    }
}
