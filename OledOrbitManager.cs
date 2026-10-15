using System;
using System.Threading;
using System.Threading.Tasks;

namespace OverscanFix;

public class OledOrbitManager : IDisposable
{
    private readonly OverscanEngine _engine;
    private readonly AppSettings _settings;
    private CancellationTokenSource? _cts;
    private int _step = 0;

    public OledOrbitManager(OverscanEngine engine, AppSettings settings)
    {
        _engine = engine;
        _settings = settings;
    }

    public void Start()
    {
        Stop();
        if (!_settings.EnableOledPixelOrbit) return;

        _cts = new CancellationTokenSource();
        Task.Run(() => OrbitLoopAsync(_cts.Token));
        Logger.Info($"OLED Pixel Orbit started: interval {_settings.OledOrbitIntervalMinutes} min, max shift {_settings.OledOrbitMaxShiftPx} px.");
    }

    public void Stop()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }

    private async Task OrbitLoopAsync(CancellationToken token)
    {
        // 8-direction orbit pattern around center:
        // (0,0) -> (1,0) -> (1,1) -> (0,1) -> (-1,1) -> (-1,0) -> (-1,-1) -> (0,-1) -> (1,-1)
        int[,] deltas = new int[,]
        {
            { 0, 0 },
            { 1, 0 },
            { 1, 1 },
            { 0, 1 },
            { -1, 1 },
            { -1, 0 },
            { -1, -1 },
            { 0, -1 },
            { 1, -1 }
        };

        while (!token.IsCancellationRequested)
        {
            int intervalMs = Math.Max(1, _settings.OledOrbitIntervalMinutes) * 60 * 1000;
            try
            {
                await Task.Delay(intervalMs, token);
            }
            catch (TaskCanceledException)
            {
                break;
            }

            if (!_settings.EnableOledPixelOrbit || !_engine.IsApplied)
                continue;

            _step = (_step + 1) % 9;
            int maxPx = Math.Max(1, _settings.OledOrbitMaxShiftPx);
            int dx = deltas[_step, 0] * maxPx;
            int dy = deltas[_step, 1] * maxPx;

            var baseProf = _settings.CurrentProfile;
            int l = Math.Max(0, baseProf.LeftPixels + dx);
            int r = Math.Max(0, baseProf.RightPixels - dx);
            int t = Math.Max(0, baseProf.TopPixels + dy);
            int b = Math.Max(0, baseProf.BottomPixels - dy);

            Logger.Info($"OLED Orbit shift: step {_step}, dx={dx}, dy={dy} -> L{l} T{t} R{r} B{b}");
            _engine.ApplyOverscan(l, t, r, b,
                baseProf.EnableWorkAreaAdjustment,
                baseProf.EnableBlackBorders,
                baseProf.MaskColorHex,
                baseProf.MaskOpacity);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
