using System;
using System.Threading;
using System.Threading.Tasks;

namespace SlskDown.Core
{
    /// <summary>
    /// Rate Limiter usando Token Bucket Algorithm (estilo Nicotine+)
    /// </summary>
    public class RateLimiter
    {
        private int tokens;
        private readonly int maxTokens;
        private readonly int refillRate; // tokens por segundo
        private DateTime lastRefill;
        private readonly object lockObj = new object();
        
        public RateLimiter(int maxTokens, int refillRate)
        {
            this.maxTokens = maxTokens;
            this.refillRate = refillRate;
            this.tokens = maxTokens;
            this.lastRefill = DateTime.Now;
        }
        
        public async Task<bool> TryConsumeAsync(int count = 1, CancellationToken cancellationToken = default)
        {
            RefillTokens();
            
            lock (lockObj)
            {
                if (tokens >= count)
                {
                    tokens -= count;
                    return true;
                }
            }
            
            // Calcular tiempo de espera
            int tokensNeeded = count - tokens;
            int waitTimeMs = (tokensNeeded * 1000) / refillRate;
            
            if (waitTimeMs > 0)
            {
                await Task.Delay(waitTimeMs, cancellationToken);
                RefillTokens();
                
                lock (lockObj)
                {
                    if (tokens >= count)
                    {
                        tokens -= count;
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        public bool TryConsume(int count = 1)
        {
            RefillTokens();
            
            lock (lockObj)
            {
                if (tokens >= count)
                {
                    tokens -= count;
                    return true;
                }
            }
            
            return false;
        }
        
        private void RefillTokens()
        {
            lock (lockObj)
            {
                var now = DateTime.Now;
                var elapsed = (now - lastRefill).TotalSeconds;
                var newTokens = (int)(elapsed * refillRate);
                
                if (newTokens > 0)
                {
                    tokens = Math.Min(tokens + newTokens, maxTokens);
                    lastRefill = now;
                }
            }
        }
        
        public int AvailableTokens
        {
            get
            {
                RefillTokens();
                lock (lockObj)
                {
                    return tokens;
                }
            }
        }
    }
}
