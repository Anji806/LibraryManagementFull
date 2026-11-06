using System.Collections.Concurrent;
using System.Threading.Channels;
using LibraryManagement.Models;

namespace LibraryManagement.Realtime;

public sealed record LoginActivityEvent(string Name, LoginActivityRecord Record);

// Only fields needed by the admin activity table are sent over the stream.
// JWTs, passwords, session IDs, and token expiry values are never published.
public sealed record LoginActivityRecord(
    long LoginLogId,
    int? UserId,
    string Username,
    DateTime LoginDateTime,
    DateTime? LogoutDateTime,
    string? IPAddress,
    string? Browser,
    string? OperatingSystem,
    string LoginStatus,
    string? FailureReason,
    string SessionStatus)
{
    public static LoginActivityRecord From(LoginHistory item) => new(
        item.LoginLogId, item.UserId, item.Username, item.LoginDateTime,
        item.LogoutDateTime, item.IPAddress, item.Browser, item.OperatingSystem,
        item.LoginStatus, item.FailureReason, item.SessionStatus);
}

public interface ILoginActivityEventBus
{
    LoginActivitySubscription Subscribe();
    void PublishLogin(LoginHistory item);
    void PublishLogout(LoginHistory item);
}

public sealed class LoginActivityEventBus : ILoginActivityEventBus
{
    private readonly ConcurrentDictionary<Guid, Channel<LoginActivityEvent>> _subscribers = new();

    public LoginActivitySubscription Subscribe()
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<LoginActivityEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        _subscribers[id] = channel;
        return new LoginActivitySubscription(channel.Reader, () =>
        {
            _subscribers.TryRemove(id, out _);
            channel.Writer.TryComplete();
        });
    }

    public void PublishLogin(LoginHistory item) => Publish("login:new", item);
    public void PublishLogout(LoginHistory item) => Publish("logout:update", item);

    private void Publish(string name, LoginHistory item)
    {
        var message = new LoginActivityEvent(name, LoginActivityRecord.From(item));
        foreach (var subscriber in _subscribers.Values)
            subscriber.Writer.TryWrite(message);
    }
}

public sealed class LoginActivitySubscription(ChannelReader<LoginActivityEvent> reader, Action dispose) : IDisposable
{
    public ChannelReader<LoginActivityEvent> Reader { get; } = reader;
    public void Dispose() => dispose();
}
