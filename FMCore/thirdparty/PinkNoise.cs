// Pink noise class using the autocorrelated generator method.
// Method proposed and described by Larry Trammell "the RidgeRat" --
// see http://home.earthlink.net/~ltrammell/tech/newpink.htm
// There are no restrictions.
//
// ------------------------------------------------------------------
//
// This is a canonical, 16-bit fixed-point implementation of the
// generator in 32-bit arithmetic. There are only a few system
// dependencies.
//
//   -- access to an allocator 'malloc' for operator new
//   -- access to definition of 'size_t'
//   -- assumes 32-bit two's complement arithmetic
//   -- assumes long int is 32 bits, short int is 16 bits
//   -- assumes that signed right shift propagates the sign bit
//
// It needs a separate URand class to provide uniform 16-bit random
// numbers on interval [1,65535]. The assumed class must provide
// methods to query and set the current seed value, establish a
// scrambled initial seed value, and evaluate uniform random values.
//
//  c# port:   Nobuyuki (nobuyukinyuu@users.noreply.github.com)
using System;

public class P_URand
{
    int _seed=1;

    public P_URand(){this.seed=0;}
    public P_URand(int seed){this.seed = seed;}

    public int seed
    {
        get => _seed;
        set {if (value==0) _seed = (int)(DateTime.Now.ToBinary() & int.MaxValue);  else _seed = value;}
    }

    public int urand()
    {
        _seed ^= (_seed << 13);
        _seed ^= (_seed >> 17);
        _seed ^= (_seed << 5);

        return _seed;
    }    
}


public class PinkNoise {
    // Coefficients (fixed)
    static readonly Int32[] pA = { 14055, 12759, 10733, 12273, 15716 };
    static readonly short[] pPSUM = { 22347, 27917, 29523, 29942, 30007 };

    // Internal pink generator state
    Int32[] contrib = new Int32[5];   // stage contributions
    Int32   accum;        // combined generators

    // Include a UNoise component
    public P_URand     ugen = new P_URand();

    // Constructor. Guarantee that initial state is cleared
    // and uniform generator scrambled.
    public PinkNoise( ){ internal_clear(); }
    // Copy constructor. Preserve generator state from the source
    // object, including the uniform generator seed.
    public PinkNoise( PinkNoise Source )
    {
        int  i;
        for (i=0; i<5; ++i)  contrib[i]=Source.contrib[i];
        accum = Source.accum;
        ugen.seed = Source.ugen.seed ;
    }

    // Clear generator to a zero state.
    public void Clear( )
    {
        int  i;
        for  (i=0; i<5; ++i)  { contrib[i]=0; }
        accum = 0;
    }

    // PRIVATE, clear generator and also scramble the internal
    // uniform generator seed.
    void internal_clear( )
    {
        Clear();
        ugen.seed=0;    // Randomizes the seed!
    }

    // Evaluate next randomized 'pink' number with uniform CPU loading.
    public short Next( )
    {
        short randu = unchecked((short) (ugen.urand() & 0x7fff));     // U[0,32767]
        short randv = unchecked((short) ugen.urand());  // U[-32768,32767]

        void UPDATE_CONTRIB(byte n)  
        {
            accum -= contrib[n];      
            contrib[n] = randv * pA[n];
            accum += contrib[n];
        }

        // Structured block, at most one update is performed
        while (true)
        {
            if (randu < pPSUM[0]) { UPDATE_CONTRIB(0);  break; }
            if (randu < pPSUM[1]) { UPDATE_CONTRIB(1);  break; }
            if (randu < pPSUM[2]) { UPDATE_CONTRIB(2);  break; }
            if (randu < pPSUM[3]) { UPDATE_CONTRIB(3);  break; }
            if (randu < pPSUM[4]) { UPDATE_CONTRIB(4);  break; }
            break;
        }
        return (short) (accum >> 16);
    }

} ;











