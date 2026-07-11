/*
    Copyright (c) 2020 The LibDescent Team.

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LibDescent.Data.Midi
{
    /// <summary>
    /// Represents a (Standard) MIDI sequence.
    /// </summary>
    public class MIDISequence
    {
        /// <summary>
        /// The MIDI format used by this sequence.
        /// </summary>
        public MIDIFormat Format { get; private set; }
        /// <summary>
        /// The tracks contained in this sequence.
        /// </summary>
        public List<MIDITrack> Tracks;
        /// <summary>
        /// Channel playback priorities for HMP files. Undefined for MIDI files.
        /// </summary>
        public int[] HMPChannelPriorities = new int[16];

        private bool tickRateSMPTE;
        private int tickRateQuarterTicks;
        private int tickRateFrameTicks;
        private MIDISMPTEFrameRate tickRateFrames;

        /// <summary>
        /// Initializes a new instance of the MIDISequence class, representing a MIDI sequence with one track without any events.
        /// </summary>
        public MIDISequence()
        {
            Format = MIDIFormat.Type1;
            Tracks = new List<MIDITrack>();
            Tracks.Add(new MIDITrack());

            tickRateSMPTE = false;
            tickRateQuarterTicks = 480;
        }

        /// <summary>
        /// Initializes a new MIDISequence instance by loading a MIDI file from a file.
        /// </summary>
        /// <param name="filePath">The path of the file to load from.</param>
        /// <returns>The loaded MIDI sequence.</returns>
        public static MIDISequence LoadMIDI(string filePath)
        {
            var midi = new MIDISequence();
            midi.Read(filePath);
            return midi;
        }

        /// <summary>
        /// Initializes a new MIDISequence instance by loading a MIDI file from a stream.
        /// </summary>
        /// <param name="stream">The stream to load from.</param>
        /// <returns>The loaded MIDI sequence.</returns>
        public static MIDISequence LoadMIDI(Stream stream)
        {
            var midi = new MIDISequence();
            midi.Read(stream);
            return midi;
        }

        /// <summary>
        /// Initializes a new MIDISequence instance by loading a MIDI file from a byte array.
        /// </summary>
        /// <param name="array">The byte array to load from.</param>
        /// <returns>The loaded MIDI sequence.</returns>
        public static MIDISequence LoadMIDI(byte[] array)
        {
            var midi = new MIDISequence();
            midi.Read(array);
            return midi;
        }

        /// <summary>
        /// Initializes a new MIDISequence instance by loading an HMP file from a file.
        /// </summary>
        /// <param name="filePath">The path of the file to load from.</param>
        /// <returns>The loaded MIDI sequence.</returns>
        public static MIDISequence LoadHMP(string filePath)
        {
            var midi = new MIDISequence();
            midi.ReadHMP(filePath);
            return midi;
        }

        /// <summary>
        /// Initializes a new MIDISequence instance by loading an HMP file from a stream.
        /// </summary>
        /// <param name="stream">The stream to load from.</param>
        /// <returns>The loaded MIDI sequence.</returns>
        public static MIDISequence LoadHMP(Stream stream)
        {
            var midi = new MIDISequence();
            midi.ReadHMP(stream);
            return midi;
        }

        /// <summary>
        /// Initializes a new MIDISequence instance by loading an HMP file from a byte array.
        /// </summary>
        /// <param name="array">The byte array to load from.</param>
        /// <returns>The loaded MIDI sequence.</returns>
        public static MIDISequence LoadHMP(byte[] array)
        {
            var midi = new MIDISequence();
            midi.ReadHMP(array);
            return midi;
        }

        /// <summary>
        /// The MIDI pulses per quarter (PPQ) value, describing the number of MIDI ticks in a quarter note, or -1 if the MIDI uses SMPTE timing.
        /// For HMP files, Descent ignores this value, and assumes it to always be 60.
        /// </summary>
        public int PulsesPerQuarter
        {
            get => tickRateSMPTE ? -1 : tickRateQuarterTicks;
            set
            {
                if (tickRateSMPTE)
                    return;
                if (value <= 0)
                    throw new ArgumentException("PPQ must be non-negative");
                tickRateQuarterTicks = value;
            }
        }

        /// <summary>
        /// Gets the total number of tracks in this sequence.
        /// </summary>
        public int TrackCount => Tracks.Count;

        /// <summary>
        /// Loads a MIDI sequence from a stream.
        /// </summary>
        /// <param name="stream">The stream to load from.</param>
        public void Read(Stream stream)
        {
            using (BinaryReaderMIDI br = new BinaryReaderMIDI(stream))
            {
                stream.Seek(0, SeekOrigin.Begin);
                if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "MThd")
                    throw new ArgumentException("Not a valid standard MIDI 1.0 file");
                if (br.ReadInt32() != 6)
                    throw new ArgumentException("Not a valid standard MIDI 1.0 file");
                short format = br.ReadInt16();
                if (format >= 3)
                    throw new ArgumentException("Not a valid standard MIDI 1.0 file");
                Format = (MIDIFormat)format;
                ushort nTracks = br.ReadUInt16();
                short division = br.ReadInt16();
                if (tickRateSMPTE = (division < 0))
                {
                    tickRateFrameTicks = division & 0xFF;
                    int fr = -(division >> 8);
                    switch (fr)
                    {
                        case 24:
                        default:
                            tickRateFrames = MIDISMPTEFrameRate.F24;
                            break;
                        case 25:
                            tickRateFrames = MIDISMPTEFrameRate.F25;
                            break;
                        case 29:
                            tickRateFrames = MIDISMPTEFrameRate.F30Drop;
                            break;
                        case 30:
                            tickRateFrames = MIDISMPTEFrameRate.F30;
                            break;
                    }
                }
                else
                    tickRateQuarterTicks = division & 0x7FFF;

                Tracks.Clear();
                for (int i = 0; i < nTracks; ++i)
                {
                    MIDITrack trk = new MIDITrack();
                    if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "MTrk")
                        throw new ArgumentException("Not a valid MIDI");
                    int trackLength = br.ReadInt32();
                    byte[] trackData = br.ReadBytes(trackLength);
                    if (trackData.Length < trackLength)
                        throw new EndOfStreamException();

                    ReadMIDITrack(i, trk, trackData);
                    Tracks.Add(trk);
                }
            }
        }

        private void SplitTrackByChannel()
        {
            if (TrackCount != 1) return;

            // try to separate every channel into its own track
            MIDITrack[] newTracks = new MIDITrack[17];
            List<MIDIEvent> events = new List<MIDIEvent>();
            newTracks[0] = new MIDITrack();
            events.AddRange(Tracks[0].GetAllEvents());

            foreach (MIDIEvent evt in events)
            {
                MIDIMessage msg = evt.Data;
                int target = 0;
                if (msg.Channel >= 0)
                    target = msg.Channel + 1;
                if (newTracks[target] == null)
                    newTracks[target] = new MIDITrack();
                newTracks[target].AddEvent(evt);
            }

            Tracks.Clear();
            for (int i = 0; i < newTracks.Length; ++i)
            {
                MIDITrack trk = newTracks[i];
                if (trk != null)
                {
                    trk.TerminateTrack();
                    Tracks.Add(trk);
                }
            }
        }

        /// <summary>
        /// Converts this MIDI track into another format. If converting into Type0, all MIDI tracks
        /// will be merged into one track. If converting into HMI, HMP-specific values will be
        /// Initializes with defaults.
        /// </summary>
        /// <param name="newFormat"></param>
        public void Convert(MIDIFormat newFormat)
        {
            MIDIFormat oldFormat = Format;
            if (oldFormat == MIDIFormat.HMI)
            {
                tickRateSMPTE = false;
            }

            switch (newFormat)
            {
                case MIDIFormat.Type0:
                    MIDITrack mergedTrack = new MIDITrack();
                    List<MIDIEvent> allEvents = new List<MIDIEvent>();
                    foreach (MIDITrack trk in Tracks)
                        allEvents.AddRange(trk.GetAllEvents());
                    allEvents.Sort(new MIDIEventComparer());
                    foreach (MIDIEvent evt in allEvents)
                        mergedTrack.AddEvent(evt);
                    Tracks.Clear();
                    Tracks.Add(mergedTrack);
                    mergedTrack.TerminateTrack();
                    break;

                case MIDIFormat.Type1:
                    if (oldFormat == MIDIFormat.Type0)
                        SplitTrackByChannel();
                    break;

                case MIDIFormat.HMI:
                    if (oldFormat == MIDIFormat.Type0)
                        SplitTrackByChannel();

                    // channel priorities. gather all channels used during track events on all tracks
                    // and set them all to minimum music priority
                    int[] usedChannels = new int[16];
                    foreach (MIDITrack track in Tracks)
                    {
                        foreach (MIDIEvent evt in track)
                        {
                            MIDIMessage msg = evt.Data;
                            if (!msg.IsExtendedEvent && msg.Channel >= 0)
                            {
                                usedChannels[msg.Channel] = 1;
                            }
                        }
                    }
                    for (int i = 0; i < 16; ++i)
                        HMPChannelPriorities[i] = usedChannels[i] > 0 ? HMPConstants.HMI_PRIORITY_MINIMUM : HMPConstants.HMI_PRIORITY_MAXIMUM;

                    // default device map information
                    if (TrackCount < 1) break;
                    Tracks[0].HMPDevices = HMPValidDevice.Default;
                    for (int i = 1; i < TrackCount; ++i)
                        Tracks[i].HMPDevices = HMPValidDevice.All;
                    
                    break;
            }
            Format = newFormat;
        }

        /// <summary>
        /// Adjusts the pulses per quarter (PPQ) value of this sequence and shifts all event times accordingly.
        /// Returns whether the adjustment was exact; if false, there might be small inaccuracies.
        /// </summary>
        /// <param name="ppq">The new PPQ value.</param>
        /// <returns>Whether the adjustment was exact.</returns>
        public bool AdjustPPQ(int ppq)
        {
            if (tickRateSMPTE)
                throw new ArgumentException("Cannot adjust PPQ of MIDI sequence that uses SMPTE timing");
            if (ppq <= 0)
                throw new ArgumentException("PPQ must be non-negative");

            int x2 = ppq, x1 = PulsesPerQuarter;
            bool exact = true;
            if (x1 == x2)
                return true;

            foreach (MIDITrack trk in Tracks)
                exact &= trk.ShiftTime(x2, x1);

            tickRateQuarterTicks = ppq;

            if (x2 > x1)
                return x2 % x1 == 0 && exact;
            else
                return x1 % x2 == 0 && exact;
        }

        /// <summary>
        /// Removes all tempo changes from this sequence and normalizes the entire song to use 120 BPM tempo.
        /// </summary>
        public void NormalizeTempo()
        {
            List<MIDITrackEvent> evts = new List<MIDITrackEvent>();
            foreach (MIDITrack trk in Tracks)
                foreach (MIDIEvent evt in trk.GetAllEvents())
                    evts.Add(new MIDITrackEvent(trk, evt));
            Util.StableSort(evts, new MIDITrackEventComparer());

            double bpm = 120;
            const double targetBpm = 120;
            ulong time = 0;
            ulong newTime = 0;
            double timeAdjusted = 0;

            foreach (MIDITrack trk in Tracks)
                trk.Clear();
            foreach (MIDITrackEvent trke in evts)
            {
                if (trke.Event.Data.Type == MIDIMessageType.SetTempo)
                    bpm = (trke.Event.Data as MIDITempoMessage).BeatsPerMinute;
                else
                {
                    newTime = trke.Event.Time;
                    if (newTime > time)
                    {
                        timeAdjusted += (newTime - time) * (targetBpm / bpm);
                        time = newTime;
                    }
                    trke.Track.AddEvent(new MIDIEvent((ulong)timeAdjusted, trke.Event.Data));
                }
            }

            foreach (MIDITrack trk in Tracks)
                trk.TerminateTrack();
        }

        /// <summary>
        /// Remaps programs/instructions on all tracks.
        /// </summary>
        /// <param name="oldProgram">The old program number; 0-127 for melodic programs, or 128-255 for percussion programs.</param>
        /// <param name="newProgram">The new program number, between 0-127.</param>
        public void RemapProgram(int oldProgram, int newProgram)
        {
            foreach (MIDITrack trk in Tracks)
                trk.RemapProgram(oldProgram, newProgram);
        }

        /// <summary>
        /// Remaps programs/instructions on all tracks.
        /// </summary>
        /// <param name="programMap">An array of 256 integers detailing the way programs should be remapped, first 128 for melodic programs
        /// and second 128 for percussion programs. The values should range between 0-127.</param>
        public void RemapProgram(int[] programMap)
        {
            foreach (MIDITrack trk in Tracks)
                trk.RemapProgram(programMap);
        }
        
        /// <summary>
        /// Loads a HMP sequence from a stream.
        /// </summary>
        /// <param name="stream">The stream to load from.</param>
        public void ReadHMP(Stream stream)
        {
            using (BinaryReaderHMP br = new BinaryReaderHMP(stream))
            {
                stream.Seek(0, SeekOrigin.Begin);
                if (Encoding.ASCII.GetString(br.ReadBytes(8)) != "HMIMIDIP")
                    throw new ArgumentException("Not a valid HMP");

                Format = MIDIFormat.HMI;

                // offsets directly derived from Chocolate Descent source
                br.BaseStream.Seek(32, SeekOrigin.Begin);
                int branchTableOffset = br.ReadInt32();

                tickRateSMPTE = false;
                br.BaseStream.Seek(48, SeekOrigin.Begin);
                int nTracks = br.ReadInt32();
                tickRateQuarterTicks = br.ReadInt32();
                int bpm = br.ReadInt32();
                int seconds = br.ReadInt32(); //for debugging, I suppose.

                Tracks.Clear();

                for (int i = 0; i < 16; ++i)
                    HMPChannelPriorities[i] = Util.Clamp(br.ReadInt32(), HMPConstants.HMI_PRIORITY_MAXIMUM, HMPConstants.HMI_PRIORITY_MINIMUM);
                List<HMPValidDevice> deviceMappings = new List<HMPValidDevice>();
                for (int i = 0; i < 32; ++i)
                {
                    HMPValidDevice mapping = HMPValidDevice.Default;

                    for (int j = 0; j < 5; ++j)
                    {
                        switch (br.ReadInt32())
                        {
                            case HMPConstants.HMI_MIDI_DEVICE_MIDI:
                                mapping |= HMPValidDevice.MIDI;
                                break;
                            case HMPConstants.HMI_MIDI_DEVICE_GUS:
                                mapping |= HMPValidDevice.GUS;
                                break;
                            case HMPConstants.HMI_MIDI_DEVICE_FM:
                                mapping |= HMPValidDevice.FM;
                                break;
                            case HMPConstants.HMI_MIDI_DEVICE_WAVETABLE:
                                mapping |= HMPValidDevice.Wavetable;
                                break;
                        }
                    }

                    deviceMappings.Add(mapping);
                }

                // two unused values
                br.ReadInt32();
                br.ReadInt32();

                for (int i = 0; i < nTracks; ++i)
                {
                    MIDITrack trk = new MIDITrack();
                    int chunkNum = br.ReadInt32();
                    int trackLength = br.ReadInt32();
                    trackLength -= 12; //track length in HMP includes the header, for some reason.
                    int trackNum = br.ReadInt32(); //descent2.com docs imply this is needed for loops (must be on track 1), but it's unclear if it's actually true from observation.
                    byte[] trackData = br.ReadBytes(trackLength); 
                    if (trackData.Length < trackLength)
                        throw new EndOfStreamException();

                    if (bpm != 120 && i == 0) // add tempo event to first track
                        trk.AddEvent(new MIDIEvent(0, new MIDITempoMessage(-1, (double)bpm)));

                    ReadHMPTrack(i, trk, trackData);
                    trk.HMPDevices = i >= 32 ? HMPValidDevice.Default : deviceMappings[i];
                    Tracks.Add(trk);
                }

                br.BaseStream.Seek(branchTableOffset, SeekOrigin.Begin);
                byte[] branchesPerTrack = br.ReadBytes(nTracks);
                // skip branch data if invalid
                if (branchesPerTrack.Length >= nTracks && branchesPerTrack.Max() < 128)
                {
                    for (int i = 0; i < nTracks; ++i)
                    {
                        for (int j = 0; j < branchesPerTrack[i]; ++j)
                        {
                            int offset = br.ReadInt32();
                            byte branchID = br.ReadByte();
                            byte program = br.ReadByte();
                            byte loopCount = br.ReadByte();
                            byte controlChangeCount = br.ReadByte();
                            int controlChangeOffset = br.ReadInt32();
                            br.ReadInt32(); // another internal value, this time usually identical to the previous
                            br.ReadInt32(); // two unused dwords
                            br.ReadInt32();
                            List<HMPBranchControlChange> controlChanges = new List<HMPBranchControlChange>();
                            if (controlChangeCount > 0)
                            {
                                long savedPosition = br.BaseStream.Position;
                                br.BaseStream.Position = controlChangeOffset;
                                for (int k = 0; k < controlChangeCount; k += 2)
                                {
                                    byte[] control = br.ReadBytes(2);
                                    if (control.Length < 2) break;

                                    controlChanges.Add(new HMPBranchControlChange((MIDIControl)(control[0] & 127), (byte)(control[1] & 127)));
                                }
                                br.BaseStream.Position = savedPosition;
                            }
                            Tracks[i].HMPBranchPoints.Add(new HMPBranchPoint(offset, program, loopCount, branchID, controlChanges));
                        }
                    }
                }
            }
        }

        private void ReadMIDITrack(int trackNum, MIDITrack trk, byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReaderMIDI br = new BinaryReaderMIDI(ms))
            {
                ReadMIDITrackInternal(trackNum, trk, br);
            }
        }

        private void ReadHMPTrack(int trackNum, MIDITrack trk, byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
            using (BinaryReaderHMP br = new BinaryReaderHMP(ms))
            {
                ReadMIDITrackInternal(trackNum, trk, br);
            }
        }

        private void ReadMIDITrackInternal(int trackNum, MIDITrack trk, IMIDIReader br)
        {
            byte status = 0;
            ulong position = 0;
            ulong delta;
            MIDIMessage evt;
            int metaChannel = -1;
            while (ReadMIDIMessage(trackNum, br, ref status, ref metaChannel, out delta, out evt))
            {
                position += delta;
                if (evt != null)
                    trk.AddEvent(new MIDIEvent(position, evt));
            }
            if (evt != null)
                trk.AddEvent(new MIDIEvent(position + delta, evt));
        }

        private static readonly MIDIMessageType[] midiMetaTypes = new MIDIMessageType[]
        {
            0,
            MIDIMessageType.MetaText,
            MIDIMessageType.MetaCopyright,
            MIDIMessageType.MetaTrackName,
            MIDIMessageType.MetaInstrumentName,
            MIDIMessageType.MetaLyric,
            MIDIMessageType.MetaMarker,
            MIDIMessageType.MetaCuePoint
        };

        private static readonly int[] smpteFrameCount = new int[] { 24, 25, 29, 30 };

        private bool ReadMIDIMessage(int trackNum, IMIDIReader br, ref byte status, ref int metaChannel, out ulong delta, out MIDIMessage evt)
        {
            byte tmp;
            delta = (ulong)br.ReadVLQ();
            tmp = br.ReadByte();
            if ((tmp & 0x80) == 0x80)
                status = tmp;
            else                            // rewind by one byte
                br.Rewind(1);

            int hinib = (status >> 4) & 7;
            int lonib = status & 15;
            switch (hinib)
            {
                case 0:             // NoteOff
                    evt = new MIDINoteMessage(MIDIMessageType.NoteOff, lonib, br.ReadByte(), br.ReadByte());
                    metaChannel = lonib;
                    return true;
                case 1:             // NoteOn
                    evt = new MIDINoteMessage(MIDIMessageType.NoteOn, lonib, br.ReadByte(), br.ReadByte());
                    metaChannel = lonib;
                    return true;
                case 2:             // NoteAftertouch
                    evt = new MIDINoteMessage(MIDIMessageType.NoteAftertouch, lonib, br.ReadByte(), br.ReadByte());
                    metaChannel = lonib;
                    return true;
                case 3:             // ControlChange
                    evt = new MIDIControlChangeMessage(lonib, (MIDIControl)br.ReadByte(), br.ReadByte());
                    metaChannel = lonib;
                    return true;
                case 4:             // ProgramChange
                    evt = new MIDIProgramChangeMessage(lonib, br.ReadByte());
                    metaChannel = lonib;
                    return true;
                case 5:             // ChannelAftertouch
                    evt = new MIDIChannelAftertouchMessage(lonib, br.ReadByte());
                    metaChannel = lonib;
                    return true;
                case 6:             // PitchBend
                    evt = new MIDIPitchBendMessage(lonib, br.ReadMidi14());
                    metaChannel = lonib;
                    return true;
                case 7:             // ...below...
                    break;
            }

            int seqlen;
            switch (status)
            {
                case 0xF0:          // SysEx
                    seqlen = br.ReadMetaVLQ();
                    evt = new MIDISysExMessage(metaChannel, false, br.ReadBytes(seqlen));
                    return true;
                case 0xF7:          // SysEx Continue
                    seqlen = br.ReadMetaVLQ();
                    evt = new MIDISysExMessage(metaChannel, true, br.ReadBytes(seqlen));
                    return true;
                case 0xFF:          // meta
                    byte metaType = br.ReadByte();
                    switch (metaType)
                    {
                        case 0:
                            seqlen = br.ReadMetaVLQ();
                            if (seqlen == 0)
                            {
                                evt = new MIDISequenceNumberMessage(metaChannel, trackNum);
                                return true;
                            }
                            else if (seqlen == 2)
                            {
                                evt = new MIDISequenceNumberMessage(metaChannel, br.ReadMidi14());
                                return true;
                            }
                            br.Rewind(1);
                            goto default;
                        case 1:
                        case 2:
                        case 3:
                        case 4:
                        case 5:
                        case 6:
                        case 7:
                            seqlen = br.ReadMetaVLQ();
                            evt = new MIDIMetaMessage(midiMetaTypes[metaType], metaChannel, br.ReadBytes(seqlen));
                            return true;
                        case 0x20:
                            if (br.ReadMetaVLQ() == 1)
                            {
                                int channelNum = br.ReadByte();
                                metaChannel = channelNum & 15;
                                evt = null;
                                return true;
                            }
                            br.Rewind(1);
                            goto default;
                        case 0x2F:
                            if (br.ReadMetaVLQ() == 0)
                            {
                                evt = new MIDIEndOfTrackMessage(metaChannel);
                                return false;
                            }
                            br.Rewind(1);
                            goto default;
                        case 0x51:
                            if (br.ReadMetaVLQ() == 3)
                            {
                                int tempo = 0;
                                tempo |= (br.ReadByte() << 16);
                                tempo |= (br.ReadByte() << 8);
                                tempo |= br.ReadByte();
                                if (tempo == 0)
                                    tempo = 500000;
                                evt = new MIDITempoMessage(metaChannel, tempo);
                                return true;
                            }
                            br.Rewind(1);
                            goto default;
                        case 0x54:
                            if (br.ReadMetaVLQ() == 5)
                            {
                                int hours = br.ReadByte();
                                MIDISMPTEFrameRate rate = (MIDISMPTEFrameRate)((hours >> 5) & 3);
                                hours &= 31;
                                int minutes = br.ReadByte() & 127;
                                int seconds = br.ReadByte() & 127;
                                int frames = br.ReadByte() & 127;
                                int fracFrames = br.ReadByte() & 127;

                                evt = new MIDISMPTEOffsetMessage(metaChannel, rate, hours, minutes, seconds, frames, fracFrames);
                                return true;
                            }
                            br.Rewind(1);
                            goto default;
                        case 0x58:
                            if (br.ReadMetaVLQ() == 4)
                            {
                                byte n = br.ReadByte();
                                byte d = br.ReadByte();
                                byte c = br.ReadByte();
                                byte b = br.ReadByte();
                                evt = new MIDITimeSignatureMessage(metaChannel, n, d, c, b);
                                return true;
                            }
                            br.Rewind(1);
                            goto default;
                        case 0x59:
                            if (br.ReadMetaVLQ() == 2)
                            {
                                byte sf = br.ReadByte();
                                byte mi = br.ReadByte();
                                evt = new MIDIKeySignatureMessage(metaChannel, sf, mi > 0);
                                return true;
                            }
                            br.Rewind(1);
                            goto default;
                        case 0x7F:
                            seqlen = br.ReadMetaVLQ();
                            evt = new MIDISequencerProprietaryMessage(metaChannel, br.ReadBytes(seqlen));
                            return true;
                        default:
                            // try to skip unknown meta event
                            seqlen = br.ReadMetaVLQ();
                            br.Skip(seqlen);
                            evt = null;
                            return true;
                    }
            }

            evt = null;
            return false;
        }

        /// <summary>
        /// Loads a MIDI sequence from a file.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        public void Read(string filePath)
        {
            using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                Read(fs);
            }
        }

        /// <summary>
        /// Loads a MIDI sequence from an array.
        /// </summary>
        /// <param name="contents">The array to load from.</param>
        public void Read(byte[] contents)
        {
            using (MemoryStream ms = new MemoryStream(contents))
            {
                Read(ms);
            }
        }

        /// <summary>
        /// Loads a HMP sequence from a file.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        public void ReadHMP(string filePath)
        {
            using (FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.Read))
            {
                ReadHMP(fs);
            }
        }

        /// <summary>
        /// Loads a HMP sequence from an array.
        /// </summary>
        /// <param name="contents">The array to load from.</param>
        public void ReadHMP(byte[] contents)
        {
            using (MemoryStream ms = new MemoryStream(contents))
            {
                ReadHMP(ms);
            }
        }

        private void WriteMIDITrackInternal(IMIDIWriter bw, MIDIWriteOptions options, int trackNum, MIDITrack trk)
        {
            trk.TerminateTrack();
            ulong position = 0;
            ulong delta;
            int metaChannel = -1;
            byte status = 0;
            foreach (MIDIEvent evt in trk)
            {
                if (options.HasFlag(MIDIWriteOptions.ExplicitStatus))
                    status = 0;
                delta = evt.Time - position;
                position = evt.Time;
                bw.WriteVLQ((int)delta);
                WriteMIDIMessage(trackNum, bw, evt.Data, !options.HasFlag(MIDIWriteOptions.DoNotWriteMetaChannel), ref status, ref metaChannel);
            }
        }

        private byte[] WriteMIDITrack(MIDIWriteOptions options, int trackNum, MIDITrack trk)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriterMIDI bw = new BinaryWriterMIDI(ms))
            {
                WriteMIDITrackInternal(bw, options, trackNum, trk);
                return ms.ToArray();
            }
        }

        private byte[] WriteHMPTrack(MIDIWriteOptions options, int trackNum, MIDITrack trk)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriterHMP bw = new BinaryWriterHMP(ms))
            {
                WriteMIDITrackInternal(bw, options | MIDIWriteOptions.ExplicitStatus | MIDIWriteOptions.DoNotWriteMetaChannel, trackNum, trk);
                return ms.ToArray();
            }
        }

        private double GetDurationInSeconds(out double initialBpm)
        {
            double duration = 0;
            double bpm = 120;
            ulong position = 0;
            initialBpm = 0;

            List<MIDIEvent> events = new List<MIDIEvent>();
            foreach (MIDITrack trk in Tracks)
                events.AddRange(trk.GetAllEvents());
            events.Sort(new MIDIEventComparer());

            foreach (MIDIEvent evt in events)
            {
                if (evt.Time > position)
                {
                    duration += (evt.Time - position) * 60 / (bpm * tickRateQuarterTicks);
                    position = evt.Time;
                }

                if (evt.Data is MIDITempoMessage tempo)
                {
                    bpm = tempo.BeatsPerMinute;
                    if (initialBpm == 0)
                        initialBpm = bpm;
                }
            }

            if (initialBpm == 0)
                initialBpm = bpm;

            return duration;
        }

        /// <summary>
        /// Writes a MIDI sequence to a stream.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="options">The options for writing this sequence.</param>
        public void Write(Stream stream, MIDIWriteOptions options)
        {
            int trackNum = 0;
            if (Format == MIDIFormat.HMI)
            {
                if (tickRateSMPTE)
                    throw new ArgumentException("SMPTE time base is not supported on HMP");
                if (trackNum > 32)
                    throw new ArgumentException("HMP cannot take more than 32 tracks!");
                if (Tracks.Select(t => t.HMPBranchPoints.Count).Max() >= 128)
                    throw new ArgumentException("Too many HMP branch points!");

                using (BinaryWriterHMP bw = new BinaryWriterHMP(stream))
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    bw.Write(Encoding.ASCII.GetBytes("HMIMIDIP"));
                    for (int i = 8; i < 32; ++i)
                        bw.Write((byte)0);
                    long fileSizePosition = stream.Position;
                    bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
                    bw.Write(Tracks.Count);
                    bw.Write(tickRateQuarterTicks);
                    double bpm;
                    int duration = (int)GetDurationInSeconds(out bpm);
                    bw.Write((int)bpm);
                    bw.Write(duration);

                    for (int i = 0; i < 16; ++i)
                        bw.Write(HMPChannelPriorities[i]);

                    // device map information. every track has a list of devices that they will be
                    // sent out to, but if left empty, all devices are considered valid.
                    // (it is possible that Descent cares little about this information; it is the
                    // responsibility of the HMI driver to do that, but it may still choose to play
                    // the track on any device.)
                    Tracks[0].HMPDevices = HMPValidDevice.Default;
                    for (int i = 0; i < TrackCount; ++i)
                    {
                        MIDITrack trk = Tracks[i];
                        int writtenDev = 0;

                        if (trk.HMPDevices.HasFlag(HMPValidDevice.MIDI))
                        {
                            bw.Write(HMPConstants.HMI_MIDI_DEVICE_MIDI);
                            ++writtenDev;
                        }
                        if (trk.HMPDevices.HasFlag(HMPValidDevice.GUS))
                        {
                            bw.Write(HMPConstants.HMI_MIDI_DEVICE_GUS);
                            ++writtenDev;
                        }
                        if (trk.HMPDevices.HasFlag(HMPValidDevice.FM))
                        {
                            bw.Write(HMPConstants.HMI_MIDI_DEVICE_FM);
                            ++writtenDev;
                        }
                        if (trk.HMPDevices.HasFlag(HMPValidDevice.Wavetable))
                        {
                            bw.Write(HMPConstants.HMI_MIDI_DEVICE_WAVETABLE);
                            ++writtenDev;
                        }

                        for (int j = writtenDev; j < 5; ++j)
                            bw.Write(0);
                    }
                    for (int i = TrackCount; i < 32; ++i)
                    {
                        for (int j = 0; j < 5; ++j)
                            bw.Write(0);
                    }

                    // two reserved values
                    bw.Write(0);
                    bw.Write(0);

                    int chunkNum = 0;
                    foreach (MIDITrack track in Tracks)
                    {
                        track.TerminateTrack();
                        byte[] trackData = WriteHMPTrack(options, trackNum, track);
                        // track number
                        bw.Write(chunkNum++);
                        // track length (incl. header)
                        bw.Write(trackData.Length + 12);
                        // channel number
                        MIDINoteMessage msg = track.GetAllEvents().Select(m => m.Data).OfType<MIDINoteMessage>().FirstOrDefault();
                        int ch = 0;
                        if (msg != null && msg.Channel >= 0)
                            ch = msg.Channel;
                        bw.Write(ch);
                        bw.Write(trackData);
                    }

                    int branchTableOffset = (int)stream.Position;
                    stream.Position = fileSizePosition;
                    bw.Write(branchTableOffset);

                    // write branch table
                    stream.Position = branchTableOffset;
                    for (int i = 0; i < TrackCount; ++i)
                        bw.Write((byte)(Tracks[i].HMPBranchPoints.Count));

                    Dictionary<HMPBranchPoint, Int64> controlChangePtrOffsets = new Dictionary<HMPBranchPoint, long>();
                    foreach (MIDITrack trk in Tracks)
                    {
                        foreach (HMPBranchPoint brp in trk.HMPBranchPoints)
                        {
                            if (brp.ControlChanges.Count > 127)
                                throw new ArgumentException("Too many HMP control changes for branch point!");

                            bw.Write(brp.Offset);
                            bw.Write(brp.BranchID);
                            bw.Write(brp.Program);
                            bw.Write(brp.LoopCount);
                            bw.Write((byte)(brp.ControlChanges.Count * 2));
                            bw.Flush();
                            controlChangePtrOffsets[brp] = bw.BaseStream.Position;
                            bw.Write(0); // to be filled later
                            bw.Write(0); // to be filled later
                            bw.Write(0); // unused value
                            bw.Write(0); // unused value
                        }
                    }

                    // write branch table associated control changes
                    foreach (MIDITrack trk in Tracks)
                    {
                        foreach (HMPBranchPoint brp in trk.HMPBranchPoints)
                        {
                            long myOffset = bw.BaseStream.Position;
                            bw.BaseStream.Position = controlChangePtrOffsets[brp];
                            bw.Write((int)(myOffset));
                            bw.Write((int)(myOffset)); // second value presumably only used internally, but should match the first
                            bw.BaseStream.Position = myOffset;

                            foreach (HMPBranchControlChange cc in brp.ControlChanges)
                            {
                                bw.Write((byte)cc.Controller);
                                bw.Write(cc.Value);
                            }

                            bw.Flush();
                        }
                    }
                }
            }
            else
            {
                if (Format == MIDIFormat.Type0 && TrackCount > 1)
                {
                    throw new ArgumentException("Cannot save Type-0 MIDI with multiple tracks; merge tracks or save as Type-1");
                }

                using (BinaryWriterMIDI bw = new BinaryWriterMIDI(stream))
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    bw.Write(Encoding.ASCII.GetBytes("MThd"));
                    bw.Write(6);
                    bw.Write((short)Format);
                    bw.Write((short)TrackCount);
                    if (tickRateSMPTE)
                        bw.Write((short)((((-smpteFrameCount[(int)tickRateFrames]) << 8)) | tickRateFrameTicks));
                    else
                        bw.Write((short)(tickRateQuarterTicks));

                    foreach (MIDITrack track in Tracks)
                    {
                        bw.Write(Encoding.ASCII.GetBytes("MTrk"));
                        byte[] trackData = WriteMIDITrack(options, trackNum++, track);
                        bw.Write(trackData.Length);
                        bw.Write(trackData);
                    }
                }
            }
        }

        /// <summary>
        /// Writes a MIDI sequence from a file.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <param name="options">The options for writing this sequence.</param>
        public void Write(string filePath, MIDIWriteOptions options)
        {
            using (FileStream fs = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                Write(fs, options);
            }
        }

        /// <summary>
        /// Writes a MIDI sequence from an array.
        /// </summary>
        /// <param name="contents">The array to load from.</param>
        /// <param name="options">The options for writing this sequence.</param>
        /// <returns>The MIDI file as a byte array.</returns>
        public byte[] Write(MIDIWriteOptions options)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                Write(ms, options);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Writes a MIDI sequence to a stream.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        public void Write(Stream stream)
        {
            Write(stream, MIDIWriteOptions.None);
        }

        /// <summary>
        /// Writes a MIDI sequence from a file.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        public void Write(string filePath)
        {
            Write(filePath, MIDIWriteOptions.None);
        }

        /// <summary>
        /// Writes a MIDI sequence from an array.
        /// </summary>
        /// <param name="contents">The array to load from.</param>
        /// <returns>The MIDI file as a byte array.</returns>
        public byte[] Write()
        {
            return Write(MIDIWriteOptions.None);
        }

        private void WriteMIDIMessage(int trackNum, IMIDIWriter bw, MIDIMessage message, bool writeMetaChannel, ref byte status, ref int metaChannel)
        {
            byte newStatus;
            switch (message.Type)
            {
                case MIDIMessageType.NoteOff:
                    metaChannel = message.Channel;
                    newStatus = (byte)(0x80 | message.Channel);
                    if (newStatus != status)
                        bw.Write(status = newStatus);
                    bw.Write((byte)((message as MIDINoteMessage).Key));
                    bw.Write((byte)((message as MIDINoteMessage).Velocity));
                    return;
                case MIDIMessageType.NoteOn:
                    metaChannel = message.Channel;
                    newStatus = (byte)(0x90 | message.Channel);
                    if (newStatus != status)
                        bw.Write(status = newStatus);
                    bw.Write((byte)((message as MIDINoteMessage).Key));
                    bw.Write((byte)((message as MIDINoteMessage).Velocity));
                    return;
                case MIDIMessageType.NoteAftertouch:
                    metaChannel = message.Channel;
                    newStatus = (byte)(0xA0 | message.Channel);
                    if (newStatus != status)
                        bw.Write(status = newStatus);
                    bw.Write((byte)((message as MIDINoteMessage).Key));
                    bw.Write((byte)((message as MIDINoteMessage).Velocity));
                    return;
                case MIDIMessageType.ControlChange:
                    metaChannel = message.Channel;
                    newStatus = (byte)(0xB0 | message.Channel);
                    if (newStatus != status)
                        bw.Write(status = newStatus);
                    bw.Write((byte)((message as MIDIControlChangeMessage).Controller));
                    bw.Write((byte)((message as MIDIControlChangeMessage).Value));
                    return;
                case MIDIMessageType.ProgramChange:
                    metaChannel = message.Channel;
                    newStatus = (byte)(0xC0 | message.Channel);
                    if (newStatus != status)
                        bw.Write(status = newStatus);
                    bw.Write((byte)((message as MIDIProgramChangeMessage).Program));
                    return;
                case MIDIMessageType.ChannelAftertouch:
                    metaChannel = message.Channel;
                    newStatus = (byte)(0xD0 | message.Channel);
                    if (newStatus != status)
                        bw.Write(status = newStatus);
                    bw.Write((byte)((message as MIDIChannelAftertouchMessage).Value));
                    return;
                case MIDIMessageType.PitchBend:
                    metaChannel = message.Channel;
                    newStatus = (byte)(0xE0 | message.Channel);
                    if (newStatus != status)
                        bw.Write(status = newStatus);
                    bw.WriteMidi14((message as MIDIPitchBendMessage).Pitch);
                    return;
                case MIDIMessageType.EndOfTrack:
                    bw.Write(status = (byte)0xFF);
                    bw.Write((byte)0x2F);
                    bw.WriteMetaVLQ(0);
                    return;
            }

            // metadata event
            int msgChannel = message.Channel;
            if (writeMetaChannel && trackNum > 0 && msgChannel >= 0 && msgChannel != metaChannel)
            {
                bw.Write((byte)0xFF);
                bw.Write((byte)0x20);
                bw.WriteMetaVLQ(1);
                bw.Write((byte)msgChannel);
                metaChannel = msgChannel;
            }

            switch (message.Type)
            {
                case MIDIMessageType.SysEx:
                    var sysex = message as MIDISysExMessage;
                    bw.Write(status = (byte)(sysex.Continue ? 0xF7 : 0xF0));
                    bw.WriteMetaVLQ(sysex.Message.Length);
                    bw.Write(sysex.Message);
                    return;
                case MIDIMessageType.SequenceNumber:
                    var seqnex = message as MIDISequenceNumberMessage;
                    bw.Write(status = (byte)0xFF);
                    if (seqnex.Sequence == trackNum)
                        bw.WriteMetaVLQ(0);
                    {
                        bw.WriteMetaVLQ(2);
                        bw.WriteMidi14((short)seqnex.Sequence);
                    }
                    return;
                case MIDIMessageType.MetaText:
                case MIDIMessageType.MetaCopyright:
                case MIDIMessageType.MetaTrackName:
                case MIDIMessageType.MetaInstrumentName:
                case MIDIMessageType.MetaLyric:
                case MIDIMessageType.MetaMarker:
                case MIDIMessageType.MetaCuePoint:
                    var meta = message as MIDIMetaMessage;
                    bw.Write(status = (byte)0xFF);
                    bw.Write((byte)(midiMetaTypes.IndexOf(message.Type)));
                    bw.WriteMetaVLQ(meta.Data.Length);
                    bw.Write(meta.Data);
                    return;
                case MIDIMessageType.SetTempo:
                    var tempo = message as MIDITempoMessage;
                    bw.Write(status = (byte)0xFF);
                    bw.Write((byte)0x51);
                    bw.WriteMetaVLQ(3);
                    bw.Write((byte)((tempo.Tempo >> 16) & 0xFF));
                    bw.Write((byte)((tempo.Tempo >> 8) & 0xFF));
                    bw.Write((byte)(tempo.Tempo & 0xFF));
                    return;
                case MIDIMessageType.SMPTEOffset:
                    var smpte = message as MIDISMPTEOffsetMessage;
                    bw.Write(status = (byte)0xFF);
                    bw.Write((byte)0x54);
                    bw.WriteMetaVLQ(5);
                    bw.Write((byte)((((int)smpte.FrameRate) << 5) | smpte.Hours));
                    bw.Write((byte)(smpte.Minutes));
                    bw.Write((byte)(smpte.Seconds));
                    bw.Write((byte)(smpte.Frames));
                    bw.Write((byte)(smpte.FractionalFrames));
                    return;
                case MIDIMessageType.TimeSignature:
                    var ts = message as MIDITimeSignatureMessage;
                    bw.Write(status = (byte)0xFF);
                    bw.Write((byte)0x58);
                    bw.WriteMetaVLQ(4);
                    bw.Write((byte)(ts.Numerator));
                    bw.Write((byte)(ts.DenomLog2));
                    bw.Write((byte)(ts.MetronomeClocks));
                    bw.Write((byte)(ts.NotatedQuarterTicks));
                    return;
                case MIDIMessageType.KeySignature:
                    var ks = message as MIDIKeySignatureMessage;
                    bw.Write(status = (byte)0xFF);
                    bw.Write((byte)0x59);
                    bw.WriteMetaVLQ(2);
                    bw.Write((byte)(ks.SharpsFlats));
                    bw.Write((byte)(ks.Minor ? 1 : 0));
                    return;
                case MIDIMessageType.SequencerProprietary:
                    var seqp = message as MIDISequencerProprietaryMessage;
                    bw.Write(status = (byte)0xFF);
                    bw.Write((byte)0x7F);
                    bw.WriteMetaVLQ(seqp.Data.Length);
                    bw.Write(seqp.Data);
                    return;
            }
        }
    }

    /// <summary>
    /// Represents a MIDI track.
    /// </summary>
    public class MIDITrack : IEnumerable<MIDIEvent>
    {
        /// <summary>
        /// The devices this track will be played on˔ Only applies to HMI/HMP files, and even
        /// then not for the first track.
        /// </summary>
        public HMPValidDevice HMPDevices;
        /// <summary>
        /// The HMP branch points for this track. Not used in MIDI files.
        /// </summary>
        public List<HMPBranchPoint> HMPBranchPoints;

        internal SortedSet<MIDIInstant> tree;
        internal Dictionary<UInt64, MIDIInstant> idict;

        public MIDITrack()
        {
            tree = new SortedSet<MIDIInstant>(new MIDIInstantComparer());
            idict = new Dictionary<ulong, MIDIInstant>();
            HMPDevices = HMPValidDevice.All;
            HMPBranchPoints = new List<HMPBranchPoint>();
        }

        IEnumerator<MIDIEvent> IEnumerable<MIDIEvent>.GetEnumerator()
        {
            return new MIDITrackEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new MIDITrackEnumerator(this);
        }

        /// <summary>
        /// Returns an enumerator that iterates over relative events (instead of the time
        /// since the beginning of the track, the time will be from the last event).
        /// </summary>
        /// <returns>An enumerator that can be used to iterate over relative-time events.</returns>
        public IEnumerator<MIDIEventRelative> GetRelativeEnumerator()
        {
            return new MIDITrackDeltaEnumerator(this);
        }

        /// <summary>
        /// Gets all MIDI events that happen at a given point in time.
        /// </summary>
        /// <param name="time">The point in time, in ticks from the beginning of the track.</param>
        /// <returns>The set of MIDI events occurring at that time.</returns>
        public ICollection<MIDIEvent> GetEventsAt(ulong time)
        {
            List<MIDIEvent> events = new List<MIDIEvent>();
            if (idict.ContainsKey(time))
            {
                foreach (MIDIMessage msg in idict[time].Messages)
                    events.Add(new MIDIEvent(time, msg));
            }
            return events;
        }

        /// <summary>
        /// Gets all MIDI events that occur between two given points in time.
        /// </summary>
        /// <param name="start">The earliest time for which events should be returned.</param>
        /// <param name="end">The latest time for which events should be returned.</param>
        /// <returns>The set of MIDI events occurring between the two given points in time.</returns>
        public ICollection<MIDIEvent> GetEventsBetween(ulong start, ulong end)
        {
            MIDIInstant ghostStart = new MIDIInstant(start);
            MIDIInstant ghostEnd = new MIDIInstant(end);
            List<MIDIEvent> events = new List<MIDIEvent>();
            foreach (MIDIInstant point in tree.GetViewBetween(ghostStart, ghostEnd))
                foreach (MIDIMessage msg in point.Messages)
                    events.Add(new MIDIEvent(point.Time, msg));
            return events;
        }

        /// <summary>
        /// Gets all events in this MIDI track, in order.
        /// </summary>
        /// <returns>The set of all MIDI events on this track.</returns>
        public IList<MIDIEvent> GetAllEvents()
        { 
            List<MIDIEvent> events = new List<MIDIEvent>();
            foreach (MIDIInstant point in tree)
                foreach (MIDIMessage msg in point.Messages)
                    events.Add(new MIDIEvent(point.Time, msg));
            return events;
        }

        /// <summary>
        /// Gets the total number of events on this track.
        /// </summary>
        public int EventCount => tree.Select(p => p.Messages.Count).DefaultIfEmpty(0).Sum();

        /// <summary>
        /// The amount of ticks until the end of the track.
        /// </summary>
        public ulong Duration => tree.Max.Time;

        /// <summary>
        /// Adds a new event onto this MIDI track.
        /// </summary>
        /// <param name="evt">The MIDI event to add.</param>
        public void AddEvent(MIDIEvent evt)
        {
            AddEvent(evt, false);
        }

        /// <summary>
        /// Adds a new event onto this MIDI track.
        /// </summary>
        /// <param name="evt">The MIDI event to add.</param>
        /// <param name="addToBeginning">If events already exist at that time, determines whether the
        /// event should be added as the first event of that point in time, as opposed to being added
        /// as the last one.</param>
        public void AddEvent(MIDIEvent evt, bool addToBeginning)
        {
            if (!idict.ContainsKey(evt.Time))
            {
                MIDIInstant point = new MIDIInstant(evt.Time);
                point.Messages = new List<MIDIMessage>();
                idict[evt.Time] = point;
                tree.Add(point);
            }
            if (addToBeginning)
                idict[evt.Time].Messages.Insert(0, evt.Data);
            else
                idict[evt.Time].Messages.Add(evt.Data);
        }

        /// <summary>
        /// Moves an event from this MIDI track to a new time, and makes it the first event on that point in time.
        /// </summary>
        /// <param name="newTime">The new time to move to, in MIDI ticks from the beginning of the track.</param>
        /// <param name="evt">The event to move.</param>
        /// <returns>Whether the event was moved.</returns>
        public bool MoveEvent(ulong newTime, MIDIEvent evt)
        {
            if (evt.Time == newTime)
            {
                if (!idict.ContainsKey(evt.Time))
                    return false;
                MIDIInstant instant = idict[evt.Time];
                if (!instant.Messages.Remove(evt.Data))
                    return false;
                instant.Messages.Insert(0, evt.Data);
                return true;
            }
            if (RemoveEvent(evt))
            {
                evt.Time = newTime;
                AddEvent(evt);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes an event from this MIDI track.
        /// </summary>
        /// <param name="evt">The event to remove.</param>
        /// <returns>Whether the event was removed from this track.</returns>
        public bool RemoveEvent(MIDIEvent evt)
        {
            ulong time = evt.Time;
            if (!idict.ContainsKey(time))
                return false;
            return idict[time].Messages.Remove(evt.Data);
        }

        /// <summary>
        /// Ensures there is only one end-of-track event and that it is the final event on the track.
        /// </summary>
        public void TerminateTrack()
        {
            List<MIDIEvent> evts = new List<MIDIEvent>();
            evts.AddRange(GetAllEvents());
            for (int i = 0; i < evts.Count - 1; ++i)
            {
                if (evts[i].Data.Type == MIDIMessageType.EndOfTrack)
                {
                    // remove all end-of-track events
                    foreach (MIDIInstant instant in tree)
                    {
                        instant.Messages.RemoveAll(m => m.Type == MIDIMessageType.EndOfTrack);
                    }
                    break;
                }
            }
            if (evts.Count < 1 || evts.Last().Data.Type != MIDIMessageType.EndOfTrack)
                AddEvent(new MIDIEvent(evts.Count < 1 ? 0 : tree.Max.Time, new MIDIEndOfTrackMessage(-1)));
        }

        /// <summary>
        /// Removes all MIDI events that match the given filter predicate.
        /// </summary>
        /// <param name="messageFilter">The filter that should return true if the event should be deleted.</param>
        public void RemoveEvents(Predicate<MIDIEvent> eventFilter)
        {
            List<MIDIEvent> removable = GetAllEvents().Where(e => eventFilter(e)).ToList();
            foreach (MIDIEvent evt in removable)
                RemoveEvent(evt);
        }

        /// <summary>
        /// Removes all MIDI messages that match the given filter predicate.
        /// </summary>
        /// <param name="messageFilter">The filter that should return true if the message should be deleted.</param>
        public void RemoveMessages(Predicate<MIDIMessage> messageFilter)
        {
            List<MIDIEvent> removable = GetAllEvents().Where(e => messageFilter(e.Data)).ToList();
            foreach (MIDIEvent evt in removable)
                RemoveEvent(evt);
        }

        /// <summary>
        /// Remaps programs/instructions on this track.
        /// </summary>
        /// <param name="oldProgram">The old program number; 0-127 for melodic programs, or 128-255 for percussion programs.</param>
        /// <param name="newProgram">The new program number, between 0-127.</param>
        public void RemapProgram(int oldProgram, int newProgram)
        {
            foreach (MIDIMessage msg in GetAllEvents().Select(e => e.Data).Where(m => !m.IsExtendedEvent))
            {
                bool percussion = msg.Channel == 9;
                if (percussion && msg is MIDINoteMessage nmsg && nmsg.Key == (oldProgram & 127))
                    nmsg.Key = (byte)(newProgram & 127);
                else if (!percussion && msg is MIDIProgramChangeMessage pmsg && pmsg.Program == (oldProgram & 127))
                    pmsg.Program = (byte)(newProgram & 127);
            }
        }

        /// <summary>
        /// Remaps programs/instructions on this track.
        /// </summary>
        /// <param name="programMap">An array of 256 integers detailing the way programs should be remapped, first 128 for melodic programs
        /// and second 128 for percussion programs. The values should range between 0-127.</param>
        public void RemapProgram(int[] programMap)
        {
            if (programMap.Length != 256)
                throw new ArgumentException("program remap array must be 256 items long");
            foreach (MIDIMessage msg in GetAllEvents().Select(e => e.Data).Where(m => !m.IsExtendedEvent))
            {
                bool percussion = msg.Channel == 9;
                if (percussion && msg is MIDINoteMessage nmsg)
                    nmsg.Key = (byte)(programMap[128 | (nmsg.Key & 127)] & 127);
                else if (!percussion && msg is MIDIProgramChangeMessage pmsg)
                    pmsg.Program = (byte)(programMap[pmsg.Program & 127] & 127);
            }
        }

        internal bool ShiftTime(int n, int d)
        {
            bool exact = true;
            List<MIDIInstant> instants = new List<MIDIInstant>();
            instants.AddRange(tree);
            tree.Clear();
            idict.Clear();

            foreach (MIDIInstant instant in instants)
            {
                ulong newTime = instant.Time * (ulong)n;
                exact &= (newTime % (ulong)d) == 0;
                newTime /= (ulong)d;
                if (idict.ContainsKey(newTime))
                {
                    idict[newTime].Messages.AddRange(instant.Messages);
                }
                else
                {
                    instant.Time = newTime;
                    tree.Add(instant);
                    idict[newTime] = instant;
                }
            }

            return exact;
        }

        /// <summary>
        /// Removes all events from this track.
        /// </summary>
        public void Clear()
        {
            tree.Clear();
            idict.Clear();
        }

        /// <summary>
        /// An enumerator that is used to iterate over all of the events in a track in order,
        /// with the events containing the associated message and the point in time measured in
        /// MIDI ticks from the beginning of the track.
        /// </summary>
        public class MIDITrackEnumerator : IEnumerator<MIDIEvent>
        {
            private MIDITrack track;
            private IEnumerator<MIDIInstant> ienum;
            private int listIndex = 0;
            private int listLength = 0;
            private MIDIEvent ed;

            public MIDITrackEnumerator(MIDITrack track)
            {
                this.track = track;
                this.ienum = track.tree.GetEnumerator();
                Reset();
            }

            public MIDIEvent Current => ed;
            object IEnumerator.Current => ed;

            public bool MoveNext()
            {
                while (listIndex >= listLength)
                {
                    if (!ienum.MoveNext())
                        return false;
                    listIndex = 0;
                    listLength = ienum.Current.Messages.Count;
                }
                ed = new MIDIEvent(ienum.Current.Time, ienum.Current.Messages[listIndex++]);
                return true;
            }

            public void Dispose()
            {
                ienum.Dispose();
            }

            public void Reset()
            {
                ienum.Reset();
                listIndex = 0;
                listLength = 0;
            }
        }

        /// <summary>
        /// An enumerator that is used to iterate over all of the events in a track in order,
        /// with the events containing the associated message and the point in time measured in
        /// MIDI ticks from the last event.
        /// </summary>
        public class MIDITrackDeltaEnumerator : IEnumerator<MIDIEventRelative>
        {
            private MIDITrack track;
            private IEnumerator<MIDIInstant> ienum;
            private int listIndex = 0;
            private int listLength = 0;
            private MIDIEventRelative ed;

            public MIDITrackDeltaEnumerator(MIDITrack track)
            {
                this.track = track;
                this.ienum = track.tree.GetEnumerator();
                Reset();
            }

            public MIDIEventRelative Current => ed;
            object IEnumerator.Current => ed;

            public bool MoveNext()
            {
                ulong start = ienum.Current.Time;
                ulong delta;
                while (listIndex >= listLength)
                {
                    if (!ienum.MoveNext())
                        return false;
                    listIndex = 0;
                    listLength = ienum.Current.Messages.Count;
                }
                delta = ienum.Current.Time - start;
                ed = new MIDIEventRelative(delta, ienum.Current.Messages[listIndex++]);
                return true;
            }

            public void Dispose()
            {
                ienum.Dispose();
            }

            public void Reset()
            {
                ienum.Reset();
                listIndex = 0;
                listLength = 0;
            }
        }

        internal class MIDIInstantComparer : Comparer<MIDIInstant>
        {
            public override int Compare(MIDIInstant a, MIDIInstant b)
            {
                return a.Time.CompareTo(b.Time);
            }
        }
    }

    internal class MIDIEventComparer : Comparer<MIDIEvent>
    {
        public override int Compare(MIDIEvent a, MIDIEvent b)
        {
            return a.Time.CompareTo(b.Time);
        }
    }

    internal class MIDITrackEventComparer : Comparer<MIDITrackEvent>
    {
        public override int Compare(MIDITrackEvent a, MIDITrackEvent b)
        {
            return a.Event.Time.CompareTo(b.Event.Time);
        }
    }

    [Flags]
    public enum MIDIWriteOptions
    {
        /// <summary>
        /// Default options.
        /// </summary>
        None = 0,
        /// <summary>
        /// Always explicitly write status bytes, even if they are repeats of the preceding byte.
        /// </summary>
        ExplicitStatus = 1,
        /// <summary>
        /// Does not write events to change the channel for meta events.
        /// </summary>
        DoNotWriteMetaChannel = 2,
    }

    /// <summary>
    /// Represents a MIDI event and its position on the track.
    /// </summary>
    public struct MIDIEvent
    {
        /// <summary>
        /// The number of ticks since the beginning of the track.
        /// </summary>
        public ulong Time;
        /// <summary>
        /// The event itself.
        /// </summary>
        public MIDIMessage Data;

        public MIDIEvent(ulong position, MIDIMessage evt)
        {
            Time = position;
            Data = evt;
        }
    }

    internal class MIDITrackEvent
    {
        internal MIDITrack Track;
        internal MIDIEvent Event;

        internal MIDITrackEvent(MIDITrack track, MIDIEvent evt)
        {
            Track = track;
            Event = evt;
        }
    }

    /// <summary>
    /// Represents a MIDI event and the number of ticks that has elapsed since the previous event.
    /// </summary>
    public struct MIDIEventRelative
    {
        /// <summary>
        /// The number of ticks since last event.
        /// </summary>
        public ulong Interval;
        /// <summary>
        /// The event itself.
        /// </summary>
        public MIDIMessage Event;

        public MIDIEventRelative(ulong interval, MIDIMessage evt)
        {
            Interval = interval;
            Event = evt;
        }
    }

    internal class MIDIInstant
    {
        internal ulong Time;
        internal List<MIDIMessage> Messages;

        internal MIDIInstant() : this(0) { }

        internal MIDIInstant(ulong position)
        {
            Time = position;
        }

        internal MIDIInstant(ulong position, IEnumerable<MIDIMessage> messages) : this(position)
        {
            Messages = new List<MIDIMessage>();
            Messages.AddRange(messages);
        }
    }

    /// <summary>
    /// Represents the internal MIDI format.
    /// </summary>
    public enum MIDIFormat : short
    {
        /// <summary>
        /// Type 0. One track.
        /// </summary>
        Type0 = 0,
        /// <summary>
        /// Type 1. Multiple synchronous tracks.
        /// </summary>
        Type1 = 1,
        /// <summary>
        /// Type 2. Multiple consecutive tracks.
        /// </summary>
        Type2 = 2,
        /// <summary>
        /// HMI format. Modified version of Type 1 MIDI.
        /// </summary>
        HMI = 3,
    }

    /// <summary>
    /// Represents an SMPTE frame rate.
    /// </summary>
    public enum MIDISMPTEFrameRate
    {
        /// <summary>
        /// 24 frames per second.
        /// </summary>
        F24 = 0,
        /// <summary>
        /// 25 frames per second.
        /// </summary>
        F25 = 1,
        /// <summary>
        /// 29.97 frames per second.
        /// </summary>
        F30Drop = 2,
        /// <summary>
        /// 30 frames per second.
        /// </summary>
        F30 = 3
    }

    /// <summary>
    /// Common interface for MIDI and HMP readers.
    /// </summary>
    public interface IMIDIReader
    {
        byte ReadByte();
        byte[] ReadBytes(int length);
        short ReadInt16();
        int ReadInt32();
        long ReadInt64();
        int ReadVLQ();
        int ReadMetaVLQ();
        short ReadMidi14();
        /// <summary>
        /// Rewinds the stream of the reader back by a given number of bytes.
        /// </summary>
        /// <param name="bytes">The number of bytes to rewind.</param>
        void Rewind(int bytes);
        /// <summary>
        /// Skips bytes from the stream.
        /// </summary>
        /// <param name="bytes">The number of bytes to skips.</param>
        void Skip(int bytes);
    }

    /// <summary>
    /// Common interface for MIDI and HMP writers.
    /// </summary>
    public interface IMIDIWriter
    {
        void Write(byte n);
        void Write(byte[] data);
        void Write(short n);
        void Write(int n);
        void Write(long n);
        void WriteVLQ(int v);
        void WriteMetaVLQ(int v);
        void WriteMidi14(short v);
    }

    /// <summary>
    /// A BinaryReader intended for use with MIDI files. Reads big-endian data and has a method for reading MIDI VLQs (variable-length quantity).
    /// </summary>
    public class BinaryReaderMIDI : BinaryReaderBE, IMIDIReader
    {
        public BinaryReaderMIDI(Stream stream) : base(stream) { }

        /// <summary>
        /// Reads a MIDI variable length quantity (VLQ) to the current stream and advances the stream position accordingly.
        /// </summary>
        /// <returns>The value that was read from the stream.</returns>
        public int ReadVLQ()
        {
            byte b;
            int r = 0;
            do
                r = (r << 7) | ((b = ReadByte()) & 0x7F);
            while (b >= 0x80);
            return r;
        }

        /// <summary>
        /// Reads a MIDI variable length quantity (VLQ) for a metadata field length to the current stream and advances the stream position accordingly.
        /// </summary>
        /// <returns>The value that was read from the stream.</returns>
        public int ReadMetaVLQ()
        {
            return ReadVLQ();
        }

        /// <summary>
        /// Reads a 14-bit quantity to the current stream and advances the stream position by two bytes.
        /// </summary>
        /// <returns>The 14-bit quantity read from the stream.</returns>
        public short ReadMidi14()
        {
            return (short)((ReadByte() & 0x7F) | ((ReadByte() & 0x7F) << 7));
        }

        public void Rewind(int bytes)
        {
            BaseStream.Position -= bytes;
        }

        public void Skip(int bytes)
        {
            BaseStream.Position += bytes;
        }
    }

    /// <summary>
    /// A BinaryReader intended for use with HMP files. Reads little-endian data and has a method for reading HMP VLQs (variable-length quantity).
    /// </summary>
    public class BinaryReaderHMP : BinaryReader, IMIDIReader
    {
        public BinaryReaderHMP(Stream stream) : base(stream) { }

        /// <summary>
        /// Reads a HMP variable length quantity (VLQ) to the current stream and advances the stream position accordingly.
        /// </summary>
        /// <returns>The value that was read from the stream.</returns>
        public int ReadVLQ()
        {
            byte b;
            int r = 0;
            int shift = -7;
            do
                r = r | (((b = ReadByte()) & 0x7F) << (shift += 7));
            while ((b & 0x80) == 0); //HMI inverts the meaning of 0x80 in delta encodings.
            return r;
        }

        /// <summary>
        /// Reads a HMP variable length quantity (VLQ) to the current stream and advances the stream position accordingly.
        /// </summary>
        /// <returns>The value that was read from the stream.</returns>
        public int ReadMetaVLQ()
        {
            byte b;
            int r = 0;
            do
                r = (r << 7) | ((b = ReadByte()) & 0xF);
            while (b >= 0x80);
            return r;
        }

        /// <summary>
        /// Reads a 14-bit quantity to the current stream and advances the stream position by two bytes.
        /// </summary>
        /// <returns>The 14-bit quantity read from the stream.</returns>
        public short ReadMidi14()
        {
            return (short)((ReadByte() & 0x7F) | ((ReadByte() & 0x7F) << 7));
        }

        public void Rewind(int bytes)
        {
            BaseStream.Position -= bytes;
        }

        public void Skip(int bytes)
        {
            BaseStream.Position += bytes;
        }
    }

    /// <summary>
    /// A BinaryWriter intended for use with MIDI files. Writes big-endian data and has a method for writing MIDI VLQs (variable-length quantity).
    /// </summary>
    public class BinaryWriterMIDI : BinaryWriterBE, IMIDIWriter
    {
        public BinaryWriterMIDI(Stream stream) : base(stream) { }

        private byte[] vlq_buf = new byte[4];

        /// <summary>
        /// Writes a MIDI variable length quantity (VLQ) to the current stream and advances the stream position accordingly.
        /// </summary>
        /// <param name="v">The value to write.</param>
        public void WriteVLQ(int v)
        {
            if (v >= 0xFFFFFFF || v < 0)
                throw new ArgumentOutOfRangeException("n is over maximum allowed VLQ value");
            int q = 0;
            vlq_buf[q] = 0;
            while (v > 0)
            {
                vlq_buf[q++] = (byte)(v & 0x7F);
                v >>= 7;
            }
            while (--q > 0)
                Write((byte)(vlq_buf[q] | 0x80));
            Write(vlq_buf[0]);
        }

        /// <summary>
        /// Writes a HMP variable length quantity (VLQ) to the current stream for a metadata field length and advances the stream position accordingly.
        /// </summary>
        /// <param name="v">The value to write.</param>
        public void WriteMetaVLQ(int v)
        {
            WriteVLQ(v);
        }

        /// <summary>
        /// Writes a 14-bit quantity to the current stream and advances the stream position by two bytes.
        /// </summary>
        /// <param name="v">The value to write.</param>
        public void WriteMidi14(short v)
        {
            Write((byte)(v & 0x7F));
            Write((byte)((v >> 7) & 0x7F));
        }
    }

    /// <summary>
    /// A BinaryWriter intended for use with HMP files. Writes little-endian data and has a method for writing HMP VLQs (variable-length quantity).
    /// </summary>
    public class BinaryWriterHMP : BinaryWriter, IMIDIWriter
    {
        public BinaryWriterHMP(Stream stream) : base(stream) { }

        private byte[] vlq_buf = new byte[4];

        /// <summary>
        /// Writes a HMP variable length quantity (VLQ) to the current stream and advances the stream position accordingly.
        /// </summary>
        /// <param name="v">The value to write.</param>
        public void WriteVLQ(int v)
        {
            if (v >= 0xFFFFFFF || v < 0)
                throw new ArgumentOutOfRangeException("n is over maximum allowed VLQ value");
            do
                Write((byte)((v & 0x7F) | (v < 0x80 ? 0x80 : 0)));
            while ((v >>= 7) > 0);
        }

        /// <summary>
        /// Writes a HMP variable length quantity (VLQ) to the current stream for a metadata field length and advances the stream position accordingly.
        /// </summary>
        /// <param name="v">The value to write.</param>
        public void WriteMetaVLQ(int v)
        {
            if (v >= 0xFFFFFFF || v < 0)
                throw new ArgumentOutOfRangeException("n is over maximum allowed VLQ value");
            int q = 0;
            vlq_buf[q] = 0;
            while (v > 0)
            {
                vlq_buf[q++] = (byte)(v & 0x7F);
                v >>= 7;
            }
            while (--q > 0)
                Write((byte)(vlq_buf[q] | 0x80));
            Write(vlq_buf[0]);
        }

        /// <summary>
        /// Writes a 14-bit quantity to the current stream and advances the stream position by two bytes.
        /// </summary>
        /// <param name="v">The value to write.</param>
        public void WriteMidi14(short v)
        {
            Write((byte)(v & 0x7F));
            Write((byte)((v >> 7) & 0x7F));
        }
    }

    [Flags]
    public enum HMPValidDevice
    {
        /// <summary>
        /// Default device mappings.
        /// </summary>
        Default = 0,
        /// <summary>
        /// A MIDI output device.
        /// </summary>
        MIDI = 1,
        /// <summary>
        /// Any sound card using wavetable synthesis.
        /// </summary>
        Wavetable = 2,
        /// <summary>
        /// Any FM (frequency modulation) sound card, such as those using OPL2 or OPL3.
        /// </summary>
        FM = 4,
        /// <summary>
        /// Gravis Ultrasound.
        /// </summary>
        GUS = 8,
        /// <summary>
        /// Play this file on all available options.
        /// </summary>
        All = MIDI | Wavetable | FM | GUS
    }

    /// <summary>
    /// Represents a HMP branch point that is used 
    /// </summary>
    public class HMPBranchPoint
    {
        /// <summary>
        /// The offset of the branch point into track data.
        /// </summary>
        public int Offset;
        /// <summary>
        /// The program/instrument to apply when the track jumps to the branch point.
        /// </summary>
        public byte Program;
        /// <summary>
        /// The number of times the branch will be jumped to, or 0 for infinite loop.
        /// </summary>
        public byte LoopCount;
        /// <summary>
        /// The ID of this branch point.
        /// </summary>
        public byte BranchID;
        /// <summary>
        /// The control changes to apply when the track jumps to the branch point.
        /// </summary>
        public List<HMPBranchControlChange> ControlChanges;

        /// <summary>
        /// Initializes a new HMPBranchPoint instance.
        /// </summary>
        /// <param name="offset">The offset of the branch point into track data.</param>
        /// <param name="branchID">The ID of this branch point.</param>
        public HMPBranchPoint(int offset, byte program, byte loopCount, byte branchID)
        {
            Offset = offset;
            Program = program;
            LoopCount = loopCount;
            BranchID = branchID;
            ControlChanges = new List<HMPBranchControlChange>();
        }

        /// <summary>
        /// Initializes a new HMPBranchPoint instance.
        /// </summary>
        /// <param name="offset">The offset of the branch point into track data.</param>
        /// <param name="branchID">The ID of this branch point.</param>
        /// <param name="controlChanges">The control changes to apply when the track jumps to the branch point.</param>
        public HMPBranchPoint(int offset, byte program, byte loopCount, byte branchID, IEnumerable<HMPBranchControlChange> controlChanges) : this(offset, program, loopCount, branchID)
        {
            ControlChanges.AddRange(controlChanges);
        }
    }

    /// <summary>
    /// Represents a control value change that is associated with a HMP branch point.
    /// </summary>
    public class HMPBranchControlChange
    {
        /// <summary>
        /// The controller to change.
        /// </summary>
        public MIDIControl Controller;
        /// <summary>
        /// The new value to apply between 0-127.
        /// </summary>
        public byte Value;

        /// <summary>
        /// Initializes a new HMPBranchControlChange instance.
        /// </summary>
        /// <param name="controller">The controller to change.</param>
        /// <param name="value">The new value to apply between 0-127.</param>
        public HMPBranchControlChange(MIDIControl controller, byte value)
        {
            Controller = controller;
            Value = value;
        }
    }

    /// <summary>
    /// Contains constants for HMP files.
    /// </summary>
    public static class HMPConstants
    {
        /// <summary>
        /// Represents the maximum priority for a music channel.
        /// </summary>
        public const int HMI_PRIORITY_MAXIMUM = 0;
        /// <summary>
        /// Represents the minimum priority for a music channel.
        /// </summary>
        public const int HMI_PRIORITY_MINIMUM = 9;

        /// <summary>
        /// Used by Descent for MIDI output devices in HMP files.
        /// </summary>
        public const int HMI_MIDI_DEVICE_MIDI = 0xA000;
        /// <summary>
        /// Covox Sound Master II.
        /// </summary>
        public const int HMI_MIDI_DEVICE_SOUND_MASTER_II = 0xA000;
        /// <summary>
        /// Roland MPU-401, or any Standard MIDI compatible device.
        /// </summary>
        public const int HMI_MIDI_DEVICE_MPU_401 = 0xA001;
        /// <summary>
        /// Generic FM device.
        /// </summary>
        public const int HMI_MIDI_DEVICE_FM = 0xA002;
        /// <summary>
        /// Yamaha OPL2 (YM3812).
        /// </summary>
        public const int HMI_MIDI_DEVICE_OPL2 = 0xA002;
        /// <summary>
        /// Roland MT-32.
        /// </summary>
        public const int HMI_MIDI_DEVICE_MT_32 = 0xA004;
        /// <summary>
        /// The internal PC speaker.
        /// </summary>
        public const int HMI_MIDI_DEVICE_INTERNAL_SPEAKER = 0xA006;
        /// <summary>
        /// Any wavetable synthesis sound card.
        /// </summary>
        public const int HMI_MIDI_DEVICE_WAVETABLE = 0xA007;
        /// <summary>
        /// Creative Sound Blaster AWE32.
        /// </summary>
        public const int HMI_MIDI_DEVICE_AWE32 = 0xA008;
        /// <summary>
        /// Yamaha OPL3 (YMF262).
        /// </summary>
        public const int HMI_MIDI_DEVICE_OPL3 = 0xA009;
        /// <summary>
        /// Gravis Ultrasound.
        /// </summary>
        public const int HMI_MIDI_DEVICE_GUS = 0xA00A;
    }
}
