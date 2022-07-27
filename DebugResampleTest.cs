using Godot;
using System;
using WaveLoader;
using PhaseEngine;

public class DebugResampleTest : Control
{

    public WaveFile wf;
    public float[] data = new float[0];
    public float[][] lod = new float[8][];
    AudioStreamGenerator stream;
    AudioStreamGeneratorPlayback buf;
    AudioStreamPlayer player;
    Vector2[] bufferPool;


    public delegate float DownsampleTechniqueDelegate (int sample);
    public DownsampleTechniqueDelegate Downsampler;
    enum ResampleTechnique{NONE, LOD, LOD_LERP}
    double STRENGTH = 1.5;
    int currentLod = 0;

    double head = 0;
    float PlaybackPosition {get => data.Length==0?  0: (float)(head/(double)data.Length);}
    double stride = 1;
    float multiplier = 1;
    float currentSpeed = 1;

    public DebugResampleTest(){lod[0] = data; Downsampler = RawSampleOf;}

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        player = GetNode<AudioStreamPlayer>("Player");
        stream = (AudioStreamGenerator) player.Stream;
        buf = (AudioStreamGeneratorPlayback) player.GetStreamPlayback();

        bufferPool = new Vector2[(int)Math.Max(stream.MixRate * stream.BufferLength +2, stream.MixRate)];
        stream.MixRate = Global.MixRate;
        player.Play();        
    }

    public void LoadWave(string path)
    {
        wf = WaveFile.Load(path);
        data = wf.GetDataFloat();
        head=0;
        stride = wf.SampleRate / stream.MixRate;
        // GD.Print(wf.SampleRate, ", stride ", stride);
        GetNode<Slider>("Freq").Value = wf.SampleRate;

        //Load LODs
        lod[0] = data;
        for (int i=1; i<lod.Length; i++)
        {
            float[] array;
            array = Tools.Butterworth(data, wf.SampleRate, wf.SampleRate / (1<<i));
            lod[i] = array;
        }

        RecalcDisplay();
    }

    public void RecalcDisplay() => RecalcDisplay(lod[currentLod]);
    public void RecalcDisplay(float[] dataSrc)
    {
        var rect = GetNode<ColorRect>("Display");
        var display = new WaveformDisplay<float>(dataSrc);
        float[] mins = new float[(int)rect.RectSize.x*1], maxes = new float[(int)rect.RectSize.x*1];
        int samplesPerPx = Math.Max(1, (int)Math.Round(dataSrc.Length/rect.RectSize.x));
        display.GetDisplayData(0, samplesPerPx, ref mins, ref maxes, (int)rect.RectSize.x*1);

        var optimalSamples = Math.Sqrt(dataSrc.Length/OS.GetScreenSize().x);
        GD.Print("Optimal samples per LUT: ", optimalSamples, ", ", Tools.Pow2Ceil((int)optimalSamples));
        GD.Print("Predicted LUT size: ", 131072.0 / Tools.Pow2Ceil((int)optimalSamples));

        //Convert peaks and valleys such that any bits from the current data are connected to the previous.
        const float epsilon = 1.0f/(float)ushort.MaxValue;
        for (int i=1; i < mins.Length; i++)
        {
            if (mins[i] > maxes[i - 1])
                mins[i] = maxes[i - 1] + epsilon;
            if (maxes[i] < mins[i - 1])
                maxes[i] = mins[i - 1] - epsilon;
        }

        rect.Set("mins", mins);
        rect.Set("maxes", maxes);
        rect.Update();        
    }

    public float RawSampleOf(int sample) => data[sample];
    public float LodOf(int sample)
    {
        var mult = (int)Math.Clamp(Math.Abs(multiplier), 0, 7);  //Clamp to the number of LODs available
        return lod[mult][sample];
    }
    public float LerpLodOf(int sample)
    {
        var mult = (int)Math.Clamp(Math.Abs(multiplier), 0, 7);  //Clamp to the number of LODs available
        var mult2 = mult+1;  if (mult2>7) mult2=7;
        var percent = multiplier - Math.Truncate(multiplier);
        return Tools.Lerp(lod[mult][sample], lod[mult2][sample], (float)percent);
    }


    //TODO:  Consider the following:  Build a log2 table of value multipliers up to 8 octaves above current.
    //Using a base note on a MIDI note value we can get the closest floating log2 approximates and lerp between them.
    //Lerping the multiplier approximates may not be necessary but will only give 12 levels of fidelity between LODs.
    //Test to see if the transition is audible on pitch bends.
    //This is to speed up lookups of LOD mixing percentages based on current pitch relative to normal in the sampler core.
    //Also consider exposing ability to set a sample as "LODable" as well as a strength multiplier to affect how strong the lowpass effect is as pitch rises.
    public void SetSpeed(float multiplier) 
    {
        currentSpeed = multiplier;
        stride = wf.SampleRate / stream.MixRate * multiplier;
        this.multiplier = (float)Tools.Log2(Math.Abs(multiplier)*STRENGTH) * Math.Sign(multiplier);
        if(double.IsNaN(this.multiplier)) 
            this.multiplier=0;
    }
    public void SetStrength(float strength) { STRENGTH = strength; SetSpeed(currentSpeed); }
    public void SetResampleTechnique(int tech)
    {
        switch((ResampleTechnique)tech)
        {
            case ResampleTechnique.NONE:
                Downsampler = RawSampleOf;
                break;
            case ResampleTechnique.LOD:
                Downsampler = LodOf;
                break;
            case ResampleTechnique.LOD_LERP:
                Downsampler = LerpLodOf;
                break;
            default:
                Downsampler = RawSampleOf;
                break;
        }
    }

    public override void _Process(float delta)
    {
        base._Process(delta);

        if (buf.GetSkips() > 0)
        {
            var frames= buf.GetFramesAvailable();
            // var output = new Vector2[frames];
            var segment = new ArraySegment<Vector2>(bufferPool, 0, frames);
            var output = segment.Array;
            
            for (int i=0; i<frames;  i++)
            {
                if(data.Length==0)
                {
                    output[i].x = 0;
                    output[i].y = 0;
                } else {
                    // output[i].x = lod[currentLod][(int)head];
                    output[i].x = Downsampler((int)head);
                    output[i].y = output[i].x;
                    head+=stride;
                    // head%=data.Length;
                    head = Tools.Mod(head, data.Length);
                }
            }


            buf.PushBuffer(segment.ToArray());
        }



    }




    public override void _PhysicsProcess(float delta)
    {
        base._PhysicsProcess(delta);
    }

}
