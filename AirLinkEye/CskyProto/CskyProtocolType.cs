using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MissionPlanner.AirLinkEye.CskyProto
{
    public enum CskyProtocolType : byte
    {
        None = 0,
        Mavlink = 1,
        Rtcp = 2,
        Rtp = 3,
    }
}
