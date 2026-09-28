using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace MadMax.Net
{
    /// <summary>Growable little-endian byte writer with game quantisation helpers.</summary>
    public class NetWriter
    {
        byte[] buf;
        public int Length { get; private set; }
        public NetWriter(int capacity = 1400) { buf = new byte[capacity]; }
        public byte[] Buffer => buf;
        public void Reset() => Length = 0;
        public Span<byte> Span => new Span<byte>(buf, 0, Length);

        void Ensure(int n)
        {
            if (Length + n <= buf.Length) return;
            Array.Resize(ref buf, Mathf.Max(buf.Length * 2, Length + n));
        }

        public void Byte(byte v) { Ensure(1); buf[Length++] = v; }
        public void SByte(sbyte v) => Byte((byte)v);
        public void Bool(bool v) => Byte(v ? (byte)1 : (byte)0);
        public void UShort(ushort v) { Ensure(2); buf[Length++] = (byte)v; buf[Length++] = (byte)(v >> 8); }
        public void Short(short v) => UShort((ushort)v);
        public void UInt(uint v) { Ensure(4); for (int i = 0; i < 4; i++) buf[Length++] = (byte)(v >> (8 * i)); }
        public void Int(int v) => UInt((uint)v);
        public void Float(float v) => Int(System.BitConverter.SingleToInt32Bits(v));
        public void Bytes(byte[] b, int offset, int count) { Ensure(count); Array.Copy(b, offset, buf, Length, count); Length += count; }

        public void String(string s)
        {
            var b = Encoding.UTF8.GetBytes(s ?? "");
            UShort((ushort)b.Length);
            Bytes(b, 0, b.Length);
        }

        /// <summary>Position in millimetres (±2000 km).</summary>
        public void Pos(Vector3 p) { Int(Mathf.RoundToInt(p.x * 1000f)); Int(Mathf.RoundToInt(p.y * 1000f)); Int(Mathf.RoundToInt(p.z * 1000f)); }

        /// <summary>Velocity in cm/s (±327 m/s).</summary>
        public void Vel(Vector3 v) { Short(Q16(v.x * 100f)); Short(Q16(v.y * 100f)); Short(Q16(v.z * 100f)); }

        static short Q16(float f) => (short)Mathf.Clamp(Mathf.RoundToInt(f), short.MinValue, short.MaxValue);

        /// <summary>Smallest-three quaternion in 32 bits (2-bit index + 3 x 10 bits).</summary>
        public void Rot(Quaternion q)
        {
            q.Normalize();
            float[] c = { q.x, q.y, q.z, q.w };
            int largest = 0;
            for (int i = 1; i < 4; i++) if (Mathf.Abs(c[i]) > Mathf.Abs(c[largest])) largest = i;
            float sign = c[largest] < 0 ? -1f : 1f;
            uint packed = (uint)largest << 30;
            int shift = 20;
            for (int i = 0; i < 4; i++)
            {
                if (i == largest) continue;
                float v = c[i] * sign / 0.70710678f;
                uint qv = (uint)Mathf.Clamp(Mathf.RoundToInt((v * 0.5f + 0.5f) * 1023f), 0, 1023);
                packed |= qv << shift;
                shift -= 10;
            }
            UInt(packed);
        }

        public void Yaw(float degrees) => UShort((ushort)Mathf.RoundToInt(Mathf.Repeat(degrees, 360f) / 360f * 65535f));
    }

    public class NetReader
    {
        byte[] buf;
        int pos, end;
        public bool Failed { get; private set; }
        public int Remaining => end - pos;

        public void Set(byte[] b, int length) { buf = b; pos = 0; end = length; Failed = false; }

        bool Need(int n) { if (pos + n > end) { Failed = true; return false; } return true; }
        public byte Byte() => Need(1) ? buf[pos++] : (byte)0;
        public sbyte SByte() => (sbyte)Byte();
        public bool Bool() => Byte() != 0;
        public ushort UShort() { if (!Need(2)) return 0; ushort v = (ushort)(buf[pos] | buf[pos + 1] << 8); pos += 2; return v; }
        public short Short() => (short)UShort();
        public uint UInt() { if (!Need(4)) return 0; uint v = (uint)(buf[pos] | buf[pos + 1] << 8 | buf[pos + 2] << 16 | buf[pos + 3] << 24); pos += 4; return v; }
        public int Int() => (int)UInt();
        public float Float() => System.BitConverter.Int32BitsToSingle(Int());

        public byte[] Bytes(int count)
        {
            if (!Need(count)) return new byte[0];
            var b = new byte[count];
            Array.Copy(buf, pos, b, 0, count);
            pos += count;
            return b;
        }

        public string String() { int n = UShort(); return Need(n) ? Encoding.UTF8.GetString(Bytes(n)) : ""; }
        public Vector3 Pos() => new Vector3(Int() / 1000f, Int() / 1000f, Int() / 1000f);
        public Vector3 Vel() => new Vector3(Short() / 100f, Short() / 100f, Short() / 100f);
        public float Yaw() => UShort() / 65535f * 360f;

        public Quaternion Rot()
        {
            uint packed = UInt();
            int largest = (int)(packed >> 30);
            float[] c = new float[4];
            int shift = 20; float sum = 0f;
            for (int i = 0; i < 4; i++)
            {
                if (i == largest) continue;
                float v = ((packed >> shift) & 1023) / 1023f * 2f - 1f;
                c[i] = v * 0.70710678f;
                sum += c[i] * c[i];
                shift -= 10;
            }
            c[largest] = Mathf.Sqrt(Mathf.Max(0f, 1f - sum));
            return new Quaternion(c[0], c[1], c[2], c[3]);
        }
    }

    public static class NetCompress
    {
        public static byte[] Zip(string text)
        {
            using var ms = new MemoryStream();
            using (var gz = new GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, true)) { var b = Encoding.UTF8.GetBytes(text); gz.Write(b, 0, b.Length); }
            return ms.ToArray();
        }

        public static string Unzip(byte[] data)
        {
            using var ms = new MemoryStream(data);
            using var gz = new GZipStream(ms, CompressionMode.Decompress);
            using var r = new StreamReader(gz, Encoding.UTF8);
            return r.ReadToEnd();
        }
    }
}
