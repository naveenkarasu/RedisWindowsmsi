# Redis for Windows - Usage Guide

This guide covers common Redis usage scenarios on Windows.

## Getting Started

### Basic Commands

```powershell
# Start Redis CLI
redis-cli

# Inside redis-cli:
> ping                          # Test connection
> set mykey "Hello"             # Set a value
> get mykey                     # Get a value
> del mykey                     # Delete a key
> keys *                        # List all keys (use carefully in production!)
> flushall                      # Clear all data (destructive!)
> quit                          # Exit redis-cli
```

### Data Types

#### Strings
```powershell
redis-cli set name "John Doe"
redis-cli get name
redis-cli incr counter
redis-cli decr counter
redis-cli append name " Jr."
```

#### Lists
```powershell
redis-cli lpush mylist "first"
redis-cli lpush mylist "second"
redis-cli rpush mylist "last"
redis-cli lrange mylist 0 -1
redis-cli lpop mylist
```

#### Sets
```powershell
redis-cli sadd myset "apple"
redis-cli sadd myset "banana"
redis-cli sadd myset "orange"
redis-cli smembers myset
redis-cli sismember myset "apple"
```

#### Hashes
```powershell
redis-cli hset user:1 name "Alice"
redis-cli hset user:1 age 30
redis-cli hget user:1 name
redis-cli hgetall user:1
```

#### Sorted Sets
```powershell
redis-cli zadd scores 100 "Alice"
redis-cli zadd scores 95 "Bob"
redis-cli zadd scores 98 "Charlie"
redis-cli zrange scores 0 -1 withscores
redis-cli zrevrange scores 0 -1
```

## Common Use Cases

### Caching

```powershell
# Set cache with expiration (5 minutes)
redis-cli setex cache:user:123 300 "{\"name\":\"John\",\"email\":\"john@example.com\"}"

# Get cached value
redis-cli get cache:user:123

# Check TTL
redis-cli ttl cache:user:123
```

### Session Storage

```powershell
# Store session (24 hours)
redis-cli setex session:abc123 86400 "{\"userId\":123,\"loggedIn\":true}"

# Update session expiration
redis-cli expire session:abc123 86400

# Delete session (logout)
redis-cli del session:abc123
```

### Rate Limiting

```powershell
# Simple rate limiter (10 requests per minute)
redis-cli incr ratelimit:user:123
redis-cli expire ratelimit:user:123 60

# Check rate limit
$count = redis-cli get ratelimit:user:123
if ($count -gt 10) {
    Write-Host "Rate limit exceeded"
}
```

### Pub/Sub Messaging

**Subscriber (Terminal 1):**
```powershell
redis-cli subscribe notifications
```

**Publisher (Terminal 2):**
```powershell
redis-cli publish notifications "Hello, World!"
redis-cli publish notifications "New message received"
```

### Task Queue

```powershell
# Enqueue task
redis-cli lpush queue:tasks "{\"type\":\"email\",\"to\":\"user@example.com\"}"

# Dequeue task (blocking)
redis-cli brpop queue:tasks 0

# Check queue length
redis-cli llen queue:tasks
```

## PowerShell Integration

### Using Redis in PowerShell Scripts

```powershell
# Function to call redis-cli
function Invoke-Redis {
    param([string]$Command)
    & redis-cli $Command
}

# Set value
Invoke-Redis "set mykey myvalue"

# Get value
$value = Invoke-Redis "get mykey"
Write-Host "Value: $value"

# Set with expiration
Invoke-Redis "setex tempkey 60 tempvalue"
```

### Advanced PowerShell Example

```powershell
# User cache management
function Set-UserCache {
    param(
        [int]$UserId,
        [hashtable]$UserData,
        [int]$ExpirationSeconds = 3600
    )
    
    $key = "cache:user:$UserId"
    $json = $UserData | ConvertTo-Json -Compress
    redis-cli setex $key $ExpirationSeconds $json
}

function Get-UserCache {
    param([int]$UserId)
    
    $key = "cache:user:$UserId"
    $json = redis-cli get $key
    
    if ($json) {
        return $json | ConvertFrom-Json
    }
    return $null
}

# Usage
Set-UserCache -UserId 123 -UserData @{
    Name = "Alice"
    Email = "alice@example.com"
    Role = "Admin"
}

$user = Get-UserCache -UserId 123
Write-Host "User: $($user.Name)"
```

## Application Integration

### ASP.NET Core

**Install Package:**
```powershell
dotnet add package StackExchange.Redis
```

**Program.cs:**
```csharp
using StackExchange.Redis;

builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect("localhost:6379")
);

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:6379";
    options.InstanceName = "MyApp";
});
```

**Controller:**
```csharp
public class CacheController : Controller
{
    private readonly IDatabase _redis;
    
    public CacheController(IConnectionMultiplexer redis)
    {
        _redis = redis.GetDatabase();
    }
    
    public async Task<IActionResult> Get(string key)
    {
        var value = await _redis.StringGetAsync(key);
        return Ok(value.ToString());
    }
    
    public async Task<IActionResult> Set(string key, string value)
    {
        await _redis.StringSetAsync(key, value, TimeSpan.FromHours(1));
        return Ok();
    }
}
```

### Python (Flask)

```python
from flask import Flask
from redis import Redis

app = Flask(__name__)
redis_client = Redis(host='localhost', port=6379, decode_responses=True)

@app.route('/cache/<key>')
def get_cache(key):
    value = redis_client.get(key)
    return {'key': key, 'value': value}

@app.route('/cache/<key>/<value>')
def set_cache(key, value):
    redis_client.setex(key, 3600, value)
    return {'status': 'success'}

if __name__ == '__main__':
    app.run(debug=True)
```

### Node.js (Express)

```javascript
const express = require('express');
const redis = require('redis');

const app = express();
const client = redis.createClient();

client.connect();

app.get('/cache/:key', async (req, res) => {
    const value = await client.get(req.params.key);
    res.json({ key: req.params.key, value });
});

app.post('/cache/:key/:value', async (req, res) => {
    await client.setEx(req.params.key, 3600, req.params.value);
    res.json({ status: 'success' });
});

app.listen(3000, () => {
    console.log('Server running on port 3000');
});
```

## Monitoring and Maintenance

### Monitor Redis Activity

```powershell
# Real-time command monitoring
redis-cli monitor

# Server statistics
redis-cli info

# Specific section
redis-cli info stats
redis-cli info memory
redis-cli info clients
```

### Performance Monitoring

```powershell
# Check latency
redis-cli --latency

# Intrinsic latency check
redis-cli --intrinsic-latency 100

# Big keys analysis
redis-cli --bigkeys

# Memory usage by key
redis-cli memory usage mykey
```

### Maintenance Commands

```powershell
# Save database to disk
redis-cli save          # Blocking
redis-cli bgsave        # Background

# Check last save time
redis-cli lastsave

# Analyze memory
redis-cli memory stats

# Check database size
redis-cli dbsize

# Flush database
redis-cli flushdb       # Current DB only
redis-cli flushall      # All DBs
```

## Benchmarking

### Built-in Benchmark Tool

```powershell
# Basic benchmark
redis-benchmark

# Custom benchmark
redis-benchmark -t set,get -n 100000 -q

# Pipeline benchmark
redis-benchmark -n 100000 -P 16 -q

# Specific operations
redis-benchmark -t set,lpush -n 100000 -q
```

### PowerShell Benchmark Script

```powershell
$iterations = 10000
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

for ($i = 0; $i -lt $iterations; $i++) {
    redis-cli set "benchmark:$i" "value$i" | Out-Null
}

$stopwatch.Stop()
$opsPerSecond = $iterations / $stopwatch.Elapsed.TotalSeconds

Write-Host "Operations: $iterations"
Write-Host "Time: $($stopwatch.Elapsed.TotalSeconds) seconds"
Write-Host "Operations/sec: $([math]::Round($opsPerSecond, 2))"
```

## Best Practices

### Key Naming Convention

```powershell
# Use colon-separated namespacing
user:1000:profile
user:1000:sessions
product:5000:inventory
cache:homepage:en

# Include type hint
set:active_users
hash:user:1000
list:notifications:1000
zset:leaderboard
```

### Memory Management

```conf
# In redis.conf

# Set max memory
maxmemory 512mb

# Eviction policy
maxmemory-policy allkeys-lru  # Evict any key using LRU
# Options: volatile-lru, allkeys-lru, volatile-random, allkeys-random,
#          volatile-ttl, noeviction
```

### Connection Pooling

**.NET Example:**
```csharp
// Single connection multiplexer (reuse across application)
private static readonly Lazy<ConnectionMultiplexer> lazyConnection =
    new Lazy<ConnectionMultiplexer>(() =>
        ConnectionMultiplexer.Connect("localhost:6379")
    );

public static ConnectionMultiplexer Connection => lazyConnection.Value;
```

### Error Handling

```csharp
try
{
    var db = redis.GetDatabase();
    await db.StringSetAsync("key", "value");
}
catch (RedisConnectionException ex)
{
    // Handle connection failure
    _logger.LogError(ex, "Redis connection failed");
}
catch (RedisTimeoutException ex)
{
    // Handle timeout
    _logger.LogWarning(ex, "Redis operation timeout");
}
```

## Troubleshooting

### Check Connection

```powershell
# Test connection
redis-cli ping

# Check server info
redis-cli info server

# Test latency
redis-cli --latency-history
```

### Debug Commands

```powershell
# Show slow queries (slower than 10ms)
redis-cli config set slowlog-log-slower-than 10000
redis-cli slowlog get 10

# Show connected clients
redis-cli client list

# Check memory usage
redis-cli info memory
redis-cli memory doctor
```

## Resources

- **Redis Commands:** https://redis.io/commands
- **Redis Documentation:** https://redis.io/documentation
- **StackExchange.Redis:** https://stackexchange.github.io/StackExchange.Redis/
- **redis-py:** https://redis-py.readthedocs.io/
- **node-redis:** https://github.com/redis/node-redis

## Next Steps

- Explore [Advanced Configuration](ADVANCED.md)
- Review [Performance Tuning](PERFORMANCE.md)
- Check [TROUBLESHOOTING.md](TROUBLESHOOTING.md)

