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


    Slider acc_inc;
    Slider bitCrush;

    Operator op = new Operator();
    Operator op2 = new Operator();



    public override void _Ready()
    {
        // await ToSignal(GetTree(), "idle_frame");
        Text = "It's okay";
        player = GetNode<AudioStreamPlayer>("Player");
        stream = (AudioStreamGenerator) player.Stream;
        buf = (AudioStreamGeneratorPlayback) player.GetStreamPlayback();
        acc_inc = GetNode<HSlider>("../Accumulator");
        bitCrush = GetNode<HSlider>("../BitCrush");

        op.SetOperatorType(Oscillator.Sine);
        op2.SetOperatorType(Oscillator.Pulse);
 
        // op.NoteSelect(0);
        op.FreqSelect(440);
        op2.FreqSelect(880);

        player.Play();

    }

    public override void _Process(float delta)
    {
        //  this.Text = Engine.GetIdleFrames().ToString() + ":=   " + Oscillator.Sine(Engine.GetIdleFrames(), 1).ToString();

        // this.Text = Engine.GetIdleFrames().ToString() + ":  " + Tables.sin[Engine.GetIdleFrames() & Tables.SINE_TABLE_MASK].ToString();
        // this.Text = ((short.MaxValue/stream.MixRate * acc_inc.Value)).ToString();

        short samp2=op2.RequestSample();
        // this.Text = Tools.ToBinStr(op.compute_volume(0,0)) + " = " + op.compute_volume(0,0) + "\n" + op.noteIncrement.ToString();
        this.Text = Tools.ToBinStr(samp2) + " = " + samp2.ToString() + "\n" + op.noteIncrement.ToString();

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
            op.Clock();
            op2.Clock();
            var samp=op.RequestSample();
            short samp2=op2.RequestSample();
            // output[i].x = Tables.short2float[ samp + Tables.SIGNED_TO_INDEX ];


            // output[i].x = attenuation_to_volume(unchecked((ushort)(samp>>0))) / 8192f ;
            output[i].x = Tables.short2float[  (short)(op.compute_volume((ushort)unchecked((samp2) >>6),  0)<<1) + Tables.SIGNED_TO_INDEX ] ;
            // output[i].x = Tables.short2float[ samp2 + Tables.SIGNED_TO_INDEX] ;
            // output[i].x = Tables.short2float[Oscillator.Sine(op.phase>>20, 0) + Tables.SIGNED_TO_INDEX] ;
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
        op2.duty = (ushort) bitCrush.Value;
        op.FreqSelect(acc_inc.Value);
        op2.FreqSelect(acc_inc.Value * 5);
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


            DrawLine(new Vector2(0, scopeHeight/2), new Vector2(scopeLen,scopeHeight/2), Color.ColorN("white", 0.3f));
            DrawLine(new Vector2(scopeLen, scopeHeight), new Vector2(scopeLen,0), Color.ColorN("white", 0.3f));

            for(int i=0;  i<scopeLen-1; i++)  
            {
                DrawLine(drawCache[i], drawCache[i+1], Color.ColorN("cyan"), 0.5f, true);
            }

            // for (int i=0; i< 256; i++)
            // {
            //     // var pos = new Vector2(i/256, 256-Tables.linVol[i] * 256);
            //     // var pos = new Vector2(i, attenuation_to_volume(abs_sin_attenuation((ushort)(i*4))) /128 + 256);
            //     var pos = new Vector2(i, Tables.logVol[i*256]/256);
            //     var pos2 = new Vector2(i/2, (float)(Tables.atbl[i*128]/256));
            //     DrawLine(pos, pos + new Vector2(0, 2), new Color("#ff0000"));
            //     DrawLine(pos2, pos2 + new Vector2(0, 2), new Color("#ffff00"));
            // }

            // DrawPolyline(pts, Color.ColorN("blue"),1,true);

            // var st = new ushort[1024];
            // for (int i=0; i < st.Length; i++)
            // {
            //     st[i] = attenuation_to_volume(abs_sin_attenuation((ushort)(i)));
            // }

    }


}
