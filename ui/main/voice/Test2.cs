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

    Vector2[] bufferPool;

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

        bufferPool = new Vector2[(int)Math.Max(stream.MixRate * stream.BufferLength +2, stream.MixRate)];

        player.Play();

        // for (int i=4; i<c.Voice.egs.Length; i++)  c.Voice.egs[i].mute = true;

        fromMidi = Owner.GetNode("MIDI Control");

        // fromMidi.Connect("note_on", this, "TryNoteOn");
        // fromMidi.Connect("note_off", this, "TryNoteOff");

        fromMidi.Connect("note_on", this, "QueueNote");
        fromMidi.Connect("note_off", this, "QueueNote", new Godot.Collections.Array( new int[1] ) );
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
    { notes_queued[midi_note] = (byte)velocity; }

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
 



        if (buf.GetSkips() > 0)
            fill_buffer();


    }

    //Used by Op panels to populate the UI elements.  
    //TODO:  Consider having these convert from JSON to dict here and get rid of dupe methods in Envelope and Increments...
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
    public byte GetOscType(int opTarget){if(opTarget >= c.OpCount) return 0; else if (opTarget==-1) return (byte)c.Voice.lfo.OscType; else return c.Voice.oscType[opTarget];}

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
    public Godot.Collections.Dictionary GetAlgorithm(){ return c.Voice.GetAlgorithm(); }

    public Godot.Collections.Dictionary SetPreset(int preset, bool useSix)
    {
        c.Voice.SetOpCount(useSix? (byte)6 : (byte)4);
        c.Voice.alg = Algorithm.FromPreset((byte)preset, useSix? Algorithm.PresetType.DX : Algorithm.PresetType.Reface);

        return c.Voice.GetAlgorithm();
    }

    public void NormalizeVoice()
    {
        //Find connections to output.
        var outputs = new List<byte>(8);
        var loudest = Envelope.L_MAX;
        for(byte i=0; i<c.Voice.opCount; i++)
        {
            if(c.Voice.alg.connections[i]!=0) continue;
            outputs.Add(i);
            loudest = Math.Min(c.Voice.egs[i].tl, loudest);
        }

        // if (loudest==0) return;  //Voice is already normalized.
        foreach(byte i in outputs) c.Voice.egs[i].tl -= loudest;
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

    public void SetLFO(string property, float val){ c.Voice.lfo.SetVal(property, val); }

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
                var EG = c.Voice.egs[opTarget];
                var note = c.channels[i].midi_note;
                var velocity = c.channels[i].lastVelocity;
                ushort tl = (ushort) (val + EG.ksl[note] + EG.velocity[velocity]);  //This incurs a 'hidden' recalc cost from rTable thru the indexer

                var op = c.channels[i].ops[opTarget];
                if (op != null) op.eg.tl = tl; 
            }
        else
            for(int i=0; i<c.channels.Length; i++)
            {
                var op = c.channels[i].ops[opTarget]; 
                op?.eg.ChangeValue(property, val);
            }
    }

    public void SetFixedFreq(int opTarget, bool isFixed) { c.Voice.pgs[opTarget].fixedFreq = isFixed; } //Used when fixed/ratio toggled
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

    public void SetOscillator(int opTarget, float val)
    {
        if (opTarget==-1) //LFO
        {
            c.Voice.lfo.SetOscillatorType((byte)val);
            return;
        }
        c.Voice.SetOscillator(opTarget, (int)val);
    }

    public void SetFilterType(int opTarget, float val)
    {
        c.Voice.egs[opTarget].aux_func = (byte)val;  //Needed for GetOscTypeOrFunction to work
        for(int i=0; i<c.channels.Length; i++)
        {
            var op = c.channels[i].ops[opTarget] as Filter;
            if (op==null) continue;
            op.eg.aux_func = (byte)val;
            op.SetOscillatorType((byte) val);
        }

        var p = c.Voice.preview.ops[opTarget];
        p.eg.aux_func = (byte)val;
        p.SetOscillatorType((byte) val);

        RecalcFilter(opTarget, "all");
    }


    public void RecalcPreviewFilters() {for(int i=0; i<c.Voice.opCount; i++)  RecalcPreviewFilter(i);}

    public void RecalcPreviewFilter(int opTarget, System.Reflection.MethodInfo m=null)
    {
        const double RATIO = 16.35 / Global.BASE_HZ;
        {
            var op = c.Voice.preview.ops[opTarget] as Filter;
            if (op==null) return;
            // op.eg = new Envelope(c.Voice.egs[opTarget]);  //Make copy.
            op.eg.Configure(c.Voice.egs[opTarget]); 

            // op.SetOscillatorType(c.Voice.egs[opTarget].aux_func);
            op.eg.cutoff = Math.Max(2, op.eg.cutoff * RATIO);  //Preview note is midi_note 0!  Reduce cutoff a bunch to be more representative of A440.
            op.Reset();
            if(m==null) op.RecalcAll(); else m.Invoke(op, null);
        }         
    }


    public void RecalcFilter(int opTarget, string property)
    {
        System.Reflection.MethodInfo m;
        switch(property)
        {
            case "type":
                m = typeof(Filter).GetMethod("RecalcCoefficientsOnly");  break;
            case "cutoff":
                m = typeof(Filter).GetMethod("RecalcFrequency");  break;
            case "resonance":
                m = typeof(Filter).GetMethod("RecalcQFactor");  break;
            case "gain":
                m = typeof(Filter).GetMethod("RecalcGain");  break;
            case "all":  default:
                m = typeof(Filter).GetMethod("RecalcAll");   break;
        }

        //Set up preview for a recalc.
        RecalcPreviewFilter(opTarget, m);


        //Recalculate all channels applicable (including preview)
        for(int i=0; i<c.channels.Length; i++)
        {
            var op = c.channels[i].ops[opTarget] as Filter;
            m.Invoke(op, null);
        //     switch(property)
        //     {
        //         case "type":
        //             op.RecalcCoefficientsOnly();  break;
        //         case "cutoff":
        //             op.RecalcFrequency();  break;
        //         case "resonance":
        //             op.RecalcQFactor();  break;
        //         case "gain":
        //             op.RecalcGain();  break;
        //         case "all":  default:
        //             op.RecalcAll();   break;
        //     }
        }
    }

    public void SetBypass(int opTarget, bool val) {c.Voice.egs[opTarget].bypass = val;}
    public void SetMute(int opTarget, bool val) {c.Voice.egs[opTarget].mute = val;}
    public void SetVoiceData(string property, object val) {c.Voice.SetVal(property, val);}

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
        tbl?.SetScale(isMax? -1:value, isMax? value:-1);
    }
    public string GetTable(int opNum, RTableIntent intent)
    {
        opNum = (opNum < c.Voice.opCount)?  opNum: 0;
        IResponseTable tbl = c.Voice.egs[opNum].GetTable(intent);
        return tbl.ToJSONString();
    }

    //////////////////////////////    IO    ////////////////////////////////////

    public string VoiceAsJSONString() {return c.Voice.ToJSONString();}
    public string OperatorAsJSONString(int opNum) { return c.Voice.OpToJSON((byte)opNum, true).ToJSONString();}
    public Godot.Error PasteJSONData(string data) {return PasteJSONData(data, 0);}
    public Godot.Error PasteJSONData(string data, int target)
    {
        JSONParseResult result = Godot.JSON.Parse(data);
        if (result.Error != Error.Ok)
        {
            GD.PrintErr(String.Format("Error validating JSON: {0} (line {1})", result.ErrorString, result.ErrorLine) );
            return result.Error;
        }
        var o = PE_Json.JSONData.ReadJSON(data) as PE_Json.JSONObject;
        if (o == null)
        {
            System.Diagnostics.Debug.Print("PasteJSONData:  Failed to parse");
            return Error.ParseError;
        }

        //Determine what the nature of the JSON attempting to be parsed in actually is.  Possibilities:  1.Operator with intent, 2.Algorithm, 3.Voice
        if (o.HasItem("operators"))  //Probably Voice.  Create new voice and assign to chip.
        {
            var v = new Voice(o);
            c.SetVoice(v); 
            c.SetOpCount(v.opCount);

        } else if (o.HasItem("grid") || o.HasItem("connections")) {  //Probably an Algorithm

        } else if (o.HasItem("intent") && o.HasItem("envelope")) {  //Probably an Operator
            c.Voice.SetOpFromJSON((byte)target, o);

        } else { //Unrecognized JSON
            GD.Print("Paste Failed:  Unrecognized JSON");
        }

        return Error.Ok;
    }

    public Godot.Collections.Array GetSupportedFormats()
    {
        var output = new Godot.Collections.Array();

        foreach (VoiceBankImporter v in PE_ImportServer.loaders.Values)
        {
            output.Add(String.Format("*.{0}; {1}", v.fileFormat, v.description));
        }

        return output;
    }

    //Requests the import server to load a bank with specified voice import format and return 
    public Godot.Collections.Array RequestVoiceImport(string path)
    {
        var output = new Godot.Collections.Array();
        VoiceBankImporter v;
        var err = PE_ImportServer.TryLoad(path, out v);

        if (err !=IOErrorFlags.OK) return output;

        for(int i=0; i<v.bank.Length; i++)
        {  //Get the names of all the instruments in the bank and return them.
            // PE_Json.JSONObject o = (PE_Json.JSONObject) PE_Json.JSONData.ReadJSON(v.bank[i]);
            // var alg = (PE_Json.JSONObject) o.GetItem("algorithm");
            // output.Add( String.Format("{0}: {1}", i, o.GetItem("name", "unnamed")), alg.GetItem("compatiblePreset", -1) );

            output.Add( v.bank[i] );
        }

        return output;
    }


    ///////////////////////////////////  PREVIEW  ///////////////////////////////////
    public float[] CalcPreview() {return c.Voice.CalcPreview();}

    public bool is_quiet() {return c.ChannelsAreFree;}
    public int connections_to_output() {return c.Voice.alg.NumberOfConnectionsToOutput;}
    public byte[] GetCarriers() {return c.Voice.alg.GetCarriers();}
    public byte[] GetModulators(byte opNum) {  //Used by the UI to arrange operators in the kanban by stack.
        var m = c.Voice.alg.GetModulators(opNum);
        var output=new byte[m.Count]; 
        m.CopyTo(output); return output;
        }


    ///////////////////////////////////    WAVEFORM    /////////////////////////////////////
    public Godot.Collections.Array AddWave(int tableSize) {
        var sampleWidth = Tools.Ctz(tableSize);  //Snap table size to the nearest power of 2 by specifying the bit width to wavetable.AddBank().
        c.Voice.wavetable.AddBank((byte) Math.Clamp(sampleWidth, 4, 10)); 
        return new Godot.Collections.Array(c.Voice.wavetable.GetTable(c.Voice.wavetable.NumBanks-1));
        }
    public void RemoveWave(int idx) {c.Voice.wavetable.RemoveBank(idx);}

    public int NumBanks {get => c.Voice.wavetable.NumBanks;}

    public void SetWaveBank(int opNum, int bank) 
    {
        c.Voice.egs[opNum].wavetable_bank = (byte) bank;
    }

    public Godot.Collections.Array GetWave(int bank)
    {
        var tbl = c.Voice.wavetable;
        var output = new Godot.Collections.Array();
        if (tbl.NumBanks <=0) return output;
        if (bank<=-1)  //Get all banks
            for(int i=0; i<tbl.NumBanks; i++)
                output.Add(tbl.GetTable(i));
        else if (bank<tbl.NumBanks)
            output= new Godot.Collections.Array(c.Voice.wavetable.GetTable(bank));

        return output;
    }

    public void SetWave(int bank, int index, int value)
    {
        var tbl = c.Voice.wavetable;
        if (tbl.NumBanks <=0) return;

        tbl.SetTable(bank, index, (short) value);
    }
    public void SetWave(int bank, int[] input)
    {
        var tbl = c.Voice.wavetable;
        if (tbl.NumBanks <=0) return;

        // var output = (short[]) Convert.ChangeType(input, typeof(short[]));
        var output = Array.ConvertAll(input, Convert.ToInt16);
        tbl.SetTable(bank, output);
    }



    ///////////////////////////////////    BUFFER    /////////////////////////////////////

    float previousSample=0;
    void fill_buffer()
    {
        var frames= buf.GetFramesAvailable();
        // var output = new Vector2[frames];
        var segment = new ArraySegment<Vector2>(bufferPool, 0, frames);
        var output = segment.Array;
        
        for (int i=0; i<frames;  i++)
        {
            c.Clock();


            // output[i].x = c.RequestSampleF();
            output[i].x = Tools.Lerp(c.RequestSampleF(), previousSample, 0.51f); //Filtered in an attempt to level off the high end
            output[i].y = output[i].x;
            previousSample = output[i].x;

            output[i] *= new Vector2(c.Voice.panL, c.Voice.panR);

        }

        //Calculate scope
        for (int i=0; i < output.Length; i++)
        {
            if (pts.Count >= scopeLen)  break;
            var h= scopeHeight/2;
            pts.Enqueue(output[i].x * h + h);
        }

        buf.PushBuffer(segment.ToArray());
    }


    public override void _PhysicsProcess(float delta)
    {
        base._PhysicsProcess(delta);

        if(Visible)
        {
            this.Text = c.channels[0].ToString();
            // if (c.channels[0].ops[0].pg.increment > int.MaxValue) 
                // this.SelfModulate = new Color("ff0000"); 
            // else
                // this.SelfModulate = new Color("ffffff"); 


            var info = GetNode<Label>("ChInfo");
            info.Text = c.ToString();
            // info.Text = FramesPerOscillation();
            Update();            
        }
    }



    ///////////////////////////////////////////////// SCOPE /////////////////////////////////////////////////
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
