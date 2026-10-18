using System.Diagnostics;

namespace AshenReliquary.App.Rendering;

/// <summary>Running averages of how long each render stage takes, in milliseconds.</summary>
public sealed class RenderStats
{
    private readonly Stopwatch _clock = new();
    private readonly Dictionary<string, double> _totals = [];
    private double _last;

    public int Frames { get; private set; }

    public void Begin()
    {
        Frames++;
        _clock.Restart();
        _last = 0;
    }

    public void Mark(string stage)
    {
        var now = _clock.Elapsed.TotalMilliseconds;
        _totals[stage] = _totals.GetValueOrDefault(stage) + (now - _last);
        _last = now;
    }

    /// <summary>Time spent outside the draw (the world's update), added to the same averages.</summary>
    public void Add(string stage, double ms) => _totals[stage] = _totals.GetValueOrDefault(stage) + ms;

    public IReadOnlyDictionary<string, double> AverageMs =>
        _totals.ToDictionary(kv => kv.Key, kv => Frames == 0 ? 0 : kv.Value / Frames);

    public double AverageFrameMs => AverageMs.Values.Sum();

    public void Reset()
    {
        _totals.Clear();
        Frames = 0;
    }

    public override string ToString() =>
        $"{AverageFrameMs:0.0} ms/frame: " + string.Join(", ", AverageMs.Select(kv => $"{kv.Key} {kv.Value:0.0}"));
}
