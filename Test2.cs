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


    Operator op = new Operator();
    Operator op2 = new Operator();

    Chip c = new Chip();

    Node fromMidi;

    public override void _Ready()
    {
        // await ToSignal(GetTree(), "idle_frame");

        player = GetNode<AudioStreamPlayer>("Player");
        stream = (AudioStreamGenerator) player.Stream;
        buf = (AudioStreamGeneratorPlayback) player.GetStreamPlayback();

 

        player.Play();

        // op.NoteOn();
        // op2.NoteOn();
        op2.eg.tl = 0;
        op2.eg.ar = 64;
        op2.eg.sr = 63;

        fromMidi = Owner.GetNode("MIDI Control");

        fromMidi.Connect("note_on", this, "TryNoteOn");
        fromMidi.Connect("note_off", this, "TryNoteOff");

    }

    public void TryNoteOn(int midi_note, int velocity)
    {
        GD.Print("On?  ", midi_note, " ", velocity);
    }

    public void TryNoteOff(int midi_note)
    {
        GD.Print("Off?  ", midi_note);
    }


    public override void _Process(float delta)
    {
        // this.Text = String.Format("{0}, {1}:  {2}", op.env_counter.ToString(), op2.eg.attenuation.ToString(), op2.pg.increment.ToString());
        string rising;
        if (op2.egStatus>=0 && (int)op2.egStatus < op2.eg.rising.Length)
            rising = op2.eg.rising[(int)op2.egStatus] ? "Rising" : "Falling";
        else rising= "x";
        this.Text = String.Format("{0}, {1}:  {2}, {3}", op.env_counter.ToString(), op2.eg.attenuation.ToString(), op2.egStatus.ToString(), rising);

        // if (buf.GetSkips() > 0)
        //     fill_buffer();

    }

    // Called from EG controls to bus to the appropriate tuning properties.
    public void SetPG(int opTarget, string property, float val)
    {
        Operator op;
        if (opTarget ==1) op = this.op; else op = this.op2;

        try
        {
            op.pg.SetVal(property, val);
            op.pg.Recalc();
            // GD.Print(String.Format("Set op{0}.eg.{1} to {2}.", opTarget, property, val));
        } catch(NullReferenceException) {
            GD.PrintErr(String.Format("No property handler for op{0}.pg.{1}.", opTarget, property, val));
        }            
    }


    // Called from EG controls to bus to the appropriate envelope property.
    public void SetEG(int opTarget, string property, float val)
    {
        Operator op;
        if (opTarget ==1) op = this.op; else op = this.op2;

        try
        {
            op.eg.SetVal(property, unchecked((int) val));
            // GD.Print(String.Format("Set op{0}.eg.{1} to {2}.", opTarget, property, val));
        } catch(NullReferenceException) {
            GD.PrintErr(String.Format("No property handler for op{0}.eg.{1}.", opTarget, property, val));
        }            
    }

    public void SetWaveform(int opTarget, float val)
    {
        Operator op;
        if (opTarget ==1) op = this.op; else op = this.op2;

        try{
            op.SetOperatorType(Oscillator.waveFuncs[(int)val]);
        } catch(IndexOutOfRangeException e) {
            GD.PrintErr(String.Format("Waveform {0} not implemented", val));
        }
    }

    public void SetFeedback(int opTarget, float val)
    {
        Operator op;
        if (opTarget ==1) op = this.op; else op = this.op2;

        op.eg.feedback = (byte)val;
    }
    public void SetDuty(int opTarget, float val)
    {
        Operator op;
        if (opTarget ==1) op = this.op; else op = this.op2;

        op.eg.duty = (ushort)val;
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


            // // CPU TEST:  Clock the operators
            // for(int j=0; j<ops.Length; j++)
            // {
            //     ops[j].Clock();
            //     ops[j].RequestSample();
            // }





            // output[i].x = Tables.short2float[  (short) (op2.compute_fb( (ushort)(op.compute_fb(0)>>4))) + Tables.SIGNED_TO_INDEX ] ;
            output[i].x = Tables.short2float[  (short) (op2.RequestSample( (ushort)(op.RequestSample()>>2))) + Tables.SIGNED_TO_INDEX ] ;

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


        if (Input.IsActionJustPressed("ui_accept"))
        {
            op.NoteOn();
            op2.NoteOn();
        } else if (Input.IsActionJustPressed("ui_cancel")) {
            op.NoteOff();
            op2.NoteOff();
        }

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
