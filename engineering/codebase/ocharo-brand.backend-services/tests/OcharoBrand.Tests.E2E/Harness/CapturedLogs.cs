using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace OcharoBrand.Tests.Harness;

/// <summary>Records every host log event, rendered as the console renders it, so a test can count the warnings startup
/// writes; the be sdk's Serilog reads sinks from the host's services.</summary>
internal sealed class CapturedLogs : ILogEventSink
{
    private static readonly MessageTemplateTextFormatter Message = new("{Message:lj}");

    public ConcurrentQueue<(LogEventLevel Level, string Message)> Entries { get; } = new();

    public void Emit(LogEvent logEvent)
    {
        using var text = new StringWriter();
        Message.Format(logEvent, text);
        Entries.Enqueue((logEvent.Level, text.ToString()));
    }
}
