using System;
using gdsFM;

namespace gdsFM 
{
    public class Filter : OpBase
    {
        //TODO:  Replace with delegate func array
        public enum FilterType {NONE, LOWPASS, HIPASS, BANDPASS_CSG, BANDPASS_CZPG, NOTCH, ALLPASS, PEAKING, LOWSHELF, HISHELF}
        public delegate short FilterFunc(ushort input);
        public FilterFunc ApplyFilter;

        public Filter()    {}

        public override void Clock()
        {
            return;
            throw new NotImplementedException();
        }

        public override short RequestSample(ushort input, ushort am_offset)
        {
            return 0;
            throw new NotImplementedException();
        }

        public override void SetOscillatorType(byte waveform_index)
        {
            throw new NotImplementedException();
        }
    }

}
