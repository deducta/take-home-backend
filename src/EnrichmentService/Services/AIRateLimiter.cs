namespace EnrichmentService.Services;

public class AIRateLimiter
{
    private const int LimitPerSecond = 18;
    
    private readonly Queue<DateTime> _requestTimes = new();
    
    private readonly object _lock = new();
    
    // Space request starts to avoid bursts that can exceed
    // the downstream API's rolling-window limit.
    private static readonly TimeSpan MinSpacing =
        TimeSpan.FromMilliseconds(65);

    private DateTime _nextAllowedAt = DateTime.MinValue;
    
    public async Task WaitForPermitAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryAcquirePermit())
            {
                return;
            }

            await Task.Delay(100, cancellationToken);
        }
    }
    
    private bool TryAcquirePermit()
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;

            RemoveExpiredRequests(now);

            if (IsRequestAllowed(now))
            {
                AddRequest(now);
                _nextAllowedAt = now.Add(MinSpacing);
                
                return true;
            }

            return false;
        }
    }
    
    private bool IsRequestAllowed(DateTime now)
    {
        if (_requestTimes.Count >= LimitPerSecond)
        {
            return false;
        }
        
        if (_nextAllowedAt > now)
        {
            return false;
        }
        
        return true;
    }
    
    private void AddRequest(DateTime now)
    {
        _requestTimes.Enqueue(now);
    }
    
    private void RemoveExpiredRequests(DateTime now)
    {
        while (_requestTimes.Count > 0 && _requestTimes.Peek() < now.AddSeconds(-1))
        {
            _requestTimes.Dequeue();
        }
    }
}