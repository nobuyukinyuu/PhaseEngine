using System;
using gdsFM;

namespace gdsFM 
{
    public class WaveFolder : Operator
    {
        public WaveFolder()    {}

        // public override void Clock()
        // {
        //     base.Clock();
        //     return;
        // }

        public override short RequestSample(ushort input, ushort am_offset)
        {
            var raw_output = Fold(input);  //Scaled to 14-bit for our processing table.
            flip = raw_output<0;
            var input_attenuation = Tables.vol2attenuation[Tools.Abs(raw_output)];
            ushort env_attenuation = (ushort) (envelope_attenuation(am_offset) << 2);
            int result = Tables.attenuation_to_volume((ushort)(input_attenuation + env_attenuation));

            //Bit crush the result.
            var crush = eg.aux_func /*+ 1*/;  //Save an add by adjusting aux_func in the UI to skip first bit of crushing
            // result = result & ~(1<<(eg.aux_func+1));   //This function behaved oddly....
            result >>= crush;  
            result <<= crush;

            return flip? (short)-result : (short)result; 
        }

        short Fold(ushort input)
        {
            //TODO:  Consider using osc_sync or another eg value to determine whether input should wrap or clamp
            double x = Tables.short2float[ (short)input + Tables.SIGNED_TO_INDEX] * eg.gain;
            double bias = Tables.short2float[ eg.duty ];
            x += bias;  //Apply bias
            x *= 4;   //Apply oscillator scaling
            if (eg.osc_sync)  x = Math.Clamp(x, -2.0, 2.0);

            var y = 4 * (Math.Abs(0.25*x + 0.25 - Math.Round(0.25*x + 0.25)) - 0.25);  //Fold
            
            var output = (int)(y * short.MaxValue) >> 2;
            return (short)output;
        }

        public override void SetOscillatorType(byte waveform_index)
        {
            return;
            throw new NotImplementedException();
        }
    }

}
