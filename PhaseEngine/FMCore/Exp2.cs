//Derived and modified from music-synthesizer-for-android, license below:
/*
 * Copyright 2012 Google Inc.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *      http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;


namespace PhaseEngine
{
    public static class Exp2
    {
        const int EXP2_LG_N_SAMPLES = 10;
        const int EXP2_N_SAMPLES = (1 << EXP2_LG_N_SAMPLES);

        readonly static int[] exp2tab = new int[EXP2_N_SAMPLES << 1];

        static Exp2() 
        {
            System.Diagnostics.Debug.Print("Ebbabablen");
            init();
        }


        static void init()
        {
            double inc = Math.Pow(2.0, 1.0 / EXP2_N_SAMPLES);
            double y = 1 << 30;
            for (int i = 0; i < EXP2_N_SAMPLES; i++) {
                exp2tab[(i << 1) + 1] = (int) Math.Floor(y + 0.5);
                y *= inc;
            }

            for (int i = 0; i < EXP2_N_SAMPLES - 1; i++) 
                exp2tab[i << 1] = exp2tab[(i << 1) + 3] - exp2tab[(i << 1) + 1];
                
            exp2tab[(EXP2_N_SAMPLES << 1) - 2] = (int)((1U << 31) - exp2tab[(EXP2_N_SAMPLES << 1) - 1]);
            System.Diagnostics.Debug.Print("jldsfkn");
        }

        public static int Lookup(int x)
        {
            const int SHIFT = 24 - EXP2_LG_N_SAMPLES;
            int lowbits = x & ((1 << SHIFT) - 1);
            int x_int = (x >> (SHIFT - 1)) & ((EXP2_N_SAMPLES - 1) << 1);
            int dy = exp2tab[x_int];
            int y0 = exp2tab[x_int + 1];

            int y = (int)(y0 + (((long)dy * (long)lowbits) >> SHIFT));
            return y >> (6 - (x >> 24));
        }
    }
}
