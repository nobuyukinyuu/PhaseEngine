using Godot;
using System;
using PhaseEngine;
using System.Collections.Generic;

public class Test2 : Label
{
    // Called when the node enters the scene tree for the first time.

    AudioStreamGenerator stream;
    AudioStreamGeneratorPlayback buf;
    AudioStreamPlayer player;

    const int scopeLen = 256;
    const int scopeHeight = 128;


    Chip c = new Chip(6,4);
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

        System.Text.StringBuilder s = new System.Text.StringBuilder();
        // for(ushort k=8; k<11; k++)
            for (ushort j=0; j<16; j++)
            {
                for (ushort i=0; i<16; i++)
                {
                    // s.Append( Tables.attenuation_to_volume((ushort)(k*256+j*16+i)) );
                    s.Append((Tables.s_power_table[j*16+i] & 0x3FF));
                    s.Append(", ");
                }
                s.Append("\n");
            }
        // System.Diagnostics.Debug.Print(s.ToString());
        System.Diagnostics.Debug.Print("");
        OS.Clipboard = s.ToString();
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
 
        if(Visible)
        {
            this.Text = c.channels[0].ToString();
            if (c.channels[0].ops[0].pg.increment > int.MaxValue) 
                // this.AddColorOverride("font_color", new Color("ff0000")); 
                this.SelfModulate = new Color("ff0000"); 
            else
                // this.AddColorOverride("font_color", new Color("ffffff"));
                this.SelfModulate = new Color("ffffff"); 


            var info = GetNode<Label>("ChInfo");
            info.Text = c.ToString();
            // info.Text = FramesPerOscillation();
            Update();            
        }


        if (buf.GetSkips() > 0)
            fill_buffer();


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

    public byte GetOpIntent(int opTarget){ if(opTarget >= c.OpCount) return 0; else return (byte)c.Voice.alg.intent[opTarget]; }
    public byte GetOscType(int opTarget){ if(opTarget >= c.OpCount) return 0; else return c.Voice.oscType[opTarget]; }

    public byte GetOscTypeOrFunction(int opTarget)  //Returns a value corresponding to the primary function of the operator.  For determining preview icons, etc...
    {
        switch(c.Voice.alg.intent[opTarget])
        {
            case OpBase.Intents.FM_OP:
                return GetOscType(opTarget);
            case OpBase.Intents.FILTER:
            case OpBase.Intents.BITWISE:
                return c.Voice.egs[opTarget].aux_func;
            case OpBase.Intents.WAVEFOLDER:  //Return an encoded value corresponding to gain instead.
                var whole = (int)c.Voice.egs[opTarget].gain;
                var frac = (int)Math.Round((c.Voice.egs[opTarget].gain - whole) * 10);

                //Guarantee that the high bits are outside of the normal oscillator range.  Add one to gain.
                // whole ++;  
                return (byte)((whole << 4) | frac);  //4.4 fixed point value.  Whole value should be 1-11 now, and frac 0-9.


            default:
                return 0xFF;
        }
    }
    public int GetOpCount() { return c.OpCount; }
    public Godot.Collections.Dictionary SetOpCount(int opCount)
    {
        c.SetOpCount((byte)opCount, c.Voice);
        var alg = c.Voice.GetAlgorithm();
        return alg;
    }

    public void SetAlgorithm(Godot.Collections.Dictionary d){   c.SetAlgorithm(d); /*GD.Print("Setting algo...");*/    }
    public Godot.Collections.Dictionary GetAlgorithm(Godot.Collections.Dictionary d){ return c.Voice.GetAlgorithm(); }

    public Godot.Collections.Dictionary SetPreset(int preset, bool useSix)
    {
        c.Voice.SetOpCount(useSix? (byte)6 : (byte)4);
        c.Voice.alg = Algorithm.FromPreset((byte)preset, useSix);

        //DEBUG:  REMOVE ME
        // var presets = useSix?  Algorithm.dx_presets : Algorithm.reface_presets;
        // System.Diagnostics.Debug.Print(presets[preset].ToString());
        // GD.Print(presets[preset].ToString());

        return c.Voice.GetAlgorithm();

    }


    //FIXME:  This function is hacky and only sets the chip's channel ops to the processed value.  Voice preview updates this manually as well.
    //        Channel.NoteOn doesn't set the delegate because c# has no fuckin switch fallthru and I don't want to use an If statement right now
    //        It's going to need to be fixed when creating a new BitwiseOperator in any other context (like music replayers)
    public void SetBitwiseFunc(byte opTarget, int val)
    {
        foreach(Channel ch in c.channels)
        {
            var op = ch.ops[opTarget] as BitwiseOperator;
            if (op==null) continue;
            op.OpFuncType = (byte)val;  //Property has hidden side effect of updating the delegate
            c.Voice.egs[opTarget].aux_func = (byte)val;
        }
    }

    public bool SetOpIntent(int opTarget, int _intent)
    {
        var intent = (OpBase.Intents) _intent;
        return c.UpdateIntent((byte)opTarget, (OpBase.Intents) _intent);

    }

    public void SetLFO(string property, float val)
    {
        c.Voice.lfo.SetVal(property, val);
    }

    // Called from EG controls to bus to the appropriate tuning properties.
    public void SetPG(int opTarget, string property, float val)
    {
        c.Voice.SetPG(opTarget, property, val);
        c.Voice.pgs[opTarget].Recalc();  //This isn't normally done by Voice but we need it for the UI tooltip mult preview, so we do it.

        //For live feedback of changes in the PG value.  Inefficient;  DON'T use this in production!
        for(int i=0; i<c.channels.Length; i++)
        {
            c.channels[i].ops[opTarget].pg.SetVal(property, val);
            c.channels[i].ops[opTarget].pg.Recalc();
        }

    }
    // Called from EG controls to bus to the appropriate envelope property.
    public void SetEG(int opTarget, string property, float val)
    {
        c.Voice.SetEG(opTarget, property, val);

        //For live feedback of changes in the EG value.  Inefficient;  DON'T use this in production!
        if (opTarget >= c.Voice.opCount) return;

        if (property=="tl")  //Recalc level from rTables if necessary.
            for(int i=0; i<c.channels.Length; i++)
            {
                var newEG = new Envelope(c.Voice.egs[opTarget]);
                var note = c.channels[i].midi_note;
                var velocity = c.channels[i].lastVelocity;
                ushort tl = (ushort) (val + newEG.ksl[note] + newEG.velocity[velocity]);  //This incurs a 'hidden' recalc cost from rTable thru the indexer
                newEG.tl = tl;

                var op = c.channels[i].ops[opTarget];
                if (op != null) op.eg = newEG; 
            }
        else
            for(int i=0; i<c.channels.Length; i++)
            {
                var op = c.channels[i].ops[opTarget]; 
                op?.eg.ChangeValue(property, val);
            }
    }

    public void SetFixedFreq(int opTarget, bool isFixed) { c.Voice.pgs[opTarget].fixedFreq = isFixed; }
    public void SetFrequency(int opTarget, float freq)
    {
        c.Voice.pgs[opTarget].FreqSelect(freq);

        //For live feedback of changes in the frequency value.  Inefficient;  DON'T use this in production!
        for(int i=0; i<c.channels.Length; i++)
        {
            c.channels[i].ops[opTarget].pg.FreqSelect(freq);
            c.channels[i].ops[opTarget].pg.Recalc();
        }

    }

    public void SetFeedback(int opTarget, int val) 
    {
        SetEG(opTarget, "feedback", val);

        //Force a re-check of the oscillator type, which will set the feedback functionality on or off depending on the current value.
        //This is inefficient and not necessary for non-live input as the function is checked on NoteOn() anyway. But this changes it live.
        for(int i=0; i<c.channels.Length; i++)
            c.channels[i].ops[opTarget].SetOscillatorType(GetOscType(opTarget));
    }

    public void SetWaveform(int opTarget, float val)
    {
        if (opTarget==-1) //LFO
        {
            c.Voice.lfo.SetOscillatorType((byte)val);
            return;
        }
        c.Voice.SetWaveform(opTarget, (int)val);
    }

    public void SetFilterType(int opTarget, float val)
    {
        // c.Voice.preview.ops[opTarget].SetOscillatorType((byte)val);  //This updates the reference for all EGs and sets the delegate for preview only.
        for(int i=0; i<c.channels.Length; i++)
        {
            var op = c.channels[i].ops[opTarget] as Filter;
            if (op==null) continue;
            op.SetOscillatorType((byte) val);
        }

        RecalcFilter(opTarget);
    }

    public void RecalcFilter(int opTarget)
    {
        for(int i=0; i<c.channels.Length; i++)
        {
            var op = c.channels[i].ops[opTarget] as Filter;
            // if (op==null) continue;
            // op.SetOscillatorType(c.Voice.egs[opTarget].aux_func);  //FixMe:  may be redundant
            // op.eg.cutoff = c.Voice.egs[opTarget].cutoff;
            // op.eg.resonance = c.Voice.egs[opTarget].resonance;
            // op.eg.gain = c.Voice.egs[opTarget].gain;
            // op.eg.duty = c.Voice.egs[opTarget].duty;
            op.Recalc();
        }

        //Recalc preview.
        const double RATIO = 16.35 / Global.BASE_HZ;
        {
            var op = c.Voice.preview.ops[opTarget] as Filter;
            op.eg = new Envelope(c.Voice.egs[opTarget]);  //Make copy.
            // op.SetOscillatorType(c.Voice.egs[opTarget].aux_func);
            op.eg.cutoff = Math.Max(2, op.eg.cutoff * RATIO);  //Preview note is midi_note 0!  Reduce cutoff a bunch to be more representative of A440.
            op.Reset();
            op.Recalc();
        } 
    }

    public void SetBypass(int opTarget, bool val) {c.Voice.egs[opTarget].bypass = val;}
    public void SetMute(int opTarget, bool val) {c.Voice.egs[opTarget].mute = val;}


    //////////////////////////////    rTABLE    ////////////////////////////////////
    ///summary:  Updates a single column in an rTable.
    public void UpdateTable(int opNum, int column, int value, RTableIntent intent)
    {
        IResponseTable tbl = c.Voice.egs[opNum].GetTable(intent);
        tbl.UpdateValue((byte) column, (ushort) value);
    }
    ///summary:  Updates an rTable.
    public void SetTable(int opNum, Godot.Collections.Array input, RTableIntent intent)
    {
        IResponseTable tbl = c.Voice.egs[opNum].GetTable(intent);

        for(int i=0; i<input.Count; i++)
        {
            tbl.UpdateValue((byte) i, Convert.ToUInt16(input[i]));
        }
    }
    public void SetTableMinMax(int opNum, int value, bool isMax, RTableIntent intent)
    {
        IResponseTable tbl = c.Voice.egs[opNum].GetTable(intent);
        tbl.SetScale(isMax? -1:value, isMax? value:-1);
    }
    public string GetTable(int opNum, RTableIntent intent)
    {
        opNum = (opNum < c.Voice.opCount)?  opNum: 0;
        IResponseTable tbl = c.Voice.egs[opNum].GetTable(intent);
        return tbl.ToJSONString();
    }


    public float[] CalcPreview() {return c.Voice.CalcPreview();}
    // public string FramesPerOscillation()
    // {
    //     const int PRECISION=4;
    //     var fpc = new long[c.opCount];  //Frames per cycle
    //     var sb = new System.Text.StringBuilder();
    //     for(int i=0; i<c.opCount; i++)
    //     {
    //         // fpc[i] = Tools.ToFixedPoint((float)(Global.MixRate / c.Voice.pgs[i].hz), PRECISION );
    //         // fpc[i] = (long)Math.Round(Global.MixRate / c.Voice.pgs[i].hz);
    //         fpc[i] = (long)Math.Round(c.Voice.pgs[i].scopeMult * 100);
    //         // sb.Append((fpc[i]>>PRECISION).ToString() + ", ");
    //         sb.Append((fpc[i]).ToString() + ", ");
    //     }
    //     var lcm = Tools.LCM(fpc);
    //     // sb.Append("\n" + lcm.ToString() + ", " + (int)Tools.FromFixedPoint((long)lcm, PRECISION) );
    //     sb.Append("\n" + lcm.ToString() + ", " + (lcm/Global.MixRate) );
    //     return sb.ToString();
    // }

    public bool is_quiet() {return c.ChannelsAreFree;}
    public int connections_to_output() {return c.Voice.alg.NumberOfConnectionsToOutput;}


    ///////////////////////////////////    BUFFER    /////////////////////////////////////
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
