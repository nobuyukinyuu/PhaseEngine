using Godot;
using System;
using System.Diagnostics.Tracing;
using System.Collections.Generic;
using System.Linq;

public sealed class SystemRuntimeEventListener : EventListener
{
  public double Value { get; private set; }

  protected override void OnEventSourceCreated(EventSource eventSource)
  {
    if (eventSource.Name.Equals("System.Runtime"))
      EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.All, new Dictionary<string, string> { {"EventCounterIntervalSec", "1"} });
  }

  protected override void OnEventWritten(EventWrittenEventArgs eventData)
  {
    if (eventData.Payload == null || eventData.Payload.Count == 0)
      return;
    if (eventData.Payload[0] is IDictionary<string, object> eventPayload && 
        eventPayload.TryGetValue("Name", out var nameData) && nameData is string name && name == "cpu-usage")
    {
      if (eventPayload.TryGetValue("Mean", out var value))
      {
        if (value is double dValue)
        {
          Value = dValue;
          base.OnEventWritten(eventData);
        }
      }
    }
  }
}

public class CPUMon : Label
{

// cpuCounter = new EventCounter("Processor", "% Processor Time", "_Total");
// ramCounter = new EventCounter("Memory", "Available MBytes");

    SystemRuntimeEventListener listener;
    SceneTreeTimer timer;  float updateTimeout = 0.5f;

    public override void _Ready()
    {
         listener = new SystemRuntimeEventListener();
         timer =  GetTree().CreateTimer(updateTimeout);
         timer.Connect("timeout", this, "UpdateCounters");
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    ulong lastTick;
    List<ulong> ticks = new List<ulong>(512);
    public override void _Process(float delta)
    {
        var t = OS.GetTicksUsec();

        ticks.Add(t-lastTick);
        lastTick = t;
        // Text =  "CPU: " + listener.Value.ToString() + "%" ;

    }

    public void UpdateCounters()
    {
        timer =  GetTree().CreateTimer(updateTimeout);
        timer.Connect("timeout", this, "UpdateCounters");

        Text = "Avg Tick time: " + Avg(ticks).ToString() + " uSec\n";
        Text +=  "Static Mem: " + (OS.GetStaticMemoryUsage()/(float)0x10_0000).ToString() + " Mb" ;

        ticks.Clear();
    }

    float Avg<T>(List<T> arr) where T : struct
    {
        long sum = 0;
        for (int i=0; i < arr.Count; i++)
        {
            sum += (long) Convert.ToInt64(arr[i]);
        }

        return sum / (float)arr.Count;        
    }
}
