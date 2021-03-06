using System;
using PhaseEngine;

namespace PhaseEngine 
{
    public class Chip
    {
        public const sbyte NO_CHANNEL_FOUND = -1;

        Voice voice; //= new Voice();  //Description of the timbre.
        public Voice Voice { get => voice; set => SetVoice(value); }
        public bool disableLFO;  //Used by processes that wish to calculate voices without the LFO, such as the offline preview.


        byte opCount = 6;  //Probably should be moved to a voice class, then the chip given a unitimbral description of the voice. Realloc on major change
        public byte OpCount{get=>opCount;}  //Use SetOpCount to set opCount outside of Chip.

        public byte polyphony = 6;
        public Channel[] channels;

        //Used to determine if a chip is "Silent" so it can be put to sleep. Does
        public bool ChannelsAreFree {
            get{for(byte i=0; i<channels.Length; i++)
                    {  //Proc the busy state by peeking at the priority score.
                        channels[i].CalcPriorityScore();
                        if(channels[i].busy != BusyState.FREE) return false;
                    }
                return true;
            }
        }
        public bool ChannelIsFree(byte chNum) 
        {
            System.Diagnostics.Debug.Assert(chNum>=0 && chNum<channels.Length, String.Format("Invalid channel {0}!", chNum));
            channels[chNum].CalcPriorityScore();
            if (channels[chNum].busy != BusyState.FREE) return false;
            return true;
        }

        #region Constructors
        public Chip()
        {
            channels = new Channel[polyphony];
            voice = new Voice(opCount);
            InitChannels();
        }

        public Chip(byte polyphony) 
        {
            this.polyphony = polyphony;
            channels = new Channel[polyphony];
            voice = new Voice(opCount);
            InitChannels();
        }

        public Chip(byte polyphony, byte opCount)
        {
            this.opCount = opCount;            
            this.polyphony = polyphony;
            voice = new Voice(opCount);
            channels = new Channel[polyphony];
            InitChannels();
        }
#endregion

        void InitChannels()
        {
            for(int i=0; i<channels.Length; i++) 
            {
                channels[i] = new Channel(opCount);
                channels[i].SetVoice(voice);
            }
        }


        public void Clock()
        {
            Channel.am_offset = voice.lfo.RequestAM();
            // System.Threading.Tasks.Parallel.For(0, channels.Length, i =>
            for (int i=0; i<channels.Length;  i++)
            {
                if(channels[i].busy != BusyState.FREE) //Clock optimization
                {
                    channels[i].Clock();
                    
                    //Apply LFO pitch changes.
                    for(int j=0; j<opCount; j++)
                        voice.lfo.ApplyPM(ref channels[i].ops[j].pg);
                }
            }//);

            voice.lfo.Clock();
        }

        public short RequestSample()
        {
            int output=0;
            for (int i=0; i<channels.Length;  i++)
            {
                output += channels[i].RequestSample();
            }

            return (short) output.Clamp(short.MinValue, short.MaxValue);
        }
        public short RequestSample(byte downscaleMagnitude)
        {
            int output=0;
            for (int i=0; i<channels.Length;  i++)
            {
                output += channels[i].RequestSample();
            }

            return (short) (output>>downscaleMagnitude).Clamp(short.MinValue, short.MaxValue);
        }

        /// Returns a floating point value useful for a godot AudioStreamGenerator. Values may exceed 1.0; clamp or alter the gain yourself!
        public float RequestSampleF()
        {
            float output=0;
            for (int i=0; i<channels.Length;  i++)
            {
                output += Tables.short2float[channels[i].RequestSample()+Tables.SIGNED_TO_INDEX];
            }
            return output * voice.Gain;
        }


        /// Flips on the specified channel and returns the event ID.
        public long NoteOn(Channel ch, byte midi_note, byte velocity=127)
        {
            if (ch == null) return NO_CHANNEL_FOUND;
            ch.NoteOn(midi_note, velocity);
            voice.lfo.NoteOn();
            return ch.eventID;
        }
        /// Finds the best candidate for a channel and returns its event ID.
        public long NoteOn(byte midi_note, byte velocity=127)
        {
            Channel ch = RequestChannel(midi_note);
            return NoteOn(ch, midi_note, velocity);
        }

        /// Turns off the specified channel.  Returns false if the channel is invalid.
        public bool NoteOff(Channel ch)
        {
            if (ch == null) return false;
            ch.NoteOff();
            return true;
        }

        /// Turns off all channels with the corresponding midi note.
        public bool NoteOff(byte midi_note)
        {
            var chs = FindChannels(midi_note);
            if (chs.Length == 0) return false;
            
            for(int i=0; i<chs.Length;  i++)
            {
                chs[i].NoteOff();
            }
            return true;
        }
        /// Turns off the channel with the corresponding event ID.
        public bool NoteOff(long eventID)
        {
            if (eventID <= 0) return false;
            Channel ch = FindChannel(eventID);
            if (ch == null) return false;
            ch.NoteOff();
            ch.CalcPriorityScore();
            return true;
        }



        //TODO:  Channel request methods for flipping a note on.  Each channel is enumerated for PriorityScore unless one is found free (score of 0?)
        //      Manual requested channel is nullable and should catch out-of-bounds number requests, print an error out and return null.
        //      Auto requested channel should probably never return null....
        public Channel RequestChannel(byte midi_note=Global.NO_NOTE_SPECIFIED) 
        {
            Channel best_candidate = channels[0];
            var score = 0;
            for(int i=0; i<channels.Length; i++)
            {
                var ch=channels[i];
                if (ch.busy==BusyState.FREE) 
                    return ch;
                if (ch.midi_note == midi_note) //The channel we're peeking is the same note! Turn off. Might be best candidate
                {
                    ch.NoteOff();
                    // return ch;
                }

                var chScore = ch.CalcPriorityScore();
                if (chScore > score)
                {
                    score = chScore;
                    best_candidate = ch;
                }
            }
            return best_candidate;
        }

        // //Immediately grabs a channel whether it's busy or not.
        // public Channel GrabChannel(byte channel) 
        // {
        //     var ch = channels[channel];
        //     return ch;
        // }
        public Channel[] FindChannels(byte midi_note)
        {
            var output = new System.Collections.Generic.List<Channel>(polyphony);
            for(int i=0; i<channels.Length; i++)
            {
                var ch=channels[i];
                if(ch.midi_note == midi_note) output.Add(ch);
            }
            return output.ToArray();
        }
        public Channel FindChannel(long eventID)
        {
            for(int i=0; i<channels.Length; i++)
            {
                var ch=channels[i];
                if(ch.eventID == eventID) return ch;
            }
            return null;

        }

        public Channel RequestChannelOrNull(byte channel) 
        {
            try
            {
                var ch = channels[channel];
                return ch.busy==BusyState.FREE?  ch : null;
            } catch {
                System.Diagnostics.Debug.Print(String.Format("RequestChannel:  Invalid channel number {0}.",channel));
                return null;
            }
        }

        /// Updates all channels' references to the canonical voice.
        public void SetVoice(Voice v)
        {
            voice = v;
            for (int i=0; i<channels.Length; i++)
                channels[i].SetVoice(v);
        }

        /// Updates all channels' operator count.  Will also update any attached voice specified, if requested.
        public void SetOpCount(byte opTarget, Voice v=null)
        {
            opCount = opTarget;
            if (v!=null) v.SetOpCount(opTarget);
            for (int i=0; i<channels.Length; i++)
                channels[i].SetOpCount(opTarget);
        }

        //Sets the intents to the voice specified intent.
        public bool UpdateIntent(byte opTarget, OpBase.Intents intent)
        {
            if (voice==null) return false;
            voice.SetIntent(opTarget, intent);
            for (int i=0; i<channels.Length; i++)   channels[i].SetIntents(opTarget, (byte)(opTarget+1), voice);
            return true;
        }


        #if GODOT
            /// Updates the voice's algorithm and informs the channels.
            public void SetAlgorithm(Godot.Collections.Dictionary d)
            {
                if ((int)d["opCount"] != opCount)
                {
                    // Operator count changed.  Update channels, as the voice parameters no longer reflect their operators' current opCount.
                    opCount = Convert.ToByte(d["opCount"]);
                    voice.SetOpCount(opCount);
                    // InitChannels();
                    SetVoice(voice);  //Gives the channels a chance to change their operator count.
                }


                voice.SetAlgorithm(d);
            }
        #endif


        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            string nl = Environment.NewLine;

            for (int i=0; i<channels.Length; i++)
            {
                var ch= channels[i];
                sb.Append(String.Format("Ch.{0} (ID: {1}): {2} (Priority {3})", i, ch.eventID, ch.busy.ToString(), ch.lastPriorityScore));
                sb.Append(nl);
            }
            return sb.ToString();
        }


    }
}


// Thoughts:  Use this class to synchronize clocking across operators. Accumulate clock against reference frequency and execute as many clocks as needed to catch up.
// Then, subtract that many from the accumulator and leave the fractional component.  If godot environment is detected, consider syncing clock across buses, so that
// stream generators with different latency values can be caught up (or forced to wait) if buffer underrun is experienced or generators go out of sync.

// Channels serve as a way to house a given allocation of operators sharing a patch, though patches can assign to multiple channels.  Consider using extension methods
// for channels so that they can be configured to be assigned to a bus if Godot environment is detected. Also consider godot environment for inclusion of bus management helpers.

// Unison settings could live here and allocate multiple channels as necessary to perform the unison function.  Look into stereo width, numVoices, gain,
// detune, and perhaps a min/max voices rTable based on input velocity