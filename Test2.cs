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

    const int scopeLen = 256;
    const int scopeHeight = 128;


    Chip c = new Chip(6,3);
    long[] lastID = new long[128];  //Keeps track of the last ID pressed on a specified note, to turn it off when a noteOff event is detected.

    Node fromMidi;

    public override void _Ready()
    {
        // await ToSignal(GetTree(), "idle_frame");

        player = GetNode<AudioStreamPlayer>("Player");
        stream = (AudioStreamGenerator) player.Stream;
        buf = (AudioStreamGeneratorPlayback) player.GetStreamPlayback();

        stream.MixRate = Global.MixRate;

        player.Play();

        // for (int i=4; i<c.Voice.egs.Length; i++)  c.Voice.egs[i].mute = true;

        fromMidi = Owner.GetNode("MIDI Control");

        // fromMidi.Connect("note_on", this, "TryNoteOn");
        // fromMidi.Connect("note_off", this, "TryNoteOff");

        fromMidi.Connect("note_on", this, "QueueNote");
        fromMidi.Connect("note_off", this, "QueueNote", new Godot.Collections.Array( new int[1] ) );

    }

    public void NoteLow(bool on)
    {
        if(on) c.NoteOn(12); else c.NoteOff(12);
    }

    public void TryNoteOn(int midi_note, int velocity)
    {
        lastID[midi_note] = c.NoteOn((byte)midi_note, (byte) velocity);
        GD.Print("On?  ", midi_note, " at ", velocity, ";  id=", lastID[midi_note]);
    }

    public void TryNoteOff(int midi_note)
    {
        // GD.Print("Off?  ", midi_note, ";  id=", lastID[midi_note]);
        // c.NoteOff((byte)midi_note);  //Inefficient!! Consider NoteOff to the last event only.
        c.NoteOff(lastID[midi_note]);  
    }

    System.Collections.Concurrent.ConcurrentDictionary<int, byte> notes_queued = new System.Collections.Concurrent.ConcurrentDictionary<int, byte>();
    public void QueueNote(int midi_note, int velocity)  //Set Velocity to 0 to trigger noteOff
    {
        notes_queued[midi_note] = (byte)velocity;
    }


    public override void _Process(float delta)
    {
        //Check for notes queued and ready to go
        var queue = new Dictionary<int, byte>(notes_queued);  //Copy the queue to our thread so it's not modified while we're doing shit
        notes_queued.Clear();
        foreach (int note in queue.Keys)
        {
            if (queue[note] > 0) //NoteOn
            {
                TryNoteOn(note, queue[note]);
            } else {
                TryNoteOff(note);
            }
        }
 
        this.Text = c.channels[0].ToString();
        Update();


        if (buf.GetSkips() > 0)
            fill_buffer();

        var info = GetNode<Label>("ChInfo");
        info.Text = c.ToString();

    }


    public Godot.Collections.Dictionary GetOpValues(int whichKind, int opTarget)
    {
        switch(whichKind)
        {
            case 0: default:  //EG
                return c.Voice.GetEG(opTarget);
            case 1:  //PG
                return c.Voice.GetPG(opTarget);
        }
    }

    public int GetOpType(int opTarget){ if(opTarget >= c.opCount) return 0; else return c.Voice.opType[opTarget]; }
    public int GetOpCount() { return c.opCount; }


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

    public void SetFixedFreq(int opTarget, bool isFixed) { c.Voice.pgs[opTarget].fixedFreq = isFixed; }
    public void SetFrequency(int opTarget, float freq)
    {
        c.Voice.pgs[opTarget].FreqSelect(freq);
    }

    public void SetWaveform(int opTarget, float val)
    {
        c.Voice.SetWaveform(opTarget, (int)val);
    }

    public void SetAlgorithm(Godot.Collections.Dictionary d){   c.SetAlgorithm(d); GD.Print("Setting algo...");    }

    public void SetBypass(int opTarget, bool val) {c.Voice.egs[opTarget].bypass = val;}
    public void SetMute(int opTarget, bool val) {c.Voice.egs[opTarget].mute = val;}


    ///summary:  Updates a single column in an rTable.
    public void UpdateTable(int opNum, int column, int value, RTableIntent intent)
    {
        IResponseTable tbl = c.Voice.egs[opNum].GetTable(intent);
        tbl.UpdateValue((byte) column, (byte) value);
    }
    ///summary:  Updates an rTable.
    public void SetTable(int opNum, Godot.Collections.Array input, RTableIntent intent)
    {
        IResponseTable tbl = c.Voice.egs[opNum].GetTable(intent);

        for(int i=0; i<input.Count; i++)
        {
            tbl.UpdateValue((byte) i, Convert.ToByte(input[i]));
        }
    }
    public void SetTableMinMax(int opNum, int value, bool isMax, RTableIntent intent)
    {
        IResponseTable tbl = c.Voice.egs[opNum].GetTable(intent);
        tbl.SetScale(isMax? -1:value, isMax? value:-1);
    }




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
