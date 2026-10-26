namespace SGrowing.Core.Events;

public interface IEventStreamBroker
{
    Task PublishAsync<T>(string streamName, T message, CancellationToken cancellationToken = default);
    Task<List<T>> ReadStreamAsync<T>(string streamName, int maxBatch = 10, CancellationToken cancellationToken = default);
}

public class InMemoryEventStreamBroker : IEventStreamBroker
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<string>> _streams = new();

    public Task PublishAsync<T>(string streamName, T message, CancellationToken cancellationToken = default)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(message);
        var list = _streams.GetOrAdd(streamName, _ => new List<string>());
        lock (list)
        {
            list.Add(json);
        }
        return Task.CompletedTask;
    }

    public Task<List<T>> ReadStreamAsync<T>(string streamName, int maxBatch = 10, CancellationToken cancellationToken = default)
    {
        var result = new List<T>();
        if (!_streams.TryGetValue(streamName, out var list)) return Task.FromResult(result);

        lock (list)
        {
            var take = list.Take(maxBatch).ToList();
            list.RemoveRange(0, take.Count);
            foreach (var item in take)
            {
                var obj = System.Text.Json.JsonSerializer.Deserialize<T>(item);
                if (obj != null) result.Add(obj);
            }
        }
        return Task.FromResult(result);
    }
}
