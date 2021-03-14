using Godot;
using System;
using gdsFM;
using System.Collections.Generic;

public class Test2 : Label
{
    // Called when the node enters the scene tree for the first time.

    AudioStreamGenerator stream;
    AudioStreamGeneratorPlayback buf;
    AudioStreamPlayer player;

    //1hz = Table size (or short.maxValue) / MixRate

    const int scopeLen = 256;
    const int scopeHeight = 128;


    ulong accumulator;
    Slider acc_inc;
    Slider bitCrush;

    Operator op = new Operator();



    public override void _Ready()
    {
        // await ToSignal(GetTree(), "idle_frame");
        Text = "It's okay";
        player = GetNode<AudioStreamPlayer>("Player");
        stream = (AudioStreamGenerator) player.Stream;
        buf = (AudioStreamGeneratorPlayback) player.GetStreamPlayback();
        acc_inc = GetNode<HSlider>("../Accumulator");
        bitCrush = GetNode<HSlider>("../BitCrush");

        op.oscillator.SetWaveform(Oscillator.Absine);
 
        // op.NoteSelect(0);
        op.FreqSelect(440);

        player.Play();        
    }

    public override void _Process(float delta)
    {
        //  this.Text = Engine.GetIdleFrames().ToString() + ":=   " + Oscillator.Sine(Engine.GetIdleFrames(), 1).ToString();

        // this.Text = Engine.GetIdleFrames().ToString() + ":  " + Tables.sin[Engine.GetIdleFrames() & Tables.SINE_TABLE_MASK].ToString();
        // this.Text = ((short.MaxValue/stream.MixRate * acc_inc.Value)).ToString();

        this.Text = op.phase.ToString() + "\n" + op.noteIncrement.ToString();

        if (buf.GetSkips() > 0)
            fill_buffer();

    }

    void fill_buffer()
    {
        var frames= buf.GetFramesAvailable();
        var output = new Vector2[frames];

        for (int i=0; i<frames;  i++)
        {
            // output[i].x = Tables.short2float[ Oscillator.CrushedSine((ulong)accumulator, (ushort) bitCrush.Value) + Tables.SIGNED_TO_INDEX ];
            output[i].x = Tables.short2float[ op.RequestSample() + Tables.SIGNED_TO_INDEX ];
            output[i].y = output[i].x;
        }


        for (int i=0; i < output.Length; i++)
        {
            if (pts.Count >= scopeLen)  break;
            var h= scopeHeight/2;
            pts.Enqueue(output[i].x * h + h);
        }

        buf.PushBuffer(output);
    }


    public override void _PhysicsProcess(float delta)
    {
        base._PhysicsProcess(delta);
        // op.NoteSelect((byte) bitCrush.Value);
        op.duty = (ushort) bitCrush.Value;
        op.FreqSelect(acc_inc.Value);
        Update();
    }

    // Vector2[] pts=new Vector2[scopeLen];
    Queue<float> pts=new Queue<float>(scopeLen);
    Vector2[] drawCache = new Vector2[scopeLen];
    public override void _Draw()
    {
        base._Draw();
        
            if (pts.Count >= scopeLen) 
            {
                for(int i=0;  i<scopeLen; i++)  
                    drawCache[i] = new Vector2( i, pts.Dequeue() );

                while(pts.Count > scopeLen)  pts.Dequeue();
            }



            for(int i=0;  i<scopeLen-1; i++)  
            {
                DrawLine(drawCache[i], drawCache[i+1], Color.ColorN("cyan"), 0.5f, true);
            }

            // DrawPolyline(pts, Color.ColorN("blue"),1,true);
    }

}
