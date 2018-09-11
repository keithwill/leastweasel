using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LeastWeasel.Messaging
{

    public class Serializer<T> where T : class, new()
    {

        public delegate int SerializerFieldAction(T instance, ref Span<byte> buffer);
        public delegate int ReadFieldAction(T instance, ref Span<byte> buffer);

        private List<SerializerFieldAction> serializerFieldActionsBuilder;
        private SerializerFieldAction[] serializerFieldActions;
        private ReadFieldAction[] readFieldActions;
        private List<ReadFieldAction> readFieldActionsBuilder;

        private Dictionary<int, Func<T, int>> intGetters;
        private Dictionary<int, Action<T, int>> intSetters;
        private Dictionary<int, Func<T, int[]>> intArrayGetters;
        private Dictionary<int, Action<T, int[]>> intArraySetters;

        private Dictionary<int, Action<T, double>> doubleSetters;
        private Dictionary<int, Func<T, double>> doubleGetters;
        private Dictionary<int, Action<T, double[]>> doubleArraySetters;
        private Dictionary<int, Func<T, double[]>> doubleArrayGetters;

        private Dictionary<int, Func<T, bool>> boolGetters;
        private Dictionary<int, Action<T, bool>> boolSetters;
        private Dictionary<int, Func<T, bool[]>> boolArrayGetters;
        private Dictionary<int, Action<T, bool[]>> boolArraySetters;

        private Dictionary<int, Func<T, byte>> byteGetters;
        private Dictionary<int, Action<T, byte>> byteSetters;
        private Dictionary<int, Func<T, byte[]>> byteArrayGetters;
        private Dictionary<int, Action<T, byte[]>> byteArraySetters;

        private Dictionary<int, Func<T, char>> charGetters;
        private Dictionary<int, Action<T, char>> charSetters;
        private Dictionary<int, Func<T, char[]>> charArrayGetters;
        private Dictionary<int, Action<T, char[]>> charArraySetters;

        private Dictionary<int, Func<T, decimal>> decimalGetters;
        private Dictionary<int, Action<T, decimal>> decimalSetters;
        private Dictionary<int, Func<T, decimal[]>> decimalArrayGetters;
        private Dictionary<int, Action<T, decimal[]>> decimalArraySetters;

        private Dictionary<int, Func<T, float>> floatGetters;
        private Dictionary<int, Action<T, float>> floatSetters;
        private Dictionary<int, Func<T, float[]>> floatArrayGetters;
        private Dictionary<int, Action<T, float[]>> floatArraySetters;

        private Dictionary<int, Func<T, long>> longGetters;
        private Dictionary<int, Action<T, long>> longSetters;
        private Dictionary<int, Func<T, long[]>> longArrayGetters;
        private Dictionary<int, Action<T, long[]>> longArraySetters;

        private Dictionary<int, Func<T, short>> shortGetters;
        private Dictionary<int, Action<T, short>> shortSetters;
        private Dictionary<int, Func<T, short[]>> shortArrayGetters;
        private Dictionary<int, Action<T, short[]>> shortArraySetters;

        private Dictionary<int, Func<T, DateTime>> dateTimeGetters;
        private Dictionary<int, Action<T, DateTime>> dateTimeSetters;
        private Dictionary<int, Func<T, DateTime[]>> dateTimeArrayGetters;
        private Dictionary<int, Action<T, DateTime[]>> dateTimeArraySetters;

        private Dictionary<int, Func<T, string>> stringGetters;
        private Dictionary<int, Action<T, string>> stringSetters;
        private Dictionary<int, Func<T, string[]>> stringArrayGetters;
        private Dictionary<int, Action<T, string[]>> stringArraySetters;

        private Dictionary<int, Func<T, object>> complexGetters;
        private Dictionary<int, Action<T, object>> complexSetters;
        private Dictionary<int, Func<T, object[]>> complexArrayGetters;
        private Dictionary<int, Action<T, object[]>> complexArraySetters;


        public Serializer()
        {

            intGetters = new Dictionary<int, Func<T, int>>();
            intSetters = new Dictionary<int, Action<T, int>>();
            intArrayGetters = new Dictionary<int, Func<T, int[]>>();
            intArraySetters = new Dictionary<int, Action<T, int[]>>();

            doubleGetters = new Dictionary<int, Func<T, double>>();
            doubleSetters = new Dictionary<int, Action<T, double>>();
            doubleArrayGetters = new Dictionary<int, Func<T, double[]>>();
            doubleArraySetters = new Dictionary<int, Action<T, double[]>>();

            boolGetters = new Dictionary<int, Func<T, bool>>();
            boolSetters = new Dictionary<int, Action<T, bool>>();
            boolArrayGetters = new Dictionary<int, Func<T, bool[]>>();
            boolArraySetters = new Dictionary<int, Action<T, bool[]>>();

            byteGetters = new Dictionary<int, Func<T, byte>>();
            byteSetters = new Dictionary<int, Action<T, byte>>();
            byteArrayGetters = new Dictionary<int, Func<T, byte[]>>();
            byteArraySetters = new Dictionary<int, Action<T, byte[]>>();

            charGetters = new Dictionary<int, Func<T, char>>();
            charSetters = new Dictionary<int, Action<T, char>>();
            charArrayGetters = new Dictionary<int, Func<T, char[]>>();
            charArraySetters = new Dictionary<int, Action<T, char[]>>();

            decimalGetters = new Dictionary<int, Func<T, decimal>>();
            decimalSetters = new Dictionary<int, Action<T, decimal>>();
            decimalArrayGetters = new Dictionary<int, Func<T, decimal[]>>();
            decimalArraySetters = new Dictionary<int, Action<T, decimal[]>>();

            floatGetters = new Dictionary<int, Func<T, float>>();
            floatSetters = new Dictionary<int, Action<T, float>>();
            floatArrayGetters = new Dictionary<int, Func<T, float[]>>();
            floatArraySetters = new Dictionary<int, Action<T, float[]>>();

            longGetters = new Dictionary<int, Func<T, long>>();
            longSetters = new Dictionary<int, Action<T, long>>();
            longArrayGetters = new Dictionary<int, Func<T, long[]>>();
            longArraySetters = new Dictionary<int, Action<T, long[]>>();

            shortGetters = new Dictionary<int, Func<T, short>>();
            shortSetters = new Dictionary<int, Action<T, short>>();
            shortArrayGetters = new Dictionary<int, Func<T, short[]>>();
            shortArraySetters = new Dictionary<int, Action<T, short[]>>();

            dateTimeSetters = new Dictionary<int, Action<T, DateTime>>();
            dateTimeGetters = new Dictionary<int, Func<T, DateTime>>();
            dateTimeArrayGetters = new Dictionary<int, Func<T, DateTime[]>>();
            dateTimeArraySetters = new Dictionary<int, Action<T, DateTime[]>>();

            stringSetters = new Dictionary<int, Action<T, string>>();
            stringGetters = new Dictionary<int, Func<T, string>>();
            stringArrayGetters = new Dictionary<int, Func<T, string[]>>();
            stringArraySetters = new Dictionary<int, Action<T, string[]>>();

            complexGetters = new Dictionary<int, Func<T, object>>();
            complexSetters = new Dictionary<int, Action<T, object>>();
            complexArrayGetters = new Dictionary<int, Func<T, object[]>>();
            complexArraySetters = new Dictionary<int, Action<T, object[]>>();

            serializerFieldActionsBuilder = new List<SerializerFieldAction>();
            readFieldActionsBuilder = new List<ReadFieldAction>();

        }

        public Serializer<T> Build()
        {
            serializerFieldActions = serializerFieldActionsBuilder.ToArray();
            readFieldActions = readFieldActionsBuilder.ToArray();
            return this;
        }


        public Serializer<T> Field(Func<T, int> getter, Action<T, int> setter)
        {
            var tag = serializerFieldActionsBuilder.Count + 1;
            intSetters.Add(tag, setter);
            intGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer);
                intBuffer[0] = intGetters[tag](x);
                return 4;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer);
                intSetters[tag](x, intBuffer[0]);
                return 4;
            });

            return this;
        }

        public Serializer<T> Array(Func<T, int[]> getter, Action<T, int[]> setter)
        {
            var tag = serializerFieldActionsBuilder.Count + 1;
            intArraySetters.Add(tag, setter);
            intArrayGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var value = intArrayGetters[tag](x);
                int dataLength = (value.Length * 4);
                int writeLength = 4 + dataLength;
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(4, writeLength));
                intBuffer[0] = dataLength;
                for (int i = 0; i < value.Length; i++)
                {
                    intBuffer[i + 1] = value[i];
                }
                return writeLength;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer);
                var length = intBuffer[0];
                var data = new int[length];
                for (int i = 0; i < length; i++)
                {
                    data[i] = intBuffer[i + 1];
                }
                intArraySetters[tag](x, data);
                return 4 + (length * 4);
            });

            return this;
        }

        public Serializer<T> Field(Func<T, double> getter, Action<T, double> setter)
        {
            var tag = serializerFieldActionsBuilder.Count + 1;
            doubleSetters.Add(tag, setter);
            doubleGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var doubleBuffer = MemoryMarshal.Cast<byte, double>(buffer);
                doubleBuffer[0] = doubleGetters[tag](x);
                return 8;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var doubleBuffer = MemoryMarshal.Cast<byte, double>(buffer);
                doubleSetters[tag](x, doubleBuffer[0]);
                return 8;
            });

            return this;
        }

        public Serializer<T> Array(Func<T, double[]> getter, Action<T, double[]> setter)
        {
            var tag = serializerFieldActionsBuilder.Count + 1;
            doubleArraySetters.Add(tag, setter);
            doubleArrayGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var value = doubleArrayGetters[tag](x);
                int dataLength = (value.Length * 8);
                int writeLength = 4 + dataLength;
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(0, 4));
                intBuffer[0] = dataLength;
                var doubleBuffer = MemoryMarshal.Cast<byte, double>(buffer.Slice(4, dataLength));
                for (int i = 0; i < value.Length; i++)
                {
                    doubleBuffer[i] = value[i];
                }
                return writeLength;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer);
                var length = intBuffer[0];
                var doubleBuffer = MemoryMarshal.Cast<byte, double>(buffer.Slice(4));
                var data = new double[length];
                for (int i = 0; i < length; i++)
                {
                    data[i] = doubleBuffer[i];
                }
                doubleArraySetters[tag](x, data);
                return 4 + (length * 8);
            });

            return this;
        }

        public Serializer<T> Field(Func<T, bool> getter, Action<T, bool> setter)
        {
            var tag = serializerFieldActionsBuilder.Count + 1;
            boolSetters.Add(tag, setter);
            boolGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                buffer[0] = boolGetters[tag](x) ? (byte)1 : (byte)0;
                return 1;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                boolSetters[tag](x, buffer[0] == 1);
                return 1;
            });

            return this;
        }

        public Serializer<T> Array(Func<T, bool[]> getter, Action<T, bool[]> setter)
        {
            var tag = serializerFieldActionsBuilder.Count + 1;
            boolArraySetters.Add(tag, setter);
            boolArrayGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var value = boolArrayGetters[tag](x);
                int dataLength = value.Length;
                int writeLength = 4 + dataLength;
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(0, 4));
                intBuffer[0] = dataLength;
                var boolBuffer = buffer.Slice(4, dataLength);
                for (int i = 0; i < value.Length; i++)
                {
                    boolBuffer[i] = value[i] ? (byte)1 : (byte)0;
                }
                return writeLength;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer);
                var length = intBuffer[0];

                var boolBuffer = buffer.Slice(4);
                var data = new bool[length];
                for (int i = 0; i < length; i++)
                {
                    data[i] = boolBuffer[i] == 1;
                }
                boolArraySetters[tag](x, data);
                return 4 + (length);
            });

            return this;
        }

        public Serializer<T> Field(Func<T, byte> getter, Action<T, byte> setter)
        {
            var tag = serializerFieldActionsBuilder.Count + 1;
            byteSetters.Add(tag, setter);
            byteGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                buffer[0] = byteGetters[tag](x);
                return 1;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                byteSetters[tag](x, buffer[0]);
                return 1;
            });

            return this;
        }

        public Serializer<T> Array(Func<T, byte[]> getter, Action<T, byte[]> setter)
        {
            var tag = serializerFieldActionsBuilder.Count + 1;
            byteArraySetters.Add(tag, setter);
            byteArrayGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var value = byteArrayGetters[tag](x);
                int dataLength = value.Length;
                int writeLength = 4 + dataLength;
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(0, 4));
                intBuffer[0] = dataLength;

                var byteBuffer = buffer.Slice(4, dataLength);
                for (int i = 0; i < value.Length; i++)
                {
                    byteBuffer[i] = value[i];
                }
                return writeLength;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer);
                var length = intBuffer[0];

                var byteBuffer = buffer.Slice(4);
                var data = new byte[length];
                for (int i = 0; i < length; i++)
                {
                    data[i] = byteBuffer[i];
                }
                byteArraySetters[tag](x, data);
                return 4 + (length);
            });

            return this;
        }

        public Serializer<T> Field(Func<T, char> getter, Action<T, char> setter)
        {

            var tag = serializerFieldActionsBuilder.Count + 1;
            charSetters.Add(tag, setter);
            charGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var doubleBuffer = MemoryMarshal.Cast<byte, char>(buffer);
                doubleBuffer[0] = charGetters[tag](x);
                return 2;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var doubleBuffer = MemoryMarshal.Cast<byte, char>(buffer);
                charSetters[tag](x, doubleBuffer[0]);
                return 2;
            });

            return this;
        }

        public Serializer<T> Array(Func<T, char[]> getter, Action<T, char[]> setter)
        {
            var tag = serializerFieldActionsBuilder.Count + 1;
            charArraySetters.Add(tag, setter);
            charArrayGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var value = charArrayGetters[tag](x);
                int dataLength = (value.Length * 2);
                int writeLength = 4 + dataLength;
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(0, 4));
                intBuffer[0] = dataLength;
                var charBuffer = MemoryMarshal.Cast<byte, char>(buffer.Slice(4, dataLength));
                for (int i = 0; i < value.Length; i++)
                {
                    charBuffer[i] = value[i];
                }
                return writeLength;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer);
                var length = intBuffer[0];
                var charBuffer = MemoryMarshal.Cast<byte, char>(buffer.Slice(4));
                var data = new char[length];
                for (int i = 0; i < length; i++)
                {
                    data[i] = charBuffer[i];
                }
                charArraySetters[tag](x, data);
                return 4 + (length * 2);
            });

            return this;
        }

        public Serializer<T> Field(Func<T, decimal> getter, Action<T, decimal> setter)
        {
            var tag = serializerFieldActionsBuilder.Count + 1;
            decimalSetters.Add(tag, setter);
            decimalGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var decimalBuffer = MemoryMarshal.Cast<byte, decimal>(buffer);
                decimalBuffer[0] = decimalGetters[tag](x);
                return 16;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var decimalBuffer = MemoryMarshal.Cast<byte, decimal>(buffer);
                decimalSetters[tag](x, decimalBuffer[0]);
                return 16;
            });

            return this;
        }

        public Serializer<T> Array(Func<T, decimal[]> getter, Action<T, decimal[]> setter)
        {
            var tag = serializerFieldActionsBuilder.Count + 1;
            decimalArraySetters.Add(tag, setter);
            decimalArrayGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var value = decimalArrayGetters[tag](x);
                int dataLength = (value.Length * 16);
                int writeLength = 4 + dataLength;
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(0, 4));
                intBuffer[0] = dataLength;
                var decimalBuffer = MemoryMarshal.Cast<byte, decimal>(buffer.Slice(4, dataLength));
                for (int i = 0; i < value.Length; i++)
                {
                    decimalBuffer[i] = value[i];
                }
                return writeLength;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(0, 4));
                var length = intBuffer[0];
                var doubleBuffer = MemoryMarshal.Cast<byte, decimal>(buffer.Slice(4));
                var data = new decimal[length];
                for (int i = 0; i < length; i++)
                {
                    data[i] = doubleBuffer[i];
                }
                decimalArraySetters[tag](x, data);
                return 4 + (length * 16);
            });

            return this;
        }

        public Serializer<T> Field(Func<T, float> getter, Action<T, float> setter)
        {
            var tag = serializerFieldActionsBuilder.Count + 1;
            floatSetters.Add(tag, setter);
            floatGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var floatBuffer = MemoryMarshal.Cast<byte, float>(buffer);
                floatBuffer[0] = floatGetters[tag](x);
                return 4;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var floatBuffer = MemoryMarshal.Cast<byte, float>(buffer);
                floatSetters[tag](x, floatBuffer[0]);
                return 8;
            });

            return this;
        }

        public Serializer<T> Array(Func<T, float[]> getter, Action<T, float[]> setter)
        {

            var tag = serializerFieldActionsBuilder.Count + 1;
            floatArraySetters.Add(tag, setter);
            floatArrayGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var value = floatArrayGetters[tag](x);
                int dataLength = (value.Length * 4);
                int writeLength = 4 + dataLength;
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(0, 4));
                intBuffer[0] = dataLength;
                var floatBuffer = MemoryMarshal.Cast<byte, float>(buffer.Slice(4, dataLength));
                for (int i = 0; i < value.Length; i++)
                {
                    floatBuffer[i] = value[i];
                }
                return writeLength;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(0, 4));
                var length = intBuffer[0];
                var floatBuffer = MemoryMarshal.Cast<byte, float>(buffer.Slice(4));
                var data = new float[length];
                for (int i = 0; i < length; i++)
                {
                    data[i] = floatBuffer[i];
                }
                floatArraySetters[tag](x, data);
                return 4 + (length * 4);
            });

            return this;
        }

        public Serializer<T> Field(Func<T, long> getter, Action<T, long> setter)
        {
            var tag = serializerFieldActionsBuilder.Count + 1;
            longSetters.Add(tag, setter);
            longGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var longBuffer = MemoryMarshal.Cast<byte, long>(buffer);
                longBuffer[0] = longGetters[tag](x);
                return 8;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var longBuffer = MemoryMarshal.Cast<byte, long>(buffer);
                longSetters[tag](x, longBuffer[0]);
                return 8;
            });

            return this;
        }

        public Serializer<T> Array(Func<T, long[]> getter, Action<T, long[]> setter)
        {
            var tag = serializerFieldActionsBuilder.Count + 1;
            longArraySetters.Add(tag, setter);
            longArrayGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var value = longArrayGetters[tag](x);
                int dataLength = (value.Length * 8);
                int writeLength = 4 + dataLength;
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(0, 4));
                intBuffer[0] = dataLength;
                var longBuffer = MemoryMarshal.Cast<byte, long>(buffer.Slice(4, dataLength));
                for (int i = 0; i < value.Length; i++)
                {
                    longBuffer[i] = value[i];
                }
                return writeLength;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(0, 4));
                var length = intBuffer[0];
                var longBuffer = MemoryMarshal.Cast<byte, long>(buffer.Slice(4));
                var data = new long[length];
                for (int i = 0; i < length; i++)
                {
                    data[i] = longBuffer[i];
                }
                longArraySetters[tag](x, data);
                return 4 + (length * 8);
            });

            return this;
        }

        public Serializer<T> Field(Func<T, short> getter, Action<T, short> setter)
        {
            var tag = serializerFieldActionsBuilder.Count + 1;
            shortSetters.Add(tag, setter);
            shortGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var doubleBuffer = MemoryMarshal.Cast<byte, short>(buffer);
                doubleBuffer[0] = shortGetters[tag](x);
                return 2;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var doubleBuffer = MemoryMarshal.Cast<byte, short>(buffer);
                shortSetters[tag](x, doubleBuffer[0]);
                return 2;
            });

            return this;
        }

        public Serializer<T> Array(Func<T, short[]> getter, Action<T, short[]> setter)
        {

            var tag = serializerFieldActionsBuilder.Count + 1;
            shortArraySetters.Add(tag, setter);
            shortArrayGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var value = shortArrayGetters[tag](x);
                int dataLength = (value.Length * 2);
                int writeLength = 4 + dataLength;
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(0, 4));
                intBuffer[0] = dataLength;
                var shortBuffer = MemoryMarshal.Cast<byte, short>(buffer.Slice(4, dataLength));
                for (int i = 0; i < value.Length; i++)
                {
                    shortBuffer[i] = value[i];
                }
                return writeLength;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(0, 4));
                var length = intBuffer[0];
                var shortBuffer = MemoryMarshal.Cast<byte, short>(buffer.Slice(4));
                var data = new short[length];
                for (int i = 0; i < length; i++)
                {
                    data[i] = shortBuffer[i];
                }
                shortArraySetters[tag](x, data);
                return 4 + (length * 2);
            });

            return this;
        }

        public Serializer<T> Field(Func<T, DateTime> getter, Action<T, DateTime> setter)
        {

            var tag = serializerFieldActionsBuilder.Count + 1;
            dateTimeSetters.Add(tag, setter);
            dateTimeGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var dateTimeBuffer = MemoryMarshal.Cast<byte, DateTime>(buffer);
                dateTimeBuffer[0] = dateTimeGetters[tag](x);
                return 8;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var dateTimeBuffer = MemoryMarshal.Cast<byte, DateTime>(buffer);
                dateTimeSetters[tag](x, dateTimeBuffer[0]);
                return 8;
            });

            return this;
        }

        public Serializer<T> Array(Func<T, DateTime[]> getter, Action<T, DateTime[]> setter)
        {

            var tag = serializerFieldActionsBuilder.Count + 1;
            dateTimeArraySetters.Add(tag, setter);
            dateTimeArrayGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var value = dateTimeArrayGetters[tag](x);
                int dataLength = (value.Length * 8);
                int writeLength = 4 + dataLength;
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(0, 4));
                intBuffer[0] = dataLength;
                var dateTimeBuffer = MemoryMarshal.Cast<byte, DateTime>(buffer.Slice(4, dataLength));
                for (int i = 0; i < value.Length; i++)
                {
                    dateTimeBuffer[i] = value[i];
                }
                return writeLength;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(0, 4));
                var length = intBuffer[0];
                var dateTimeBuffer = MemoryMarshal.Cast<byte, DateTime>(buffer.Slice(4));
                var data = new DateTime[length];
                for (int i = 0; i < length; i++)
                {
                    data[i] = dateTimeBuffer[i];
                }
                dateTimeArraySetters[tag](x, data);
                return 4 + (length * 8);
            });

            return this;
        }

        public Serializer<T> Field(Func<T, string> getter, Action<T, string> setter)
        {
            var tag = serializerFieldActionsBuilder.Count + 1;
            stringSetters.Add(tag, setter);
            stringGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                ReadOnlySpan<char> value = stringGetters[tag](x);
                int dataLength = (value.Length * 2);
                int writeLength = 4 + dataLength;
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(0, 4));
                intBuffer[0] = dataLength;
                var charBuffer = MemoryMarshal.Cast<byte, char>(buffer.Slice(4, dataLength));
                for (int i = 0; i < value.Length; i++)
                {
                    charBuffer[i] = value[i];
                }
                return writeLength;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(0, 4));
                var length = intBuffer[0];
                ReadOnlySpan<char> chars = MemoryMarshal.Cast<byte, char>(buffer.Slice(4, length));
                var value = new String(chars);
                stringSetters[tag](x, value);
                return 4 + (length * 2);
            });
            return this;
        }

        public Serializer<T> Array(Func<T, string[]> getter, Action<T, string[]> setter)
        {
            var tag = serializerFieldActionsBuilder.Count + 1;
            stringArraySetters.Add(tag, setter);
            stringArrayGetters.Add(tag, getter);

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var position = 0;
                string[] values = stringArrayGetters[tag](x);
                var valuesLength = values.Length;
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(0, 4));
                position += 4;
                intBuffer[0] = valuesLength;

                for (int v = 0; v < valuesLength; v++)
                {
                    ReadOnlySpan<char> value = values[v];
                    intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(position, 4));
                    var valueLength = value.Length;
                    intBuffer[0] = valueLength;
                    position += 4;
                    var dataLength = valueLength * 2;
                    var charBuffer = MemoryMarshal.Cast<byte, char>(buffer.Slice(position, dataLength));
                    for (int i = 0; i < valueLength; i++)
                    {
                        charBuffer[i] = value[i];
                    }
                    position += dataLength;
                }

                return position;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var position = 0;
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(0, 4));
                var valuesLength = intBuffer[0];
                position += 4;
                var values = new string[valuesLength];

                for (int v = 0; v < valuesLength; v++)
                {
                    intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(position, 4));
                    var valueLength = intBuffer[0];
                    position += 4;
                    ReadOnlySpan<char> chars = MemoryMarshal.Cast<byte, char>(buffer.Slice(4, valueLength));
                    var value = new String(chars);
                    position += valueLength * 2;
                    values[v] = value;
                }

                stringArraySetters[tag](x, values);
                return position;
            });
            return this;
        }

        public Serializer<T> Field<TFieldType>(Func<T, TFieldType> getter, Action<T, TFieldType> setter, Serializer<TFieldType> serializer) where TFieldType : class, new()
        {

            var tag = serializerFieldActionsBuilder.Count + 1;
            complexGetters.Add(tag, getter);
            complexSetters.Add(tag, (x, f) => setter(x, (TFieldType)f));


            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {

                var field = (TFieldType)complexGetters[tag](x);
                return serializer.Serialize(field, ref buffer);
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var field = (TFieldType)complexGetters[tag](x);
                var read = serializer.Deserialize(ref buffer, ref field);
                complexSetters[tag](x, field);
                return read;
            });

            return this;
        }

        public Serializer<T> Array<TFieldType>(Func<T, TFieldType[]> getter, Action<T, TFieldType[]> setter, Serializer<TFieldType> serializer) where TFieldType : class, new()
        {

            var tag = serializerFieldActionsBuilder.Count + 1;
            complexArrayGetters.Add(tag, getter);
            complexArraySetters.Add(tag, (x, f) => setter(x, (TFieldType[])f));

            serializerFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var position = 0;
                var array = complexArrayGetters[tag](x);
                var values = System.Array.ConvertAll(array, (item) => (TFieldType)item);
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(position, 4));
                intBuffer[0] = values.Length;
                position += 4;
                for (int i = 0; i < values.Length; i++)
                {
                    var valueBuffer = buffer.Slice(position);
                    position += serializer.Serialize(values[i], ref valueBuffer);
                }
                return position;
            });

            readFieldActionsBuilder.Add((T x, ref Span<byte> buffer) =>
            {
                var position = 0;
                var intBuffer = MemoryMarshal.Cast<byte, int>(buffer.Slice(position, 4));
                var valuesLength = intBuffer[0];
                position += 4;
                var values = new TFieldType[valuesLength];
                for (int i = 0; i < valuesLength; i++)
                {
                    TFieldType value = null;
                    var valueBuffer = buffer.Slice(position);
                    position += serializer.Deserialize(ref valueBuffer, ref value);
                }
                return position;
            });

            return this;
        }

        public T Deserialize(ref Span<byte> deserializeBuffer, int offset = 0)
        {
            T result = null;
            Deserialize(ref deserializeBuffer, ref result, offset);
            return result;
        }

        public int Deserialize(ref Span<byte> deserializeBuffer, ref T instance, int offset = 0)
        {

            if (instance == null)
            {
                instance = new T();
            }

            var position = offset;
            var length = MemoryMarshal.Cast<byte, int>(deserializeBuffer)[0];
            var endPosition = position + length;
            position += 4;

            //var messageSpan = deserializeBuffer.Slice(position, length);

            for (int tag = 0; tag < readFieldActions.Length; tag++)
            {
                var fieldBuffer = deserializeBuffer.Slice(position);
                position += readFieldActions[tag](instance, ref fieldBuffer);
            }

            if (position != endPosition)
            {
                throw new Exception("Deserialized data does not appear valid");
            }

            return position - offset;
        }

        public int Serialize(T value, ref Span<byte> serializeBuffer)
        {
            var offset = 4; // Leave space to go back and write total length

            for (int i = 0; i < serializerFieldActions.Length; i++)
            {
                var fieldBuffer = serializeBuffer.Slice(offset);
                offset += serializerFieldActions[i](value, ref fieldBuffer);
            }

            var intBuffer = MemoryMarshal.Cast<byte, int>(serializeBuffer.Slice(0, 4));
            intBuffer[0] = offset;

            return offset;
        }


    }

}