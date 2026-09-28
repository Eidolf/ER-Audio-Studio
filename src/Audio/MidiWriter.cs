using System;
using System.Collections.Generic;
using System.IO;

namespace ErAudioTool.Audio
{
    public class MidiNote
    {
        public int NoteNumber { get; set; } // 0..127 (69 = A4 = 440Hz)
        public double StartTimeSec { get; set; }
        public double DurationSec { get; set; }
        public int Velocity { get; set; }

        public MidiNote()
        {
            Velocity = 96;
        }

        public string NoteName
        {
            get
            {
                string[] names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
                int oct = (NoteNumber / 12) - 1;
                return names[NoteNumber % 12] + oct;
            }
        }
    }

    public static class MidiWriter
    {
        public static void SaveMidiFile(string filePath, List<MidiNote> notes, int tempoBpm = 120, short ticksPerQuarter = 480)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fs))
            {
                // Header Chunk 'MThd'
                bw.Write(new char[] { 'M', 'T', 'h', 'd' });
                WriteBigEndian32(bw, 6); // Length = 6
                WriteBigEndian16(bw, 0); // Format 0 (Single track)
                WriteBigEndian16(bw, 1); // 1 Track
                WriteBigEndian16(bw, ticksPerQuarter);

                // Build Track Data
                byte[] trackData = BuildTrackData(notes, tempoBpm, ticksPerQuarter);

                // Track Chunk 'MTrk'
                bw.Write(new char[] { 'M', 'T', 'r', 'k' });
                WriteBigEndian32(bw, trackData.Length);
                bw.Write(trackData);
            }
        }

        private static byte[] BuildTrackData(List<MidiNote> notes, int tempoBpm, short ticksPerQuarter)
        {
            // Calculate tick position for each note-on and note-off
            double secondsPerQuarter = 60.0 / tempoBpm;
            double ticksPerSecond = ticksPerQuarter / secondsPerQuarter;

            var events = new List<MidiRawEvent>();

            foreach (var n in notes)
            {
                long startTick = (long)Math.Round(n.StartTimeSec * ticksPerSecond);
                long durationTicks = Math.Max(1, (long)Math.Round(n.DurationSec * ticksPerSecond));
                long endTick = startTick + durationTicks;

                byte noteVal = (byte)Math.Max(0, Math.Min(127, n.NoteNumber));
                byte velVal = (byte)Math.Max(1, Math.Min(127, n.Velocity));

                // Note On
                events.Add(new MidiRawEvent
                {
                    Tick = startTick,
                    Status = 0x90, // Note On channel 0
                    Data1 = noteVal,
                    Data2 = velVal,
                    IsNoteOff = false
                });

                // Note Off
                events.Add(new MidiRawEvent
                {
                    Tick = endTick,
                    Status = 0x80, // Note Off channel 0
                    Data1 = noteVal,
                    Data2 = 0,
                    IsNoteOff = true
                });
            }

            // Sort events by Tick; NoteOff before NoteOn if at same tick
            events.Sort((a, b) =>
            {
                if (a.Tick != b.Tick) return a.Tick.CompareTo(b.Tick);
                if (a.IsNoteOff != b.IsNoteOff) return a.IsNoteOff ? -1 : 1;
                return a.Data1.CompareTo(b.Data1);
            });

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                // Write tempo meta event at tick 0: 500000 microseconds per beat (120 BPM)
                int usPerBeat = (int)(60000000.0 / tempoBpm);
                WriteVarLen(bw, 0); // delta time
                bw.Write((byte)0xFF); // Meta
                bw.Write((byte)0x51); // Tempo
                bw.Write((byte)0x03); // Length
                bw.Write((byte)((usPerBeat >> 16) & 0xFF));
                bw.Write((byte)((usPerBeat >> 8) & 0xFF));
                bw.Write((byte)(usPerBeat & 0xFF));

                long currentTick = 0;
                foreach (var ev in events)
                {
                    long delta = Math.Max(0, ev.Tick - currentTick);
                    WriteVarLen(bw, delta);
                    bw.Write(ev.Status);
                    bw.Write(ev.Data1);
                    bw.Write(ev.Data2);
                    currentTick = ev.Tick;
                }

                // End of Track meta event
                WriteVarLen(bw, 0); // delta
                bw.Write((byte)0xFF);
                bw.Write((byte)0x2F);
                bw.Write((byte)0x00);

                return ms.ToArray();
            }
        }

        private static void WriteVarLen(BinaryWriter bw, long value)
        {
            ulong buffer = (ulong)(value & 0x7F);
            while ((value >>= 7) > 0)
            {
                buffer <<= 8;
                buffer |= 0x80;
                buffer += (ulong)(value & 0x7F);
            }

            while (true)
            {
                bw.Write((byte)(buffer & 0xFF));
                if ((buffer & 0x80) != 0)
                {
                    buffer >>= 8;
                }
                else
                {
                    break;
                }
            }
        }

        private static void WriteBigEndian32(BinaryWriter bw, int val)
        {
            byte[] b = BitConverter.GetBytes(val);
            if (BitConverter.IsLittleEndian) Array.Reverse(b);
            bw.Write(b);
        }

        private static void WriteBigEndian16(BinaryWriter bw, short val)
        {
            byte[] b = BitConverter.GetBytes(val);
            if (BitConverter.IsLittleEndian) Array.Reverse(b);
            bw.Write(b);
        }

        private class MidiRawEvent
        {
            public long Tick { get; set; }
            public byte Status { get; set; }
            public byte Data1 { get; set; }
            public byte Data2 { get; set; }
            public bool IsNoteOff { get; set; }
        }
    }
}
