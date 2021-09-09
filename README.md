&nbsp;
# ![PhaseEngine](https://raw.githubusercontent.com/nobuyukinyuu/PhaseEngine/main/gfx/logos/logo_light.png)
An experimental FM-synth written in C# intended to be the successor to [gdsFM](https://github.com/nobuyukinyuu/gdsFM/), with lower CPU usage and more predictable resource allocation for use in game engines and demos.  Leverages [Godot Engine](https://github.com/godotengine/godot/) as its primary front-end.


# Features and differences to gdsFM
## New
* Fixed-point phase accumulator (utilizing 12.20 precision by default)
* More modular, delegate-based oscillator and operator functionality (Operators can behave like filters or DSP in addition to FM)
* Envelopes with target audio levels for each phase, more like DX-style synths
* Envelope hold phase after initial attack and before initial decay, similar to the initial delay phase in gdsFM.
* All FM operators support Reface-style operator feedback.
* More traditional noise generators, including an LFSR-based generator with selectable periodicity (create more buzzing effects) similar to 2a03, etc.


## Different
* Based on more traditional paradigms, comparable to most other FM synths under the hood
  * Envelopes use attenuation in the log domain rather than mixing linear volume.
  * Only one envelope curve type supported (exponential).
  * Envelope state changes are specified in iteration rates (like most other FM synths) instead of pure length (like in gdsFM).
  * Modulating phase is done using simple addition, without piping the result through a "modulation technique" oscillator.
  * Algorithm processing order is done iteratively from the "top-down" rather than recursively from the carrier.
*  Fixed polyphony monotimbral chip layout, with operators allocated based on a chip/voice specification rather than dynamically per-note
  * Monotimbrality and fixed polyphony help simplify the design, allowing multiple instances to be specified and put on independent audio buses
* Fixed internal clock rate of 48 KHz
* Operator parameter specification is separated from its implementation, allowing more tracker-like control over individual notes (temporarily override params)
* More traditional LFO

## To be implemented
* Chip clocking to match various audio output rates, perhaps with optional sample interpolation
* PCM Sample playback + Wavetable-based oscillators
* Filter and DSP operators
* Voice format specification

## May or may not be implemented or come back
* Pitch generator
* Arbitrary parameter envelopes  (Routing an envelope to a specific parameter)
* Wider range of sampling options
* Multitimbral operation (for now, multiple chip instances syncing their clocks should suffice)

# Screenshots
![Main View](https://user-images.githubusercontent.com/1023003/132632266-234ceb1e-1409-4792-bd2b-51457e80102d.png)
![rTables](https://user-images.githubusercontent.com/1023003/132632455-aa52c44c-e76a-4805-a894-27921c8169da.png)![Wiring Grid and 6op Presets](https://user-images.githubusercontent.com/1023003/132633883-80a5c551-074c-42b8-b50a-892c93ccae4a.png)

