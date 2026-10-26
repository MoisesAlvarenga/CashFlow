namespace CashFlow.Shared.Contracts.Diagnostics;

using System.Diagnostics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;

/// <summary>
/// Propagador de contexto W3C TraceContext (traceparent / tracestate) sobre cabeçalhos AMQP do RabbitMQ.
/// Garante que transações assíncronas mantenham o mesmo TraceId de ponta a ponta
/// (HTTP API -> Banco -> Outbox -> RabbitMQ Broker -> Worker de Consolidação).
/// </summary>
public static class RabbitMqTraceContextPropagator
{
    private static readonly TextMapPropagator Propagator = new TraceContextPropagator();

    /// <summary>
    /// Injeta o TraceContext ativo (W3C traceparent e tracestate) no dicionário de headers AMQP.
    /// </summary>
    public static void InjectTraceContext(IDictionary<string, object?> headers, Activity? activity = null)
    {
        var currentActivity = activity ?? Activity.Current;
        if (currentActivity == null) return;

        var context = currentActivity.Context;
        Propagator.Inject(new PropagationContext(context, Baggage.Current), headers, (carrier, key, value) =>
        {
            carrier[key] = Encoding.UTF8.GetBytes(value);
        });
    }

    /// <summary>
    /// Extrai o TraceContext recebido nos headers AMQP para vincular uma Activity filha no consumidor.
    /// </summary>
    public static PropagationContext ExtractTraceContext(IDictionary<string, object?>? headers)
    {
        if (headers == null || headers.Count == 0)
        {
            return default;
        }

        return Propagator.Extract(default, headers, (carrier, key) =>
        {
            if (carrier.TryGetValue(key, out var val))
            {
                return val switch
                {
                    byte[] bytes => [Encoding.UTF8.GetString(bytes)],
                    string str => [str],
                    _ => val != null ? [val.ToString()!] : Enumerable.Empty<string>()
                };
            }

            return Enumerable.Empty<string>();
        });
    }
}
