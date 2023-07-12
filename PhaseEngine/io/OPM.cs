using System;
using PhaseEngine;
using System.IO;
using System.Collections.Generic;
using PE_Json;

namespace PhaseEngine
{
    public class ImportOPM : VoiceBankImporter
    {
        enum OpNames {M1=0, C1=1, M2=2, C2=3}
        public ImportOPM(){fileFormat="opm"; description="VOPM Soundbank";}


        //Ratios used to convert an OPM detune value to a PhaseEngine one.
        readonly static float[] dt1_ratios = { 0, 0.25f, 0.667f, 1.0f, 0, -0.25f, -0.667f, -1.0f };
        readonly static short[] dt2_coarse_ratios = { 0, 6, 8, 9};
        readonly static short[] dt2_fine_ratios = { 0, 0, -20, 50};

        static double ClockMult = 55930.0/Global.MixRate * Global.ClockMult;  //Ratio needed to translate one OPM clock to a PhaseEngine clock at current mixrate
        // static double ClockMult = 1;  //Ratio needed to translate one OPM clock to a PhaseEngine clock at current mixrate


        //The below values are used to calculate ratios to translate values from the chips' defaults to PhaseEngine's level of precision.
        const int TL_MAX = 127;
        const int DL_MAX= 15;
        const int AR_MAX= 31;
        const int DR_MAX= 31;
        const int SR_MAX= 31;
        const int RR_MAX= 15;


        // const float RATIO_TL = (Envelope.L_MAX) / (float)(TL_MAX+0);  //127<<3 = 1016; 1016/128 = ratio
        const float RATIO_TL = 9.1f; 
        // const float RATIO_DL = (Envelope.L_MAX) / (float)(DL_MAX+0);
        const float RATIO_DL = RATIO_TL * 8.0f;


        // const float RATIO_AR = (Envelope.R_MAX+1) / (float)(AR_MAX+1);
        // const float RATIO_DR = Envelope.R_MAX / (float)DR_MAX;
        // const float RATIO_SR = Envelope.R_MAX / (float)SR_MAX;
        static double RATIO_RR = (Envelope.R_MAX+1) / (float)(RR_MAX+1) * ClockMult;

        static double RATIO_AR = ClockMult;
        static double RATIO_DR = ClockMult;
        static double RATIO_SR = ClockMult;
        // const float RATIO_RR = 1.0f;


        public override IOErrorFlags Load(string path)
        {
            //Update the clock multiplier, in case sample rate changed....
            ClockMult = 55930.0/Global.MixRate * Global.ClockMult;

            IOErrorFlags err = IOErrorFlags.OK;
            try
            {
                using (StreamReader sr = new StreamReader(path))
                {
                    string line;
                    line=sr.ReadLine();
                    //Check header for validity.  TODO:  Check to see if there exists other formats created in other drivers
                    if (!line.StartsWith("//MiOPMdrv")) //Sound driver label missing but could still be valid OPM.  Proceed.
                        if (!line.StartsWith("//LFO: LFRQ AMD PMD WF NFRQ")) 
                            { throw new PE_ImportException(IOErrorFlags.UnrecognizedFormat); }

                    //Move forward to the instrument blocks.
                    // while (!line.StartsWith("@:"))  line=sr.ReadLine();


                    //OPM files all have 128 banks.  Initialize bank.
                    bank = new string[128];

                    //Prepare to process banks.
                    // var egs = new Envelope[4]; egs.InitArray();  //Envelope used to generate partial JSON.                    
                    // var pgs = new Increments[4]; for(int i=0; i<pgs.Length; i++) pgs[i] = Increments.Prototype();
                    var v = new Voice(4);

                    // for(int i=0; i<bank.Length; i++)
                    while(!sr.EndOfStream)
                    {
                        //Use our envelope proto as a way to store voice data as a json string.
                        //Assume the format of each block corresponds to the MiOPMdrv specification:
                        //@:[Num] [Name]
                        //LFO: FRQ AMD PMD WAV NFRQ   //Where NFRQ = Noise Frequency (Duty in PhaseEngine Noise1 or Noise2 mode)
                        //CH:  PAN	FB ALG AMS PMS SLOT NE  //Where FB = Feedback of M1 (first op) and NE = Noise override (Change all waveforms to Noise1)
                        //[OPname]: AR DR  SR  RR  DL   TL  KS MUL DT1 DT2 AMS-EN
                        //
                        // CH SLOT is the mute mask, where  M1=8, C1=16, M2=32, C2=64. It's currently unknown if flags 1, 2, 4 are used. Normal mask:  120
                        // AMS-EN is AMS enable, which only appears to have 0 and 128 as values.  Treat DT1 as normal detune and DT2 as coarse (Cents mult).

                        var p = new JSONObject();

                        //Get the first instrument.
                        var l=NextValidLine(sr);
                        while (!l.StartsWith("@:")) l=NextValidLine(sr);

                        ProcessNextVoice:
                        //Currently, l should be the instrument header line. Process every instrument in the bank now.
                        l = l.Substring(2).Trim();  //Prep for split.
                        string[] splitLine = {l.Substring(0, l.IndexOf(" ")), l.Substring(l.IndexOf(" "))};  //Split in two at first space
                        // var splitLine = l.Split(" ", StringSplitOptions.RemoveEmptyEntries);  //Should have a length of 2.
                        var slot = Convert.ToInt32(splitLine[0]);  //Will be used to assign the correct bank once we have built our voice proto.
                        // p.AddPrim("name", splitLine[1]);
                        v.name = splitLine[1].Trim();

                        l=NextValidLine(sr);  //Next line should be LFO.  However, order could be anything....
                        
                        int amd, ams=0, pmd=0, pms=0, nFrq=0;
                        

                        ProcessNextLine:
                        splitLine=l.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                        switch(splitLine[0].Trim())
                        {
                            case "LFO:": //LFO configuration options.
                                //Estimate LFO speed from OPM manual. Not very accurate -- FIXME.
                                v.lfo.pg = Increments.FromFreq( Tools.Lerp(0.008, Global.MixRate, Convert.ToByte(splitLine[1]) / 255.0) );
                                // v.lfo.pg.FreqSelect( Tools.Lerp(0.008, Global.MixRate, Convert.ToByte(splitLine[1]) / 255.0) );
                                v.lfo.pg.Recalc();
                                amd = (Convert.ToByte(splitLine[2])) << 3;  //Max value:  1016.
                                pmd = (Convert.ToByte(splitLine[3]));  //Max value:  127.

                                v.lfo.invert = true;

                                //Determine LFO oscillator.
                                Oscillator.oscTypes osc= Oscillator.oscTypes.Saw;
                                switch(Convert.ToByte(splitLine[4]))
                                {
                                    case 0:
                                        osc= Oscillator.oscTypes.Saw;
                                        break;
                                    case 1:
                                        osc= Oscillator.oscTypes.Pulse;
                                        break;
                                    case 2:
                                        osc= Oscillator.oscTypes.Triangle;
                                        break;
                                    case 3:
                                        osc= Oscillator.oscTypes.Noise2;
                                        break;
                                }
                                v.lfo.SetOscillatorType(osc);

                                //Determine noise frequency, if the noise generator is active.
                                nFrq = Convert.ToByte(splitLine[5]);
                                break;

                            case "CH:":  //Voice configuration options.
                                v.Pan = Convert.ToByte(splitLine[1]) / 128.0f * 2 -1; //VOPM appears to set PAN to 64 by default, so we want to make sure 0 is our default
                                v.egs[0].feedback = Convert.ToByte(splitLine[2]);  //Set operator 1 to the feedback level specified.
                                var algNum=Convert.ToByte(splitLine[3]);
                                v.alg = Algorithm.FromPreset(algNum, Algorithm.PresetType.OPM); 
                                ams = Convert.ToByte(splitLine[4]) << 1;  //Max value:  6
                                pms = Convert.ToByte(splitLine[5]);  //Max value:  7

                                //Process the mute mask.
                                var mute = Convert.ToByte(splitLine[6]) >> 3;  //Default mute mask is 120.  Remove 3 LSBs.
                                for(int i=0; i<4; i++)
                                    v.egs[i].mute = (mute>>i & 1)==0;

                                //Determine whether the waveform should be noise.
                                if(Convert.ToByte(splitLine[7])>0)
                                {
                                    for (int i=0; i<4; i++) {
                                        v.oscType[i] = (byte)Oscillator.oscTypes.Noise2;
                                        v.egs[i].duty = (ushort)(31 - nFrq);
                                    }
                                } else {
                                    for (int i=0; i<4; i++) {
                                        v.oscType[i] = (byte)Oscillator.oscTypes.Sine;
                                        v.egs[i].duty = 32767;
                                    }
                                }

                                break;
                            default:  //One of the 4 operators.  Translate from OpNames to their correct values to assign the correct envelope.
                                OpNames opName;
                                if (Enum.TryParse<OpNames>(splitLine[0].Trim().Substring(0, 2), true, out opName)) //Only executes on success
                                {
                                    var opNum = (int)opName;
                                    var e = v.egs[opNum];  //Select the envelope.
                                    //[OPname]: AR DR  SR  RR  DL   TL  KS MUL DT1 DT2 AMS-EN

                                    e.ar = (byte) Math.Round(Convert.ToByte(splitLine[1]) * RATIO_AR);
                                    e.dr = (byte) Math.Round(Convert.ToByte(splitLine[2]) * RATIO_DR);
                                    e.sr = (byte) Math.Round(Convert.ToByte(splitLine[3]) * RATIO_SR);
                                    e.rr = (byte) Math.Round(Convert.ToByte(splitLine[4]) * RATIO_RR);

                                    e.dl = (ushort) Math.Min(Math.Round(Convert.ToUInt16(splitLine[5]) * RATIO_DL), Envelope.L_MAX);
                                    e.sl = e.sr>0? Envelope.L_MAX: e.dl;
                                    e.tl = (ushort) Math.Min(Math.Round(Convert.ToUInt16(splitLine[6]) * RATIO_TL), Envelope.L_MAX);

                                    //Assign envelope RateTable to a default preset and scale the max application.
                                    e.ksr = new RateTable();  e.ksr.ceiling = (float)(Convert.ToUInt16(splitLine[7]) * 25 / ClockMult); //FIXME:  Check accuracy

                                    var dt2 = Convert.ToUInt16(splitLine[10]);
                                    v.pgs[opNum].mult = Convert.ToUInt16(splitLine[8]);
                                    v.pgs[opNum].Detune = dt1_ratios[Convert.ToUInt16(splitLine[9])];  //DT1
                                    v.pgs[opNum].coarse = dt2_coarse_ratios[dt2];  //DT2
                                    v.pgs[opNum].fine = dt2_fine_ratios[dt2];

                                    //Determine AMS.
                                    e.ams = Convert.ToByte(splitLine[11])>0?  (byte)ams : (byte)0;   
                                }
                            break;    
                        }

                        l=NextValidLine(sr);
                        if (l==null || l.StartsWith("@:"))
                        {
                            //Final prep of instrument that has all the values it's going to have plugged into it.  Now to merge PMS/PMD.
                            //OPM LFO is not exactly linear in the pitch range from base note to min/max, so we use an estimate based on max,
                            //where a PMD of 127 translates to a PhaseEngine PMD of ~±0.58_6363 repeating.
                            v.lfo.pmd = Tools.Lerp(0, 0.586363f, pmd/127.0f) * (pms/7.0f);

                            //TODO:  Consider whether recalcing the PG increments are necessary.

                            // bank[slot] = //TODO:  Convert voice to string here...  Or, change bank type to Voice[] from string[]....
                            bank[slot] = v.ToJSONString();

                            if (l==null) break;  //End of File                            
                            else goto ProcessNextVoice;
                        }
                        else goto ProcessNextLine;
                    }

                }
            }
            catch (FileNotFoundException) { err |= IOErrorFlags.NotFound; }
            catch (PE_ImportException e) { err |= e.flags; }
            // catch //Anything else
            // { err |= IOErrorFlags.Failed | IOErrorFlags.Corrupt; }

            return err;
        }

        //At any point, some joker may have inserted a comment or empty line.  Use this local func to skip them.
        string NextValidLine(StreamReader sr) 
        { 
            string ln="";
            while(ln.StartsWith("//") || ln.Trim()=="")
            {
                ln=sr.ReadLine(); 
                if (ln==null) return null;
            }
            return ln; 
        }


        public override string ToString()
        {
            return base.ToString();
        }
    }
}
