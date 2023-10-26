using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static MAVLink;

namespace MissionPlanner.AirLinkEye
{
    public class MavReader
    {
        private EventWaitHandle _waitHandle = new AutoResetEvent(false);
        private bool isWait = false;


        private Dictionary<uint, object> _readingIds;

        private Queue<byte[]> _buffer = new Queue<byte[]>();

        private int _packetCount = 0;
        private int _packetCurrent = 0;

        public MavReader(uint[] readingIds)
        {
            _readingIds = readingIds.ToDictionary(x => x, x => (object)x);
        }

        public void Write(byte[] msg)
        {
            lock (_buffer)
            {
                _buffer.Enqueue(msg);
            }

            if (isWait)
            {
                isWait = false;
                //_waitHandle.Set();
            }
        }


        public MAVLinkMessage ReadPacket()
        {
            bool queueIsEmpty = false;

            lock (_buffer)
            {
                queueIsEmpty = _buffer.Count == 0;
            }

            if (queueIsEmpty)
            {
                isWait = true;
                //_waitHandle.WaitOne();
                while (isWait)
                {
                    Thread.Sleep(50);
                }

                return null;
            }

            DateTime packettime = DateTime.MinValue;

            byte[] buffer;

            lock (_buffer)
            {
                buffer = _buffer.Dequeue();
            }

            int sp = 0;

            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] == MAVLink.MAVLINK_STX || buffer[i] == MAVLINK_STX_MAVLINK1)
                {
                    sp = i;
                    break;
                }
                else if (i == buffer.Length - 1)
                {
                    return null;
                }
            }

            var headerlength = buffer[sp] == MAVLINK_STX ? MAVLINK_CORE_HEADER_LEN : MAVLINK_CORE_HEADER_MAVLINK1_LEN;
            var headerlengthstx = headerlength + 1;

            // packet length
            int lengthtoread = 0;

            if (buffer.Length < headerlengthstx)
                return null;

            if (buffer[sp] == MAVLINK_STX)
            {
                lengthtoread = buffer[sp + 1] + headerlengthstx + 2; // data + header + checksum - magic - length
                if ((buffer[sp + 2] & MAVLINK_IFLAG_SIGNED) > 0)
                {
                    lengthtoread += MAVLINK_SIGNATURE_BLOCK_LEN;
                }
            }
            else
            {
                lengthtoread = buffer[sp + 1] + headerlengthstx + 2; // data + header + checksum - U - length    
            }

            if (lengthtoread > (buffer.Length - sp))
                return null;

            //проверить нужно ли вообще копирование? возможно там всегда такая длинна
            byte[] result;
            //if (lengthtoread == buffer.Length - 1)
            //{
            //    byte l = buffer[buffer.Length - 1];
            //    Console.WriteLine("Ups");
            //}

            if (lengthtoread == buffer.Length)
            {
                result = buffer;
            }
            else
            {
                result = new byte[lengthtoread];
                Array.Copy(buffer, sp, result, 0, lengthtoread);

                //todo: сделать перенос в начало очереди остатка
                if ((buffer.Length - result.Length) >= headerlengthstx)
                {
                    byte[] nextMsg = new byte[buffer.Length - result.Length];
                    try
                    {
                        Array.Copy(buffer, sp + lengthtoread, nextMsg, 0, buffer.Length - (result.Length + sp));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"{e.Message} {e.StackTrace}");
                        return null;
                    }


                    lock (_buffer)
                    {
                        _buffer.Enqueue(nextMsg);
                    }
                }
            }

            MAVLinkMessage message = new MAVLinkMessage(result, packettime);

            // calc crc
            ushort crc = MavlinkCRC.crc_calculate(result, result.Length - 2);

            // calc extra bit of crc for mavlink 1.0+
            if (message.header == MAVLINK_STX || message.header == MAVLINK_STX_MAVLINK1)
            {
                crc = MavlinkCRC.crc_accumulate(MAVLINK_MESSAGE_INFOS.GetMessageInfo(message.msgid).crc, crc);
            }

            // check crc
            if ((message.crc16 >> 8) != (crc >> 8) ||
                      (message.crc16 & 0xff) != (crc & 0xff))
            {
                // crc fail
                return null;
            }

            return message;
        }
    }
}
