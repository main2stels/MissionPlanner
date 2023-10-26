using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MissionPlanner.AirLinkEye.CskyProto
{
    public class CskyLink
    {
        public CskyLink()
        {

        }

        public CskyMessage Read(byte[] data)
        {
            if (data.Length <= 8)
                return null;

            if (data[0] != 7 || data[1] != 7 || data[2] != 7)
                return null;

            var payloadLenght = BitConverter.ToUInt32(data, 4);

            byte[] payload = new byte[payloadLenght];
            Array.Copy(data, 8, payload, 0, payloadLenght);

            return new CskyMessage(payload, (CskyProtocolType)data[3]);
        }
    }
}
