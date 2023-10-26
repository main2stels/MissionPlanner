using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MissionPlanner.AirLinkEye.CskyProto
{
    public class CskyMessage
    {
        public byte[] Payload { get; private set; }
        public CskyProtocolType ProtocolType { get; private set; }

        public CskyMessage(byte[] payload, CskyProtocolType protocol)
        {
            Payload = payload;
            ProtocolType = protocol;
        }

        public byte[] ToCskyByteArray()
        {
            var result = new byte[Payload.Length + 8];
            Array.Copy(Payload, 0, result, 8, Payload.Length);
            result[0] = 7;
            result[1] = 7;
            result[2] = 7;
            result[3] = (byte)ProtocolType;

            var lenght = BitConverter.GetBytes(Payload.Length);

            Array.Copy(lenght, 0, result, 4, lenght.Length);

            return result;
        }
    }
}
