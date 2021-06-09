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


    Chip c = new Chip(1,1);
    long[] lastID = new long[128];  //Keeps track of the last ID pressed on a specified note, to turn it off when a noteOff event is detected.

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

        fromMidi = Owner.GetNode("MIDI Control");

        fromMidi.Connect("note_on", this, "TryNoteOn");
        fromMidi.Connect("note_off", this, "TryNoteOff");

    }

    public void NoteLow(bool on)
    {
        if(on) c.NoteOn(12); else c.NoteOff(12);
    }

    public void TryNoteOn(int midi_note, int velocity)
    {
        lastID[midi_note] = c.NoteOn((byte)midi_note);
        GD.Print("On?  ", midi_note, " ", velocity, ";  id=", lastID[midi_note]);
    }

    public void TryNoteOff(int midi_note)
    {
        GD.Print("Off?  ", midi_note, ";  id=", lastID[midi_note]);
        // c.NoteOff((byte)midi_note);  //Inefficient!! Consider NoteOff to the last event only.
        c.NoteOff(lastID[midi_note]);  
    }


    public override void _Process(float delta)
    {
        this.Text = c.channels[0].ToString();
        Update();


        if (buf.GetSkips() > 0)
            fill_buffer();

        var info = GetNode<Label>("ChInfo");
        info.Text = Performance.GetMonitor(Performance.Monitor.AudioOutputLatency).ToString();

    }

    // Called from EG controls to bus to the appropriate tuning properties.
    public void SetPG(int opTarget, string property, float val)
    {
        c.Voice.SetPG(opTarget, property, val);
    }
    // Called from EG controls to bus to the appropriate envelope property.
    public void SetEG(int opTarget, string property, float val)
    {
        c.Voice.SetEG(opTarget, property, val);
    }


    public void SetWaveform(int opTarget, float val)
    {
        c.Voice.SetWaveform(opTarget, val);
    }

    // public void SetFeedback(int opTarget, float val)
    // {
    //     Operator op;
    //     if (opTarget ==1) op = this.op; else op = this.op2;

    //     op.eg.feedback = (byte)val;
    // }
    // public void SetDuty(int opTarget, float val)
    // {
    //     Operator op;
    //     if (opTarget ==1) op = this.op; else op = this.op2;

    //     op.eg.duty = (ushort)val;
    // }


    void fill_buffer()
    {
        var frames= buf.GetFramesAvailable();
        var output = new Vector2[frames];

        for (int i=0; i<frames;  i++)
        {
            // output[i].x = Tables.short2float[ Oscillator.CrushedSine((ulong)accumulator, (ushort) bitCrush.Value) + Tables.SIGNED_TO_INDEX ];
            c.Clock();





            // output[i].x = Tables.short2float[  (short) (op2.compute_fb( (ushort)(op.compute_fb(0)>>4))) + Tables.SIGNED_TO_INDEX ] ;
            // output[i].x = Tables.short2float[  (short) (op2.RequestSample( (ushort)(op.RequestSample()>>2))) + Tables.SIGNED_TO_INDEX ] ;

            output[i].x = c.RequestSampleF();
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
