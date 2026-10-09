using System.Collections.Concurrent;

namespace IntelliQueryAI.AI.Memory;

public sealed record MemoryMessage(string Role, string Content, DateTime TimestampUtc);

public interface IAgentMemoryService
{
    void AppendMessage(Guid sessionId, string role, string content);
    IReadOnlyList<MemoryMessage> GetHistory(Guid sessionId, int maxMessages = 10);
    void ClearSession(Guid sessionId);
}

public sealed class AgentMemoryService : IAgentMemoryService
{
    private readonly ConcurrentDictionary<Guid, List<MemoryMessage>> _sessions = new();

    public void AppendMessage(Guid sessionId, string role, string content)
    {
        var message = new MemoryMessage(role, content, DateTime.UtcNow);
        _sessions.AddOrUpdate(
            sessionId,
            _ => [message],
            (_, list) =>
            {
                lock (list)
                {
                    list.Add(message);
                    // Keep memory buffer bounded to recent 30 messages
                    if (list.Count > 30)
                    {
                        list.RemoveAt(0);
                    }
                }
                return list;
            });
    }

    public IReadOnlyList<MemoryMessage> GetHistory(Guid sessionId, int maxMessages = 10)
    {
        if (_sessions.TryGetValue(sessionId, out var list))
        {
            lock (list)
            {
                return list.TakeLast(maxMessages).ToList();
            }
        }

        return Array.Empty<MemoryMessage>();
    }

    public void ClearSession(Guid sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
    }
}
