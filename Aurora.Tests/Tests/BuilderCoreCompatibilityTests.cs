using Builder.Core;
using Builder.Core.Events;
using Builder.Core.Logging;

namespace Aurora.Tests.Tests;

public sealed class BuilderCoreCompatibilityTests
{
    [Fact]
    public void RestoredAssembly_PreservesLegacyIdentityAndExportedTypes()
    {
        var assembly = typeof(ObservableObject).Assembly;
        string[] expectedTypes =
        [
            "Builder.Core.Events.EventAggregator",
            "Builder.Core.Events.EventBase",
            "Builder.Core.Events.IEventAggregator",
            "Builder.Core.Events.ISubscriber`1",
            "Builder.Core.Logging.DebugLogger",
            "Builder.Core.Logging.ILogger",
            "Builder.Core.Logging.Log",
            "Builder.Core.Logging.Logger",
            "Builder.Core.ObservableObject",
            "Builder.Core.RegexReplaceService",
            "Builder.Core.RelayCommand",
            "Builder.Core.RelayCommand`1"
        ];

        assembly.GetName().Name.Should().Be("Builder.Core");
        assembly.GetName().Version.Should().Be(new Version(1, 0, 83, 7407));
        assembly.GetExportedTypes()
            .Select(type => type.FullName)
            .Order(StringComparer.Ordinal)
            .Should()
            .Equal(expectedTypes.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ObservableObject_RaisesOnlyForChangedValues()
    {
        var target = new TestObservable();
        var propertyNames = new List<string?>();
        target.PropertyChanged += (_, args) => propertyNames.Add(args.PropertyName);

        target.Value = 5;
        target.Value = 5;
        target.Value = 8;

        propertyNames.Should().Equal(nameof(TestObservable.Value), nameof(TestObservable.Value));
    }

    [Fact]
    public void ObservableObject_RaisesExplicitPropertyNamesInOrder()
    {
        var target = new TestObservable();
        var propertyNames = new List<string?>();
        target.PropertyChanged += (_, args) => propertyNames.Add(args.PropertyName);

        target.RaiseProperties("Alpha", "Beta");

        propertyNames.Should().Equal("Alpha", "Beta");
    }

    [Fact]
    public void RelayCommands_PreserveExecutionAndCanExecuteBehavior()
    {
        int executions = 0;
        bool canExecute = false;
        var command = new RelayCommand(() => executions++, () => canExecute);
        int notifications = 0;
        command.CanExecuteChanged += (_, _) => notifications++;

        command.CanExecute(null).Should().BeFalse();
        canExecute = true;
        command.CanExecute(null).Should().BeTrue();
        command.Execute(null);
        command.RaiseCanExecuteChanged();

        executions.Should().Be(1);
        notifications.Should().Be(1);
        Action missingExecute = () => new RelayCommand(null!);
        missingExecute.Should().Throw<ArgumentNullException>().WithParameterName("execute");
    }

    [Fact]
    public void RelayCommands_PreserveGenericExecutionAndCanExecuteBehavior()
    {
        int? executedWith = null;
        bool canExecute = false;
        var command = new RelayCommand<int>(value => executedWith = value, value => canExecute && value > 0);
        int notifications = 0;
        command.CanExecuteChanged += (_, _) => notifications++;

        command.CanExecute(3).Should().BeFalse();
        canExecute = true;
        command.CanExecute(0).Should().BeFalse();
        command.CanExecute(3).Should().BeTrue();
        command.Execute(7);
        command.RaiseCanExecuteChanged();

        executedWith.Should().Be(7);
        notifications.Should().Be(1);
        Action missingExecute = () => new RelayCommand<int>(null!);
        missingExecute.Should().Throw<ArgumentNullException>().WithParameterName("execute");
    }

    [Fact]
    public void RegexReplaceService_RecognizesLegacyInlinePattern()
    {
        var service = new RegexReplaceService();

        service.ContainsInlineReplacement("before $(strength:modifier) after").Should().BeTrue();
        service.ContainsInlineReplacement("plain text").Should().BeFalse();
        RegexReplaceService.InlineReplacePattern.Should().Be("\\$\\((.*?)\\)");
    }

    [Fact]
    public void Logger_RegistersOneImplementationInstanceAndForwardsMessages()
    {
        var first = new CapturingLogger();
        var duplicate = new CapturingLogger();
        Logger.RegisterLogger(first);
        Logger.RegisterLogger(duplicate);
        first.Calls.Clear();

        var exception = new InvalidOperationException("expected");
        Logger.Debug("debug {0}", 1);
        Logger.Info("info {0}", 2);
        Logger.Warning("warning {0}", 3);
        Logger.Exception(exception, "CompatibilityTest");

        first.Calls.Select(call => call.Kind).Should()
            .Equal("Debug", "Info", "Warning", "Warning", "Exception");
        first.Calls[0].Arguments.Should().Equal(1);
        first.Calls[1].Arguments.Should().Equal(2);
        first.Calls[2].Arguments.Should().Equal(3);
        first.Calls[3].Message.Should().Be("Exception in {0}");
        first.Calls[3].Arguments.Should().Equal("CompatibilityTest");
        first.Calls[4].Exception.Should().BeSameAs(exception);
        duplicate.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task EventAggregator_PostsEventsToWeakSubscribers()
    {
        var aggregator = new EventAggregator();
        var subscriber = new TestSubscriber();
        aggregator.Subscribe(subscriber);

        var message = new TestEvent("restored");
        aggregator.Send(message);

        TestEvent received = await subscriber.Received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        received.Should().BeSameAs(message);
        GC.KeepAlive(subscriber);
    }

    [Fact]
    public async Task EventAggregator_RoutesEachImplementedSubscriberInterface()
    {
        var aggregator = new EventAggregator();
        var subscriber = new MultiEventSubscriber();
        aggregator.Subscribe(subscriber);

        var first = new TestEvent("first");
        var second = new OtherEvent("second");
        aggregator.Send(first);
        aggregator.Send(second);

        TestEvent receivedFirst = await subscriber.ReceivedTestEvent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        OtherEvent receivedSecond = await subscriber.ReceivedOtherEvent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        receivedFirst.Should().BeSameAs(first);
        receivedSecond.Should().BeSameAs(second);
        GC.KeepAlive(subscriber);
    }

    private sealed class TestObservable : ObservableObject
    {
        private int _value;

        public int Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public void RaiseProperties(params string[] propertyNames)
        {
            OnPropertyChanged(propertyNames);
        }
    }

    private sealed class TestEvent(string value) : EventBase
    {
        public string Value { get; } = value;
    }

    private sealed class OtherEvent(string value) : EventBase
    {
        public string Value { get; } = value;
    }

    private sealed class TestSubscriber : ISubscriber<TestEvent>
    {
        public TaskCompletionSource<TestEvent> Received { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void OnHandleEvent(TestEvent args)
        {
            Received.TrySetResult(args);
        }
    }

    private sealed class MultiEventSubscriber : ISubscriber<TestEvent>, ISubscriber<OtherEvent>
    {
        public TaskCompletionSource<TestEvent> ReceivedTestEvent { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<OtherEvent> ReceivedOtherEvent { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void OnHandleEvent(TestEvent args)
        {
            ReceivedTestEvent.TrySetResult(args);
        }

        public void OnHandleEvent(OtherEvent args)
        {
            ReceivedOtherEvent.TrySetResult(args);
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<LogCall> Calls { get; } = [];

        public void Debug(string message, params object[] args)
        {
            Calls.Add(new LogCall("Debug", message, args));
        }

        public void Info(string message, params object[] args)
        {
            Calls.Add(new LogCall("Info", message, args));
        }

        public void Warning(string message, params object[] args)
        {
            Calls.Add(new LogCall("Warning", message, args));
        }

        public void Exception(Exception ex)
        {
            Calls.Add(new LogCall("Exception", string.Empty, [], ex));
        }
    }

    private sealed record LogCall(
        string Kind,
        string Message,
        IReadOnlyList<object> Arguments,
        Exception? Exception = null);
}
