using Microsoft.Extensions.Caching.Memory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

namespace SmartHomeRepair.API.Memory
{
    public class UserSession
    {
        public ChatHistoryAgentThread Thread { get; set; } = default!;
    }

    public class SessionStore
    {
        private readonly IMemoryCache _cache;
        private readonly Kernel _kernel;

        public SessionStore(IMemoryCache cache, Kernel kernel)
        {
            _cache = cache;
            _kernel = kernel;
        }

        public UserSession GetOrCreate(string threadId)
        {
            return _cache.GetOrCreate(threadId, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromHours(2);

                var thread = new ChatHistoryAgentThread();

                return new UserSession { Thread = thread };
            })!;
        }

        public void Clear(string userId) => _cache.Remove(userId);
    }
}
