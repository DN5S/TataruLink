using System.Threading.Tasks;

namespace TataruLink.Events;

public interface IEventHandler<in TEvent> where TEvent : class
{
    Task HandleAsync(TEvent @event);
}
