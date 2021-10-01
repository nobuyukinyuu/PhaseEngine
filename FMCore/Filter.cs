using System;
using gdsFM;

namespace gdsFM 
{
    public class Filter : OpBase
    {
        public delegate short FilterFunc(ushort input);
        public FilterFunc ApplyFilter;

        public Filter()    {}

        public override void Clock()
        {
            throw new NotImplementedException();
        }

        public override short RequestSample(ushort input, ushort am_offset)
        {
            
            throw new NotImplementedException();
        }

        public override void SetOscillatorType(byte waveform_index)
        {
            throw new NotImplementedException();
        }
    }

}
