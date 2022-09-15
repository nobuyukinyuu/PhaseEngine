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
    internal int _seed=1;

    public P_URand(){this.Seed=0;}
    public P_URand(int seed){this.Seed = seed;}

    public int Seed
    {  //Setting negative or 0 values of seed create a new seed based on the system time and salted with the negative value (if any).
        get => _seed;
        set {if (value<=0) reseed(value);  else _seed = value;}
    }
    void reseed(int buff=0) {_seed = (int)(DateTime.Now.ToBinary() & int.MaxValue) + buff;}

    public int urand()
    {
        _seed ^= (_seed << 13);
        _seed ^= (_seed >> 17);
        _seed ^= (_seed << 5);

        return _seed;
    }    

    public int urand16()
    {
        _seed ^= (_seed & 0x07ff) << 5;
        _seed ^= _seed >> 7;
        _seed ^= (_seed & 0x0003) << 14;
        _seed &= 0xFFFF;
        return _seed;
    }

    public int urand(ref int externalSeed)
    {
        _seed = externalSeed;
        externalSeed = urand();
        return externalSeed;
    }    

    public int urand16(ref int externalSeed)
    {
        _seed = externalSeed;
        externalSeed = urand16();
        return externalSeed;
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
    P_URand     ugen = new P_URand(1);
    public int Seed {get=> ugen.Seed; set{ugen._seed = value; if(value==1) Clear();}}

    // Constructor. Guarantee that initial state is cleared
    // and uniform generator scrambled.
    public PinkNoise( ){ ClearAndRandomize(); }
    public PinkNoise(int seed){Clear(); ugen._seed = seed;} //Updates the internal seed without incurring the branch penalty

    // Copy constructor. Preserve generator state from the source
    // object, including the uniform generator seed.
    public PinkNoise( PinkNoise Source )
    {
        int  i;
        for (i=0; i<5; ++i)  contrib[i]=Source.contrib[i];
        accum = Source.accum;
        ugen.Seed = Source.ugen.Seed;
    }

    // Clear generator to a zero state.
    public void Clear( )
    {
        int  i;
        for  (i=0; i<5; ++i)  { contrib[i]=0; }
        accum = 0;
    }

    // PRIVATE, clear generator and also scramble the internal uniform generator seed.
    // Randomizes the seed to system time as a property side effect!
    void ClearAndRandomize() { Clear(); ugen.Seed=0; }

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











