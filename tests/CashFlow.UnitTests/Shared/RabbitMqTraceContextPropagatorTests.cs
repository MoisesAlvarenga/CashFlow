namespace CashFlow.UnitTests.Shared;

using System.Diagnostics;
using System.Text;
using CashFlow.Shared.Contracts.Diagnostics;
using Xunit;

public class RabbitMqTraceContextPropagatorTests
{
    private static readonly ActivitySource TestSource = new("CashFlow.Tests", "1.0.0");

    [Fact]
    public void InjectTraceContext_WhenNoActivityProvidedAndCurrentIsNull_HeadersRemainEmpty()
    {
        // Arrange
        var headers = new Dictionary<string, object?>();

        // Act
        RabbitMqTraceContextPropagator.InjectTraceContext(headers, activity: null);

        // Assert
        Assert.DoesNotContain("traceparent", headers.Keys);
    }

    [Fact]
    public void InjectTraceContext_WhenActivityProvided_InjectsTraceparentAsByteArray()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = TestSource.StartActivity("TestProducerActivity");
        Assert.NotNull(activity);

        var headers = new Dictionary<string, object?>();

        // Act
        RabbitMqTraceContextPropagator.InjectTraceContext(headers, activity);

        // Assert
        Assert.True(headers.ContainsKey("traceparent"));
        var rawTraceparent = headers["traceparent"];
        Assert.IsType<byte[]>(rawTraceparent);

        var traceparentString = Encoding.UTF8.GetString((byte[])rawTraceparent!);
        Assert.StartsWith("00-", traceparentString);
        Assert.Contains(activity.TraceId.ToString(), traceparentString);
    }

    [Fact]
    public void ExtractTraceContext_WhenHeadersNullOrEmpty_ReturnsDefaultContext()
    {
        // Act
        var nullResult = RabbitMqTraceContextPropagator.ExtractTraceContext(null);
        var emptyResult = RabbitMqTraceContextPropagator.ExtractTraceContext(new Dictionary<string, object?>());

        // Assert
        Assert.Equal(default, nullResult.ActivityContext);
        Assert.Equal(default, emptyResult.ActivityContext);
    }

    [Fact]
    public void ExtractTraceContext_WhenHeadersContainByteArrayTraceparent_ExtractsCorrectTraceContext()
    {
        // Arrange
        var expectedTraceId = ActivityTraceId.CreateRandom();
        var expectedSpanId = ActivitySpanId.CreateRandom();
        var traceparentValue = $"00-{expectedTraceId}-{expectedSpanId}-01";

        var headers = new Dictionary<string, object?>
        {
            ["traceparent"] = Encoding.UTF8.GetBytes(traceparentValue)
        };

        // Act
        var propagationContext = RabbitMqTraceContextPropagator.ExtractTraceContext(headers);

        // Assert
        Assert.Equal(expectedTraceId, propagationContext.ActivityContext.TraceId);
        Assert.Equal(expectedSpanId, propagationContext.ActivityContext.SpanId);
        Assert.True(propagationContext.ActivityContext.IsRemote);
    }

    [Fact]
    public void ExtractTraceContext_WhenHeadersContainStringTraceparent_ExtractsCorrectTraceContext()
    {
        // Arrange
        var expectedTraceId = ActivityTraceId.CreateRandom();
        var expectedSpanId = ActivitySpanId.CreateRandom();
        var traceparentValue = $"00-{expectedTraceId}-{expectedSpanId}-01";

        var headers = new Dictionary<string, object?>
        {
            ["traceparent"] = traceparentValue
        };

        // Act
        var propagationContext = RabbitMqTraceContextPropagator.ExtractTraceContext(headers);

        // Assert
        Assert.Equal(expectedTraceId, propagationContext.ActivityContext.TraceId);
        Assert.Equal(expectedSpanId, propagationContext.ActivityContext.SpanId);
    }

    [Fact]
    public void Roundtrip_InjectAndExtract_PreservesTraceIdAndSpanIdAcrossProducerAndConsumer()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);

        using var producerActivity = TestSource.StartActivity("ProducerSpan");
        Assert.NotNull(producerActivity);

        var headers = new Dictionary<string, object?>();

        // Act - Producer injects context
        RabbitMqTraceContextPropagator.InjectTraceContext(headers, producerActivity);

        // Act - Consumer extracts context
        var extractedContext = RabbitMqTraceContextPropagator.ExtractTraceContext(headers);

        // Assert
        Assert.Equal(producerActivity.TraceId, extractedContext.ActivityContext.TraceId);
        Assert.Equal(producerActivity.SpanId, extractedContext.ActivityContext.SpanId);

        // Act - Consumer creates child activity with extracted parent
        using var consumerActivity = TestSource.StartActivity(
            "ConsumerSpan",
            ActivityKind.Consumer,
            extractedContext.ActivityContext);

        Assert.NotNull(consumerActivity);
        Assert.Equal(producerActivity.TraceId, consumerActivity.TraceId);
        Assert.Equal(producerActivity.SpanId, consumerActivity.ParentSpanId);
    }
}
