using System;
using gdsFM;

namespace gdsFM 
{
    public class Chip
    {
        Voice voice = new Voice();  //Description of the timbre.

        public byte opCount = 6;  //Probably should be moved to a voice class, then the chip given a unitimbral description of the voice. Realloc on major change
        public byte polyphony = 6;
        public Channel[] channels;

        public Chip()
        {
            channels = new Channel[polyphony];
            InitChannels();
        }

        public Chip(byte polyphony) 
        {
            this.polyphony = polyphony;
            channels = new Channel[polyphony];
            InitChannels();
        }

        void InitChannels()
        {
            for(int i=0; i<channels.Length; i++) channels[i] = new Channel(opCount);
        }


        public void Clock()
        {
            for (int i=0; i<channels.Length;  i++)
            {
                channels[i].Clock();
            }
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

        #if GODOT
        /// Returns a floating point value useful for a godot AudioStreamGenerator. Values may exceed 1.0; clamp or alter the gain yourself!
        public float RequestSampleF()
        {
            float output=0;
            for (int i=0; i<channels.Length;  i++)
            {
                output += Tables.short2float[channels[i].RequestSample()+Tables.SIGNED_TO_INDEX];
            }
            return output;
        }
        #endif


        //TODO:  NoteOn func that can grab an unbusy note.  Should also return a handle to the channel the note was assigned.
        //      Consider returning false/-1 instead of stealing a channel to simplify NoteOff calls if a channel was already reappropriated due to polyphony limit.
        //      or, have an out variable for the channel and return true or false if note was stolen.  NoteOffs shouldn't do anything for "free" channels.
        public bool NoteOn(Channel ch, byte midi_note)
        {
            if (ch == null) return false;
            ch.NoteOn(midi_note);
            return true;
        }
        public bool NoteOn(byte midi_note)
        {
            Channel ch = RequestChannel();
            return NoteOn(ch, midi_note);
        }

        public bool NoteOff(Channel ch)
        {
            if (ch == null) return false;
            ch.NoteOff();
            return true;
        }
        public bool NoteOff(byte midi_note)
        {
            Channel ch = FindChannel(midi_note);
            if (ch == null) return false;
            ch.NoteOff();
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

                var chScore = ch.PriorityScore;
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
        public Channel FindChannel(byte midi_note)
        {
            for(int i=0; i<channels.Length; i++)
            {
                var ch=channels[i];
                if(ch.midi_note == midi_note) return ch;
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

    }

}


// Thoughts:  Use this class to synchronize clocking across operators. Accumulate clock against reference frequency and execute as many clocks as needed to catch up.
// Then, subtract that many from the accumulator and leave the fractional component.  If godot environment is detected, consider syncing clock across buses, so that
// stream generators with different latency values can be caught up (or forced to wait) if buffer underrun is experienced or generators go out of sync.

// Operators could be grouped as "voices" and pooled as such if helper methods are needed, or in a nested
// array of operators if they aren't.  config data can be a patch class, along with other globals such as LFO objects etc, which could be separate operators with/without EG.
// Channels serve as a way to house a given allocation of operators sharing a patch, though patches can assign to multiple channels.  Consider using extension methods
// for channels so that they can be configured to be assigned to a bus if Godot environment is detected. Also consider godot environment for inclusion of bus management helpers.
// 
// For operator allocation, a constructor taking patch data could be applied to channel/voice or wherever the op bank would be stored.
// "Voice" class may be necessary to poll op banks for a single note to see if one is free to reuse..... Class could also apply the algorithm?