using Godot;
using System;
using WaveLoader;
using PhaseEngine;

public class DebugResampleTest : Control
{

    public WaveFile wf;
    public float[] data = new float[0];
    AudioStreamGenerator stream;
    AudioStreamGeneratorPlayback buf;
    AudioStreamPlayer player;
    Vector2[] bufferPool;


    double head = 0;
    double stride = 1;

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
        GD.Print(wf.SampleRate, ", stride ", stride);
        GetNode<Slider>("Freq").Value = wf.SampleRate;

        var rect = GetNode<ColorRect>("Display");
        var display = new WaveformDisplay<float>(data);
        float[] mins = new float[(int)rect.RectSize.x], maxes = new float[(int)rect.RectSize.x];
        display.GetDisplayData(0, ref mins, ref maxes, (int)rect.RectSize.x, 8);

        rect.Set("mins", mins);
        rect.Set("maxes", maxes);
        rect.Update();
    }

    public void SetSpeed(float multiplier) =>  stride = wf.SampleRate / stream.MixRate * multiplier;

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
                    output[i].x = data[(int)head];
                    output[i].y = output[i].x;
                    head+=stride;
                    head%=data.Length;
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
