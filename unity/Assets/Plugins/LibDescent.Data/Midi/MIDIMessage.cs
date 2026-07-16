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
using System.Collections.Generic;
using System.Text;

namespace LibDescent.Data.Midi
{
    /// <summary>
    /// Represents a MIDI event.
    /// </summary>
    public abstract class MIDIMessage
    {
        /// <summary>
        /// The type of this event.
        /// </summary>
        public MIDIMessageType Type { get; set; }
        /// <summary>
        /// The channel of this event (0-15), or -1 if not applicable.
        /// </summary>
        public int Channel { get; set; }
        /// <summary>
        /// Whether this event is a system exclusive or a metadata event, as opposed to a normal MIDI event.
        /// </summary>
        public abstract bool IsExtendedEvent { get; }

        /// <summary>
        /// Initializes a new MIDIMessage instance.
        /// </summary>
        /// <param name="type">The MIDI message type.</param>
        /// <param name="channel">The channel associated with this message (0-15), or -1 if not applicable.</param>
        protected MIDIMessage(MIDIMessageType type, int channel)
        {
            Type = type;
            Channel = channel;
        }
    }

    /// <summary>
    /// Represents a MIDI NoteOn, NoteOff or NoteAftertouch event.
    /// </summary>
    public class MIDINoteMessage : MIDIMessage
    {
        /// <summary>
        /// The key or note in question. Middle C (C-3) is 60, and each octave is separated by 12.
        /// </summary>
        public int Key;
        /// <summary>
        /// The velocity of the note (0-127), if NoteOn/NoteOff, or the pressure value (0-127) if NoteAftertouch.
        /// </summary>
        public int Velocity;

        public override bool IsExtendedEvent => false;

        /// <summary>
        /// Initializes a new MIDINoteMessage instance.
        /// </summary>
        /// <param name="type">The MIDI message type. Must be either NoteOn, NoteOff or NoteAftertouch.</param>
        /// <param name="channel">The channel associated with this message (0-15).</param>
        /// <param name="key">The key or note in question. Middle C (C-3) is 60, and each octave is separated by 12.</param>
        /// <param name="velocity">The velocity of the note (0-127), if NoteOn/NoteOff, or the pressure value (0-127) if NoteAftertouch.</param>
        public MIDINoteMessage(MIDIMessageType type, int channel, int key, int velocity) : base(type, channel)
        {
            if (type != MIDIMessageType.NoteOn && type != MIDIMessageType.NoteOff && type != MIDIMessageType.NoteAftertouch)
                throw new ArgumentException("Invalid message type for MIDINoteMessage");
            if (channel < 0 || channel > 15)
                throw new ArgumentException("MIDINoteMessage must be associated with a channel 0-15");
            Key = key;
            Velocity = velocity;
        }
    }

    /// <summary>
    /// Represents a MIDI ControlChange message.
    /// </summary>
    public class MIDIControlChangeMessage : MIDIMessage
    {
        /// <summary>
        /// The controller to be adjusted.
        /// </summary>
        public MIDIControl Controller;
        /// <summary>
        /// The new raw value for the controller.
        /// </summary>
        public int Value;

        public override bool IsExtendedEvent => false;

        /// <summary>
        /// Initializes a new MIDIControlChangeMessage instance.
        /// </summary>
        /// <param name="channel">The channel associated with this message (0-15).</param>
        /// <param name="controller">The controller to be adjusted.</param>
        /// <param name="value">The new raw value for the controller.</param>
        public MIDIControlChangeMessage(int channel, MIDIControl controller, int value) : base(MIDIMessageType.ControlChange, channel)
        {
            if (channel < 0 || channel > 15)
                throw new ArgumentException("MIDIControlChangeMessage must be associated with a channel 0-15");
            Controller = controller;
            Value = value;
        }

        /// <summary>
        /// Initializes a new MIDIControlChangeMessage instance.
        /// </summary>
        /// <param name="channel">The channel associated with this message (0-15).</param>
        /// <param name="controller">The controller to be adjusted.</param>
        /// <param name="on">Whether the controller should be on or off. Used with some controllers, such as those controlling pedals.</param>
        public MIDIControlChangeMessage(int channel, MIDIControl controller, bool on) : this(channel, controller, on ? 127 : 0)
        {
        }

        /// <summary>
        /// Whether the value is on or off. Used with some controllers, such as those controlling pedals.
        /// </summary>
        public bool On => Value >= 64;
    }

    /// <summary>
    /// Represents a MIDI ProgramChange message.
    /// </summary>
    public class MIDIProgramChangeMessage : MIDIMessage
    {
        /// <summary>
        /// The program to change to.
        /// </summary>
        public byte Program;

        public override bool IsExtendedEvent => false;

        /// <summary>
        /// Initializes a new MIDIProgramChangeMessage instance.
        /// </summary>
        /// <param name="channel">The channel associated with this message (0-15).</param>
        /// <param name="program">The program to change to.</param>
        public MIDIProgramChangeMessage(int channel, byte program) : base(MIDIMessageType.ProgramChange, channel)
        {
            if (channel < 0 || channel > 15)
                throw new ArgumentException("MIDIProgramChangeMessage must be associated with a channel 0-15");
            Program = program;
        }
    }

    /// <summary>
    /// Represents a MIDI ChannelAftertouch message.
    /// </summary>
    public class MIDIChannelAftertouchMessage : MIDIMessage
    {
        /// <summary>
        /// The new pressure value.
        /// </summary>
        public byte Value;

        public override bool IsExtendedEvent => false;

        /// <summary>
        /// Initializes a new MIDIChannelAftertouchMessage instance.
        /// </summary>
        /// <param name="channel">The channel associated with this message (0-15).</param>
        /// <param name="value">The new pressure value.</param>
        public MIDIChannelAftertouchMessage(int channel, byte value) : base(MIDIMessageType.ChannelAftertouch, channel)
        {
            if (channel < 0 || channel > 15)
                throw new ArgumentException("MIDIChannelAftertouchMessage must be associated with a channel 0-15");
            Value = value;
        }
    }

    /// <summary>
    /// Represents a MIDI PitchBend message.
    /// </summary>
    public class MIDIPitchBendMessage : MIDIMessage
    {
        /// <summary>
        /// The new pitch value. 0x2000 (8192) represents normal pitch, and the value ranges from 0 to 0x3FFF (16383).
        /// </summary>
        public short Pitch;

        public override bool IsExtendedEvent => false;

        /// <summary>
        /// Initializes a new MIDIPitchBendMessage instance.
        /// </summary>
        /// <param name="channel">The channel associated with this message (0-15).</param>
        /// <param name="pitch">The new pitch value. 0x2000 (8192) represents normal pitch, and the value ranges from 0 to 0x3FFF (16383).</param>
        public MIDIPitchBendMessage(int channel, short pitch) : base(MIDIMessageType.PitchBend, channel)
        {
            if (channel < 0 || channel > 15)
                throw new ArgumentException("MIDIPitchBendMessage must be associated with a channel 0-15");
            Pitch = pitch;
        }
    }

    /// <summary>
    /// Represents a MIDI SysEx message.
    /// </summary>
    public class MIDISysExMessage : MIDIMessage
    {
        /// <summary>
        /// Whether this message continues an earlier SysEx message.
        /// </summary>
        public bool Continue;
        /// <summary>
        /// The raw message data.
        /// </summary>
        public byte[] Message;

        public override bool IsExtendedEvent => true;

        /// <summary>
        /// Initializes a new MIDISysExMessage instance.
        /// </summary>
        /// <param name="channel">The channel associated with this message (0-15), or -1 if not applicable.</param>
        /// <param name="cont">Whether this message continues an earlier SysEx message.</param>
        /// <param name="message">The raw message data.</param>
        public MIDISysExMessage(int channel, bool cont, byte[] message) : base(MIDIMessageType.SysEx, channel)
        {
            Continue = cont;
            Message = message;
        }
    }

    /// <summary>
    /// Represents a MIDI meta text or data message.
    /// </summary>
    public class MIDIMetaMessage : MIDIMessage
    {
        /// <summary>
        /// The raw text data of this message.
        /// </summary>
        public byte[] Data;

        public override bool IsExtendedEvent => true;

        /// <summary>
        /// Initializes a new MIDIMetaMessage instance.
        /// </summary>
        /// <param name="type">The MIDI message type. Should be one of the Meta types.</param>
        /// <param name="channel">The channel associated with this message (0-15), or -1 if not applicable.</param>
        /// <param name="data">The data associated with this meta event.</param>
        public MIDIMetaMessage(MIDIMessageType type, int channel, byte[] data) : base(type, channel)
        {
            Data = data;
        }
    }

    /// <summary>
    /// Represents a MIDI sequence number message.
    /// </summary>
    public class MIDISequenceNumberMessage : MIDIMessage
    {
        /// <summary>
        /// The sequence number.
        /// </summary>
        public int Sequence;

        public override bool IsExtendedEvent => true;

        /// <summary>
        /// Initializes a new MIDISequenceNumberMessage instance.
        /// </summary>
        /// <param name="type">The MIDI message type. Should be one of the Meta types.</param>
        /// <param name="channel">The channel associated with this message (0-15), or -1 if not applicable.</param>
        /// <param name="sequence">The sequence number.</param>
        public MIDISequenceNumberMessage(int channel, int sequence) : base(MIDIMessageType.SequenceNumber, channel)
        {
            Sequence = sequence;
        }
    }

    /// <summary>
    /// Represents a MIDI meta tempo message.
    /// </summary>
    public class MIDITempoMessage : MIDIMessage
    {
        /// <summary>
        /// Tempo in microseconds per MIDI quarter note (which is usually equivalent to 480 MIDI ticks).
        /// </summary>
        public int Tempo;

        public override bool IsExtendedEvent => true;

        /// <summary>
        /// Initializes a new MIDITempoMessage instance.
        /// </summary>
        /// <param name="channel">The channel associated with this message (0-15), or -1 if not applicable.</param>
        /// <param name="tempo">Tempo in microseconds per MIDI quarter note (which is usually equivalent to 480 MIDI ticks).</param>
        public MIDITempoMessage(int channel, int tempo) : base(MIDIMessageType.SetTempo, channel)
        {
            Tempo = tempo;
        }

        /// <summary>
        /// Initializes a new MIDITempoMessage instance.
        /// </summary>
        /// <param name="channel">The channel associated with this message (0-15), or -1 if not applicable.</param>
        /// <param name="bpm">Tempo in beats per minute.</param>
        public MIDITempoMessage(int channel, double bpm) : base(MIDIMessageType.SetTempo, channel)
        {
            BeatsPerMinute = bpm;
        }

        /// <summary>
        /// Tempo in beats per minute.
        /// </summary>
        public double BeatsPerMinute
        {
            get => 60000000.0 / Tempo;
            set => Tempo = (int)Math.Round(60000000.0 / value);
        }
    }

    public class MIDISMPTEOffsetMessage : MIDIMessage
    {
        /// <summary>
        /// The SMPTE frame rate for this offset.
        /// </summary>
        public MIDISMPTEFrameRate FrameRate;
        /// <summary>
        /// The hours of the offset.
        /// </summary>
        public int Hours;
        /// <summary>
        /// The minutes of the offset (0-59).
        /// </summary>
        public int Minutes;
        /// <summary>
        /// The seconds of the offset (0-59).
        /// </summary>
        public int Seconds;
        /// <summary>
        /// The frames of the offset (0-23/24/29).
        /// </summary>
        public int Frames;
        /// <summary>
        /// The hundredths of a frame of the offset (0-99).
        /// </summary>
        public int FractionalFrames;

        public override bool IsExtendedEvent => true;

        /// <summary>
        /// Initializes a new MIDISMPTEOffsetMessage instance.
        /// </summary>
        /// <param name="channel">The channel associated with this message (0-15), or -1 if not applicable.</param>
        /// <param name="rate">The SMPTE frame rate for this offset</param>
        /// <param name="hours">The hours of the offset.</param>
        /// <param name="minutes">The minutes of the offset (0-59).</param>
        /// <param name="seconds">The seconds of the offset (0-59).</param>
        /// <param name="frames">The frames of the offset (0-23/24/29).</param>
        /// <param name="fracFrames">The hundredths of a frame of the offset (0-99).</param>
        public MIDISMPTEOffsetMessage(int channel, MIDISMPTEFrameRate rate, int hours, int minutes, int seconds, int frames, int fracFrames) : base(MIDIMessageType.SMPTEOffset, channel)
        {
            FrameRate = rate;
            Hours = hours;
            Minutes = minutes;
            Seconds = seconds;
            Frames = frames;
            FractionalFrames = fracFrames;
        }
    }

    /// <summary>
    /// Represents a MIDI meta time signature message.
    /// </summary>
    public class MIDITimeSignatureMessage : MIDIMessage
    {
        /// <summary>
        /// The numerator of the time signature.
        /// </summary>
        public int Numerator;
        /// <summary>
        /// The base-two logarithm of the denominator of the time signature.
        /// </summary>
        public int DenomLog2;
        /// <summary>
        /// The number of MIDI ticks in a metronome click. Usually 24.
        /// </summary>
        public int MetronomeClocks;
        /// <summary>
        /// The number of notated 32th notes in a MIDI quarter note. Usually 8.
        /// </summary>
        public int NotatedQuarterTicks;

        public override bool IsExtendedEvent => true;

        /// <summary>
        /// Initializes a new MIDITimeSignatureMessage instance.
        /// </summary>
        /// <param name="channel">The channel associated with this message (0-15), or -1 if not applicable.</param>
        /// <param name="n">The numerator of the time signature.</param>
        /// <param name="d">The base-two logarithm of the denominator of the time signature.</param>
        /// <param name="c">The number of MIDI ticks in a metronome click. Usually 24.</param>
        /// <param name="b">The number of notated 32th notes in a MIDI quarter note. Usually 8.</param>
        public MIDITimeSignatureMessage(int channel, byte n, byte d, byte c, byte b) : base(MIDIMessageType.TimeSignature, channel)
        {
            Numerator = n;
            DenomLog2 = d;
            MetronomeClocks = c;
            NotatedQuarterTicks = b;
        }

        /// <summary>
        /// The denominator of the time signature.
        /// </summary>
        public int Denominator
        {
            get => 1 << DenomLog2;
            set
            {
                int c = 0, v = value >> 1;
                while ((v >>= 1) > 0)
                    ++c;
                DenomLog2 = c;
            }
        }
    }

    /// <summary>
    /// Represents a MIDI meta key signature message.
    /// </summary>
    public class MIDIKeySignatureMessage : MIDIMessage
    {
        /// <summary>
        /// Number of sharps (if positive) or flats (if negative).
        /// </summary>
        public int SharpsFlats;
        /// <summary>
        /// Whether the key is a minor key, as opposed to a major key.
        /// </summary>
        public bool Minor;

        public override bool IsExtendedEvent => true;

        /// <summary>
        /// Initializes a new MIDIKeySignatureMessage instance.
        /// </summary>
        /// <param name="channel">The channel associated with this message (0-15), or -1 if not applicable.</param>
        /// <param name="sf">Number of sharps (if positive) or flats (if negative).</param>
        /// <param name="mi">Whether the key is a minor key, as opposed to a major key.</param>
        public MIDIKeySignatureMessage(int channel, int sf, bool mi) : base(MIDIMessageType.KeySignature, channel)
        {
            SharpsFlats = sf;
            Minor = mi;
        }
    }

    /// <summary>
    /// Represents a MIDI sequencer-specific proprietary message.
    /// </summary>
    public class MIDISequencerProprietaryMessage : MIDIMessage
    {
        public override bool IsExtendedEvent => true;
        
        /// <summary>
        /// The raw data of this message.
        /// </summary>
        public byte[] Data;

        /// <summary>
        /// Initializes a new MIDISequencerProprietaryMessage instance.
        /// </summary>
        /// <param name="channel">The channel associated with this message (0-15), or -1 if not applicable.</param>
        /// <param name="data">The data associated with this meta event.</param>
        public MIDISequencerProprietaryMessage(int channel, byte[] data) : base(MIDIMessageType.SequencerProprietary, channel)
        {
            Data = data;
        }
    }

    /// <summary>
    /// Represents a MIDI end of track message.
    /// </summary>
    public class MIDIEndOfTrackMessage : MIDIMessage
    {
        public override bool IsExtendedEvent => true;
        
        /// <summary>
        /// Initializes a new MIDIEndOfTrackMessage instance.
        /// </summary>
        /// <param name="channel">The channel associated with this message (0-15), or -1 if not applicable.</param>
        public MIDIEndOfTrackMessage(int channel) : base(MIDIMessageType.EndOfTrack, channel) { }
    }

    /// <summary>
    /// Represents the possible MIDI event types.
    /// </summary>
    public enum MIDIMessageType
    {
        NoteOff,
        NoteOn,
        NoteAftertouch,
        ControlChange,
        ProgramChange,
        ChannelAftertouch,
        PitchBend,

        SysEx,

        SequenceNumber,
        MetaText,
        MetaCopyright,
        MetaTrackName,
        MetaInstrumentName,
        MetaLyric,
        MetaMarker,
        MetaCuePoint,
        ChannelPrefix,
        EndOfTrack,
        SetTempo,
        SMPTEOffset,
        TimeSignature,
        KeySignature,

        SequencerProprietary
    }

    /// <summary>
    /// Types of MIDI control changes.
    /// </summary>
    public enum MIDIControl : byte
    {
        BankSelectMSB = 0x00,
        ModulationWheelMSB = 0x01,
        BreathControlMSB = 0x02,
        FootControlMSB = 0x04,
        PortamentoTimeMSB = 0x05,
        DataEntryMSB = 0x06,
        ChannelVolumeMSB = 0x07,
        BalanceMSB = 0x08,
        PanMSB = 0x0A,
        ExpressionControlMSB = 0x0B,
        EffectControl1MSB = 0x0C,
        EffectControl2MSB = 0x0D,
        GeneralPurposeControl1MSB = 0x10,
        GeneralPurposeControl2MSB = 0x11,
        GeneralPurposeControl3MSB = 0x12,
        GeneralPurposeControl4MSB = 0x13,

        BreathControlLSB = 0x22,
        FootControlLSB = 0x24,
        PortamentoTimeLSB = 0x25,
        DataEntryLSB = 0x26,
        ChannelVolumeLSB = 0x27,
        BalanceLSB = 0x28,
        PanLSB = 0x2A,
        ExpressionControlLSB = 0x2B,
        EffectControl1LSB = 0x2C,
        EffectControl2LSB = 0x2D,
        GeneralPurposeControl1LSB = 0x30,
        GeneralPurposeControl2LSB = 0x31,
        GeneralPurposeControl3LSB = 0x32,
        GeneralPurposeControl4LSB = 0x33,

        Sustain = 0x40,
        Portamento = 0x41,
        Sustenuto = 0x42,
        SoftPedal = 0x43,
        Legato = 0x44,
        Hold2 = 0x45,
        SoundControl1 = 0x46,
        SoundControl2 = 0x47,
        SoundControl3 = 0x48,
        SoundControl4 = 0x49,
        SoundControl5 = 0x4A,
        SoundControl6 = 0x4B,
        SoundControl7 = 0x4C,
        SoundControl8 = 0x4D,
        SoundControl9 = 0x4E,
        SoundControl10 = 0x4F,

        // SOS controllers start
        HMIEnableControlReset = 0x67,
        HMIDisableControlReset = 0x68,
        HMIChannelLock = 0x6A,
        HMIChannelPriority = 0x6B,
        HMILocalBranchPoint = 0x6C,
        HMIGoToLocalBranch = 0x6D,
        HMiGlobalLoopStart = 0x6E,
        HMiGlobalLoopEnd = 0x6F,
        HMIGlobalBranchPoint = 0x71,
        HMIGoToGlobalBranch = 0x72,
        HMILocalLoopStart = 0x74,
        HMILocalLoopEnd = 0x75,
        HMITrigger = 0x77,
        // SOS controllers end

        AllSoundOff = 0x78,
        ResetAllControl = 0x79,
        LocalControl = 0x7A,
        AllNotesOff = 0x7B,
        OmniModeOff = 0x7C,
        OmniModeOn = 0x7D,
        PolyModeOff = 0x7E,
        PolyModeOn = 0x7F
    }
}
