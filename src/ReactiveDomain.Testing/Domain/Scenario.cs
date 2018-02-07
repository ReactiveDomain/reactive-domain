using System;
using System.Collections.Generic;
using System.Linq;

namespace ReactiveDomain.Testing
{
    public class Scenario : IScenarioInitialStateBuilder
    {
        private static readonly IScenarioInitialStateBuilder Builder =
            new ScenarioBuilder(
                new RecordedEvent[0],
                null,
                new RecordedEvent[0],
                null);

        public IScenarioGivenNoneStateBuilder GivenNone()
        {
            return Builder.GivenNone();
        }

        public IScenarioGivenStateBuilder Given(IEnumerable<RecordedEvent> events)
        {
            return Builder.Given(events);
        }

        public IScenarioWhenStateBuilder When(object command)
        {
            return Builder.When(command);
        }

        private class ScenarioBuilder :
            IScenarioInitialStateBuilder,
            IScenarioGivenNoneStateBuilder,
            IScenarioGivenStateBuilder,
            IScenarioWhenStateBuilder,
            IScenarioThenNoneStateBuilder,
            IScenarioThenStateBuilder,
            IScenarioThrowsStateBuilder
        {
            private readonly RecordedEvent[] _givens;
            private readonly object _when;
            private readonly RecordedEvent[] _thens;
            private readonly Exception _throws;

            public ScenarioBuilder(RecordedEvent[] givens, object when, RecordedEvent[] thens, Exception throws)
            {
                _givens = givens;
                _when = when;
                _thens = thens;
                _throws = throws;
            }

            IScenarioGivenNoneStateBuilder IScenarioInitialStateBuilder.GivenNone()
            {
                return this;
            }

            IScenarioGivenStateBuilder IScenarioGivenStateBuilder.Given(IEnumerable<RecordedEvent> events)
            {
                if (events == null) throw new ArgumentNullException(nameof(events));
                return new ScenarioBuilder(_givens.Concat(events).ToArray(), _when, _thens, _throws);
            }

            IScenarioGivenStateBuilder IScenarioInitialStateBuilder.Given(IEnumerable<RecordedEvent> events)
            {
                if (events == null) throw new ArgumentNullException(nameof(events));
                return new ScenarioBuilder(events.ToArray(), _when, _thens, _throws);
            }

            IScenarioWhenStateBuilder IScenarioGivenStateBuilder.When(object command)
            {
                return When(command);
            }

            IScenarioWhenStateBuilder IScenarioGivenNoneStateBuilder.When(object command)
            {
                return When(command);
            }

            IScenarioWhenStateBuilder IScenarioInitialStateBuilder.When(object command)
            {
                return When(command);
            }

            private IScenarioWhenStateBuilder When(object command)
            {
                if (command == null) throw new ArgumentNullException(nameof(command));
                return new ScenarioBuilder(_givens, command, _thens, _throws);
            }

            IScenarioThenNoneStateBuilder IScenarioWhenStateBuilder.ThenNone()
            {
                return this;
            }

            IScenarioThenStateBuilder IScenarioWhenStateBuilder.Then(IEnumerable<RecordedEvent> events)
            {
                return new ScenarioBuilder(_givens, _when, events.ToArray(), _throws);
            }

            IScenarioThrowsStateBuilder IScenarioWhenStateBuilder.Throws(Exception exception)
            {
                return new ScenarioBuilder(_givens, _when, _thens, exception);
            }

            ExpectEventsScenario IExpectEventsScenarioBuilder.Build()
            {
                return new ExpectEventsScenario(_givens, _when, _thens);
            }

            IScenarioThenStateBuilder IScenarioThenStateBuilder.Then(IEnumerable<RecordedEvent> events)
            {
                return new ScenarioBuilder(_givens, _when, _thens.Concat(events).ToArray(), _throws);
            }

            ExpectExceptionScenario IExpectExceptionScenarioBuilder.Build()
            {
                return new ExpectExceptionScenario(_givens, _when, _throws);
            }
        }
    }
}
