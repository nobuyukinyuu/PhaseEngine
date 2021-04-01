using System;
using gdsFM;

namespace gdsFM 
{
    public class Chip
    {
        public Chip()    {}
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