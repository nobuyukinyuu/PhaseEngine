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

    Operator[] ops = new Operator[128];

    public override void _Ready()
    {
        // await ToSignal(GetTree(), "idle_frame");

        player = GetNode<AudioStreamPlayer>("Player");
        stream = (AudioStreamGenerator) player.Stream;
        buf = (AudioStreamGeneratorPlayback) player.GetStreamPlayback();
        acc_inc = GetNode<HSlider>("../Accumulator");
        bitCrush = GetNode<HSlider>("../BitCrush");

        op.SetOperatorType(Oscillator.Sine);
        op2.SetOperatorType(Oscillator.Sine);
 
        // op.NoteSelect(0);
        op.FreqSelect(440);
        op2.FreqSelect(880);

        player.Play();

        op.NoteOn();
        op2.NoteOn();

        for(int i=0; i<ops.Length; i++)
            ops[i] = new Operator();
    }

    public override void _Process(float delta)
    {
        //  this.Text = Engine.GetIdleFrames().ToString() + ":=   " + Oscillator.Sine(Engine.GetIdleFrames(), 1).ToString();

        // this.Text = Engine.GetIdleFrames().ToString() + ":  " + Tables.sin[Engine.GetIdleFrames() & Tables.SINE_TABLE_MASK].ToString();
        // this.Text = ((short.MaxValue/stream.MixRate * acc_inc.Value)).ToString();

        short samp2=op2.RequestSample();
        // this.Text = Tools.ToBinStr(op.compute_volume(0,0)) + " = " + op.compute_volume(0,0) + "\n" + op.noteIncrement.ToString();
        // this.Text = Oscillator.gen2.ToString() + " = " + samp2.ToString() + "\n" + op.noteIncrement.ToString();
        this.Text = String.Format("{0}, {1}:  {2}", op.env_counter.ToString(), op.eg.attenuation.ToString(), op.eg.status.ToString());

        if (buf.GetSkips() > 0)
            fill_buffer();

    }

    // Called from EG controls to bus to the appropriate envelope property.
    public bool SetEG(int opTarget, string property, float val)
    {
        Operator op;
        if (opTarget ==1) op = this.op; else op = this.op2;

        op.eg[property] = unchecked((int) val);
        // GD.Print(String.Format("Set op{0}.{1} to {2}.", opTarget, property, val));
        return true;
    }

    public Envelope GetEG(int opTarget)
    {
        if (opTarget ==1) return this.op.eg; else return this.op2.eg;
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


            // // CPU TEST:  Clock the operators
            // for(int j=0; j<ops.Length; j++)
            // {
            //     ops[j].Clock();
            //     ops[j].RequestSample();
            // }



            // output[i].x = Tables.short2float[ samp + Tables.SIGNED_TO_INDEX ];


            // output[i].x = attenuation_to_volume(unchecked((ushort)(samp>>0))) / 8192f ;
            // output[i].x = Tables.short2float[  (short)(op.compute_volume((ushort)unchecked((samp2) >>6),  0)<<1) + Tables.SIGNED_TO_INDEX ] ;
            
            // output[i].x = Tables.short2float[  (short)(op.compute_fb((ushort)unchecked((samp2) >>6) )<<1) + Tables.SIGNED_TO_INDEX ] ;
            output[i].x = Tables.short2float[  (short) (op2.compute_volume( (ushort)(op.compute_fb(0)>>4), 0)) + Tables.SIGNED_TO_INDEX ] ;
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
        op2.FreqSelect(acc_inc.Value);
        op.FreqSelect(acc_inc.Value * 4);
        Update();

        if (Input.IsActionJustPressed("ui_accept"))
        {
            op.NoteOn();
            op2.NoteOn();
        }

        op.feedback = (byte) GetNode<HSlider>("../Feedback").Value;

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


    }


}
