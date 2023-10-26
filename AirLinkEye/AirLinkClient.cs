using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static MAVLink;
using MissionPlanner.AirLinkEye.CskyProto;
using BruTile.Wmts.Generated;
using static OpenTK.Graphics.OpenGL.GL;
using MissionPlanner.Utilities;

namespace MissionPlanner.AirLinkEye
{
    internal class AirLinkClient : IDisposable
    {
        private bool isEyeVirsion = false;

        public IPEndPoint ServerIp;

        private UdpClient _udpServerClient;
        private MavlinkParse _mavParse = new MavlinkParse();
        //private string _airlinkHostName = "airlink.biruch.ru";
        private string _airlinkHostName = "air-link.space";
        private int _airlinkServerPort = 10000;
        private static int _port = 14850;

        //private Thread _listener;
        private bool _readPort = true;

        private Thread _receiveServerThread;

        private Action<byte[]> _receiveAction;
        private Action<byte[]> _receiveVideoAction;
        private Action<byte[]> _receiveRtpVideoAction;

        //private MavReader _mavReader;

        //private Dictionary<uint, Action<MAVLinkMessage>> _mavActions;

        private IPEndPoint _hawkIP;
        private IPEndPoint _missionPlannerIp;

        private Thread _hpHawkSending;


        private bool _isHawkHP = true;
        private bool _threadSendingHPRequestToServer = true;

        private bool _isGStreamerStarted = false;

        private AIRLINK_EYE_HOLE_PUSH_TYPE _hpType = AIRLINK_EYE_HOLE_PUSH_TYPE.NOT_PENETRATED;
        private AIRLINK_EYE_HOLE_PUSH_TYPE _hawkhpType = AIRLINK_EYE_HOLE_PUSH_TYPE.NOT_PENETRATED;

        private CskyLink _cskyLink = new CskyLink();

        public AirLinkClient(Action<byte[]> receiveAction, bool isEyeViersion, Action<byte[]> receiveVideoAction, Action<byte[]> receiveRtpVideoAction, IPEndPoint missionPlannerIp)
        {
            isEyeVirsion = isEyeViersion;
            _missionPlannerIp = missionPlannerIp;

            IPAddress[] addresslist = Dns.GetHostAddresses(_airlinkHostName);

            foreach (IPAddress theaddress in addresslist)
            {
                Console.WriteLine(theaddress.ToString());
                ServerIp = new IPEndPoint(theaddress, _airlinkServerPort);
            }

            _receiveAction = receiveAction;

            //_mavActions = new Dictionary<uint, Action<MAVLinkMessage>>()
            //{
            //    { (uint)MAVLINK_MSG_ID.AIRLINK_EYE_GS_HOLE_PUSH_RESPONSE, HolePushResponse },
            //    { (uint)MAVLINK_MSG_ID.AIRLINK_EYE_HP, HPHawkReceiive }
            //};

            //_mavReader = new MavReader(_mavActions.Keys.ToArray());

            //_listener = new Thread(new ThreadStart(Listen));
            //_listener.Start();
            _receiveVideoAction = receiveVideoAction;
            _receiveRtpVideoAction = receiveRtpVideoAction;
        }

        public void Connect()
        {
            _udpServerClient = new UdpClient(_port);
            _udpServerClient.Client.ReceiveBufferSize = 20000;

            _receiveServerThread = new Thread(new ThreadStart(ReceiveMessage));
            _receiveServerThread.Start();

            _threadSendingHPRequestToServer = true;
        }

        #region Listen Mavlink
        //private void Listen()
        //{
        //    var mav = new MavlinkParse();

        //    while (_readPort)
        //    {
        //        MAVLinkMessage message = null;
        //        try
        //        {
        //            //message = mav.ReadPacket(_stream);
        //            message = _mavReader.ReadPacket();
        //        }
        //        catch (TimeoutException e)
        //        {
        //            //_logger.SetErrorLog(e);
        //            Console.WriteLine($"{e.Message} {e.StackTrace}");
        //            Thread.Sleep(1000);
        //        }
        //        catch (Exception e)
        //        {
        //            Console.WriteLine($"{e.Message} {e.StackTrace}");
        //        }

        //        if (message != null)
        //        {
        //            MAVLINK_MSG_ID msgId = (MAVLINK_MSG_ID)message.msgid;

        //            if (_mavActions.ContainsKey(message.msgid))
        //            {
        //                try
        //                {
        //                    _mavActions[message.msgid].Invoke(message);
        //                }
        //                catch (Exception e)
        //                {
        //                    if (e == null)
        //                    {
        //                        Console.WriteLine("exception is null");
        //                    }
        //                    else if (e.StackTrace == null)
        //                    {
        //                        Console.WriteLine("trace null");
        //                    }

        //                    try
        //                    {
        //                        Console.WriteLine(e);
        //                    }
        //                    catch (Exception ex)
        //                    {
        //                        Console.WriteLine(ex.Message);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //}
        #endregion

        private void ReceiveMessage()
        {
            IPEndPoint remoteIp = null;
            try
            {
                while (true)
                {
                    byte[] data = null;
                    try
                    {
                        data = _udpServerClient.Receive(ref remoteIp); // получаем данные
                    }
                    catch (System.ObjectDisposedException e)
                    {
                        return;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Ошибка при получении");
                        Console.WriteLine(e.StackTrace);
                        Thread.Sleep(10);
                    }

                    if (data == null)
                        continue;

                    if(remoteIp.Equals(_missionPlannerIp))
                    {
                        Send(data, ServerIp);
                        continue;
                    }

                    if (_hawkIP != null)
                    {
                        if (remoteIp.Equals(_hawkIP))
                        {
                            var msg = _cskyLink.Read(data);

                            if (msg == null)
                            {
                                continue;
                            }

                            try
                            {

                                switch (msg.ProtocolType)
                                {
                                    case CskyProtocolType.Rtcp:
                                        _receiveVideoAction.Invoke(msg.Payload);
                                        break;
                                    case CskyProtocolType.Mavlink:
                                        //_mavReader.Write(msg.Payload);
                                        _receiveAction.Invoke(msg.Payload);
                                        break;
                                    case CskyProtocolType.Rtp:
                                        _receiveRtpVideoAction.Invoke(msg.Payload);
                                        break;
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e);
                            }

                        }
                        else
                        {
                            _receiveAction.Invoke(data);
                            //_mavReader.Write(data);
                        }
                    }
                    else
                    {
                        _receiveAction.Invoke(data);
                        //_mavReader.Write(data);
                    }


                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
        }

        public void Send(MAVLINK_MSG_ID id, object data, IPEndPoint ip = null)
        {
            if (ip == null)
                ip = ServerIp;

            var packet = _mavParse.GenerateMAVLinkPacket20(id, data);
            var msg = new MAVLinkMessage(packet);

            if (_hawkIP != null)
            {
                if (ip.Equals(_hawkIP))
                {
                    var cskyMsg = new CskyMessage(msg.buffer, CskyProtocolType.Mavlink).ToCskyByteArray();
                    _udpServerClient.Send(cskyMsg, cskyMsg.Length, ip);
                    return;
                }
            }

            _udpServerClient.Send(msg.buffer, msg.Length, ip);
        }

        public void Send(byte[] data, IPEndPoint ip = null)
        {
            if (ip == null)
                ip = ServerIp;
            _udpServerClient.Send(data, data.Length, ip);
        }

        public void SendRtcpToCamera(byte[] data)
        {
            if (_hawkIP != null)
            {
                var cskyMsg = new CskyMessage(data, CskyProtocolType.Rtcp).ToCskyByteArray();
                Send(cskyMsg, _hawkIP);
            }
        }

        public void SendRtpToCamera(byte[] data)
        {
            if (_hawkIP != null)
            {
                var cskyMsg = new CskyMessage(data, CskyProtocolType.Rtp).ToCskyByteArray();
                Send(cskyMsg, _hawkIP);
            }
        }

        public void Disconnect()
        {
            _udpServerClient.Dispose();
            _threadSendingHPRequestToServer = false;
            _hawkIP = null;
            _hpHawkSending?.Abort();
            _hpHawkSending = null;
            _isGStreamerStarted = false;
            _hpType = AIRLINK_EYE_HOLE_PUSH_TYPE.NOT_PENETRATED;
            _hawkhpType = AIRLINK_EYE_HOLE_PUSH_TYPE.NOT_PENETRATED;
        }

        public void Dispose()
        {
            _receiveServerThread?.Abort();
            _udpServerClient.Dispose();
            _readPort = false;
        }


        public void EyeConnected()
        {
            if (!isEyeVirsion)
                return;

            _sendingEyeConnected = true;

            var eyeConnectedThread = new Thread(new ThreadStart(SendEyeConnected));
            eyeConnectedThread.Start();
        }

        public void MavlinkOnOnPacketReceived(object o, MAVLink.MAVLinkMessage linkMessage)
        {
            switch (linkMessage.msgid)
            {
                case (uint)MAVLink.MAVLINK_MSG_ID.AIRLINK_EYE_GS_HOLE_PUSH_RESPONSE:
                    HolePushResponse(linkMessage);
                    break;
                case (uint)MAVLink.MAVLINK_MSG_ID.AIRLINK_EYE_HP:
                    HPHawkReceiive(linkMessage);
                    break;
            }
        }

        private bool _sendingEyeConnected = false;
        private void SendEyeConnected()
        {
            while (_threadSendingHPRequestToServer)
            {
                Send(MAVLINK_MSG_ID.AIRLINK_EYE_GS_HOLE_PUSH_REQUEST, new mavlink_airlink_eye_gs_hole_push_request_t());

                if(_sendingEyeConnected)
                {
                    Thread.Sleep(500);
                }
                else
                {
                    Thread.Sleep(3000);
                }
            }
        }

        private void HPHawk()
        {
            while (_isHawkHP)
            {
                if (_hawkIP != null)
                {
                    Send(MAVLINK_MSG_ID.AIRLINK_EYE_HP, new mavlink_airlink_eye_hp_t((byte)_hpType), _hawkIP);
                }

                if (_hpType == AIRLINK_EYE_HOLE_PUSH_TYPE.BROKEN && _hawkhpType == AIRLINK_EYE_HOLE_PUSH_TYPE.BROKEN)
                {
                    if(!_isGStreamerStarted)
                    {
                        _isGStreamerStarted = true;

                        StartGStreamer();
                    }
                    Thread.Sleep(10000);
                }
                else
                    Thread.Sleep(500);
            }
        }

        private void HolePushResponse(MAVLinkMessage msg)
        {
            var resp = msg.ToStructure<mavlink_airlink_eye_gs_hole_push_response_t>();

            if ((AIRLINK_EYE_GS_HOLE_PUSH_RESP_TYPE)resp.resp_type == AIRLINK_EYE_GS_HOLE_PUSH_RESP_TYPE.PARTNER_NOT_READY)
            {
                Console.WriteLine("Partner not ready");
                return;
            }

            var ip = new IPAddress(resp.ip_address_4);
            var ipEndPoint = new IPEndPoint(ip, (int)resp.ip_port);

            if (_hawkIP == null)
            {
                _sendingEyeConnected = false;

                Console.WriteLine($"partner ip: {ip} port: {resp.ip_port}");
                _hawkIP = ipEndPoint;

                if (_hpHawkSending == null)
                {
                    _hpHawkSending = new Thread(new ThreadStart(HPHawk));
                    _hpHawkSending.Start();
                }

                return;
            }

            if(!_hawkIP.Equals(ipEndPoint))
            {
                _hpType = AIRLINK_EYE_HOLE_PUSH_TYPE.NOT_PENETRATED;
                _hawkhpType = AIRLINK_EYE_HOLE_PUSH_TYPE.NOT_PENETRATED;
                _sendingEyeConnected = true;
                _hawkIP = ipEndPoint;
            }


        }

        private void HPHawkReceiive(MAVLinkMessage msg)
        {
            var hp = msg.ToStructure<mavlink_airlink_eye_hp_t>();

            _hpType = AIRLINK_EYE_HOLE_PUSH_TYPE.BROKEN;
            _hawkhpType = (AIRLINK_EYE_HOLE_PUSH_TYPE)hp.resp_type;

            Console.WriteLine($"HP receive; HAWK HP TYPE: {_hawkhpType}");
        }

        private void StartGStreamer()
        {
            GStreamer.StopAll();

            //string ipaddr = "192.168.43.1";

            //if (Settings.Instance["herelinkip"] != null)
            //    ipaddr = Settings.Instance["herelinkip"].ToString();

            //InputBox.Show("herelink ip", "Enter herelink ip address", ref ipaddr);

            //Settings.Instance["herelinkip"] = ipaddr;

            //string url = "rtspsrc location=rtsp://localhost:554/main.264 latency=100 ! queue ! rtph264depay ! h264parse ! avdec_h264 ! videoconvert ! videoscale ! video/x-raw,width=1280,height=720 ! autovideosink";
            string url = "rtspsrc location=rtsp://admin:qwerty00@localhost:554/Streaming/Channels/101 latency=100 ! queue ! rtph264depay ! h264parse ! avdec_h264 ! videoconvert ! videoscale ! video/x-raw,width=1280,height=720 ! autovideosink";
            //string url = "rtspsrc location=rtsp://localhost:554/main.264 latency=0 ! queue ! application/x-rtp ! rtph265depay ! avdec_h265 ! videoconvert ! video/x-raw,format=BGRA ! appsink name=outsink";
            GStreamer.gstlaunch = GStreamer.LookForGstreamer();

            if (!GStreamer.gstlaunchexists)
            {
                GStreamerUI.DownloadGStreamer();

                if (!GStreamer.gstlaunchexists)
                {
                    return;
                }
            }

            GStreamer.StartA(url);
        }
    }
}
