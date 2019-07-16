using System;

namespace ReactiveDomain.Testing
{
    public class ExpectExceptionScenario
    {
        public RecordedEvent[] Givens { get; }
        public object When { get; }
        public Exception Throws { get; }

        public ExpectExceptionScenario(
            RecordedEvent[] givens,
            object when,
            Exception throws)
        {
            Givens = givens ?? throw new ArgumentNullException(nameof(givens));
            When = when ?? throw new ArgumentNullException(nameof(when));
            Throws = throws ?? throw new ArgumentNullException(nameof(throws));
        }

        public ExpectExceptionScenarioPassed Pass()
        {
            return new ExpectExceptionScenarioPassed(this);
        }

        public ScenarioExpectedExceptionButThrewOtherException ButThrewException(Exception threw)
        {
            return new ScenarioExpectedExceptionButThrewOtherException(this, threw);
        }

        public ScenarioExpectedExceptionButThrewNoException ButThrewNoException()
        {
            return new ScenarioExpectedExceptionButThrewNoException(this);
        }

        public ScenarioExpectedExceptionButRecordedEvents ButRecordedEvents(RecordedEvent[] events)
        {
            return new ScenarioExpectedExceptionButRecordedEvents(this, events);
        }
    }
}