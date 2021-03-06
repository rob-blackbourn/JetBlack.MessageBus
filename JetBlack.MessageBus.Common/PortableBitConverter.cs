﻿using System;

namespace JetBlack.MessageBus.Common
{
    public static class PortableBitConverter
    {
        private static readonly DateTime EPOCH = new DateTime(1970, 1, 1, 0, 0, 0, 0);

        public static DateTime Int64ToDate(long timestamp)
        {
            return EPOCH.AddMilliseconds(timestamp);
        }

        public static long DateToInt64(DateTime date)
        {
            var diff = date - EPOCH;
            return (long)Math.Floor(diff.TotalMilliseconds);
        }

        public static DateTime ToDateTime(byte[] buf, int offset)
        {
            var timestamp = ToInt64(buf, offset);
            return Int64ToDate(timestamp);
        }

        public static byte[] GetBytes(DateTime value)
        {
            var timestamp = DateToInt64(value);
            return GetBytes(timestamp);
        }

        public static short ToInt16(byte[] buf, int offset)
        {
            return (short)((buf[offset] << 8) + (buf[offset + 1] << 0));
        }

        public static int ToInt32(byte[] buf, int offset)
        {
            return
                (buf[offset] << 24) +
                (buf[offset + 1] << 16) +
                (buf[offset + 2] << 8) +
                (buf[offset + 3] << 0);
        }

        public static long ToInt64(byte[] buf, int offset)
        {
            return (((long)buf[offset] << 56) +
                    ((long)(buf[offset + 1] & 255) << 48) +
                    ((long)(buf[offset + 2] & 255) << 40) +
                    ((long)(buf[offset + 3] & 255) << 32) +
                    ((long)(buf[offset + 4] & 255) << 24) +
                    ((buf[offset + 5] & 255) << 16) +
                    ((buf[offset + 6] & 255) << 8) +
                    ((buf[offset + 7] & 255) << 0));
        }

        public static float ToFloat(byte[] buf, int offset)
        {
            byte[] byteArray = { buf[offset + 3], buf[offset + 2], buf[offset + 1], buf[offset] };
            return BitConverter.ToSingle(byteArray, offset);
        }

        public static double ToDouble(byte[] buf, int offset)
        {
            var value = ToInt64(buf, offset);
            return BitConverter.Int64BitsToDouble(value);
        }

        public static char ToChar(byte[] buf, int offset)
        {
            return (char)((buf[offset] << 8) + (buf[offset + 1] << 0));
        }

        public static byte[] GetBytes(char value)
        {
            return new[]
            {
                (byte) ((value >> 8) & 0xFF),
                (byte) ((value >> 0) & 0xFF)
            };
        }

        public static byte[] GetBytes(int value)
        {
            return new[]
            {
                (byte) ((value >> 24) & 0xFF),
                (byte) ((value >> 16) & 0xFF),
                (byte) ((value >> 8) & 0xFF),
                (byte) ((value >> 0) & 0xFF)
            };
        }

        public static byte[] GetBytes(long value)
        {
            return new[]
            {
                (byte) ((value >> 56) & 0xFF),
                (byte) ((value >> 48) & 0xFF),
                (byte) ((value >> 40) & 0xFF),
                (byte) ((value >> 32) & 0xFF),
                (byte) ((value >> 24) & 0xFF),
                (byte) ((value >> 16) & 0xFF),
                (byte) ((value >> 8) & 0xFF),
                (byte) ((value >> 0) & 0xFF)
            };
        }

        public static byte[] GetBytes(short value)
        {
            return new[]
            {
                (byte) ((value >> 8) & 0xFF),
                (byte) ((value >> 0) & 0xFF)
            };
        }

        public static byte[] GetBytes(float value)
        {
            var byteArray = BitConverter.GetBytes(value);
            Array.Reverse(byteArray);
            return byteArray;
        }

        public static byte[] GetBytes(double value)
        {
            var byteArray = BitConverter.GetBytes(value);
            Array.Reverse(byteArray);
            return byteArray;
        }
    }
}
