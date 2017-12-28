using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveDomain.Bus;
using ReactiveDomain.Messaging;
using ReactiveUI;

namespace ReactiveDomain.ViewObjects
{
    // TODO: We may want to move this into a separate UI project so the main ReactiveDomain DLL doesn't depend on ReactiveUI.
    public static class CommandBuilder
    {
        /// <summary>
        /// Creates a ReactiveCommand from an Action, with a defined CanExecute.
        /// </summary>
        /// <param name="canExecute"></param>
        /// <param name="action"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <returns></returns>
        public static ReactiveCommand<Unit, Unit> FromAction(
                                                IObservable<bool> canExecute,
                                                Action action,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null)
        {
            return FromAction(_ => action(), canExecute, scheduler, userErrorMsg);
        }

        /// <summary>
        /// Creates a ReactiveCommand from an Action. The ReactiveCommand's CanExecute will always be true.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <returns></returns>
        public static ReactiveCommand<Unit, Unit> FromAction(
                                                Action action,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null)
        {
            return FromAction(_ => action(), null, scheduler, userErrorMsg);
        }

        private static ReactiveCommand<Unit, Unit> FromAction(
            Action<Unit> action,
            IObservable<bool> canExecute = null,
            IScheduler scheduler = null,
            string userErrorMsg = null)
        {
            if (scheduler == null)
                scheduler = RxApp.MainThreadScheduler;
            Func<Unit, Task> task = async _ => await Task.Run(() => action(_));
            var cmd =
                canExecute == null ?
                    ReactiveCommand.CreateFromTask(task, outputScheduler: scheduler) :
                    ReactiveCommand.CreateFromTask(task, canExecute, scheduler);

            cmd.ThrownExceptions
                        .SelectMany(ex => Interactions.Errors.Handle(new UserError(userErrorMsg ?? ex.Message, ex)))
                        .ObserveOn(MainThreadScheduler)
                        .Subscribe(result =>
                        {
                            //This will return the recovery option returned from the registered user error handler
                            //right now this is a simple message box in the view code behind
                            /* n.b. this forces evaluation/execution of the select many  */
                        });
            return cmd;
        }

        /// <summary>
        /// Creates a ReactiveCommand from an Action&lt;object&gt;, with a defined CanExecute.
        /// </summary>
        /// <param name="canExecute"></param>
        /// <param name="action"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <returns></returns>
        public static ReactiveCommand<object, Unit> FromActionEx(
                                               IObservable<bool> canExecute,
                                               Action<object> action,
                                               IScheduler scheduler = null,
                                               string userErrorMsg = null)
        {
            return FromActionEx(action, canExecute, scheduler, userErrorMsg);
        }

        /// <summary>
        /// Creates a ReactiveCommand from an Action&lt;object&gt;. The ReactiveCommand's CanExecute will always be true.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <returns></returns>
        public static ReactiveCommand<object, Unit> FromActionEx(
                                                Action<object> action,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null)
        {
            return FromActionEx(action, null, scheduler, userErrorMsg);
        }

        private static ReactiveCommand<object, Unit> FromActionEx(
                                                Action<object> action,
                                                IObservable<bool> canExecute = null,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null)
        {
            if (scheduler == null)
                scheduler = RxApp.MainThreadScheduler;
            Func<object, Task> task = async x => await Task.Run(() => action(x));
            var cmd =
                canExecute == null ?
                        ReactiveCommand.CreateFromTask(task, outputScheduler: scheduler) :
                        ReactiveCommand.CreateFromTask(task, canExecute, scheduler);

            cmd.ThrownExceptions
                        .SelectMany(ex => Interactions.Errors.Handle(new UserError(userErrorMsg ?? ex.Message, ex)))
                        .ObserveOn(MainThreadScheduler)
                        .Subscribe(result =>
                        {
                            //This will return the recovery option returned from the registered user error handler
                            //right now this is a simple message box in the view code behind
                            /* n.b. this forces evaluation/execution of the select many  */
                        });
            return cmd;
        }

        /// <summary>
        /// Creates a ReactiveCommand that will fire the specified Command on the bus when executed.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="canExecute"></param>
        /// <param name="commandFunc"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <param name="responseTimeout"></param>
        /// <param name="ackTimeout"></param>
        /// <returns></returns>
        public static ReactiveCommand<Unit, Unit> BuildFireCommand(
                                                this ICommandPublisher bus,
                                                IObservable<bool> canExecute,
                                                Func<Command> commandFunc,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null,
                                                TimeSpan? responseTimeout = null,
                                                TimeSpan? ackTimeout = null)
        {
            return FireCommands(bus, new[] { commandFunc }, canExecute, scheduler, userErrorMsg, responseTimeout, ackTimeout);
        }

        /// <summary>
        /// Creates a ReactiveCommand that will fire the specified Command on the bus when executed, with a defined CanExecute.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="canExecute"></param>
        /// <param name="commandFunc"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <param name="responseTimeout"></param>
        /// <param name="ackTimeout"></param>
        /// <returns></returns>
        public static ReactiveCommand<Unit, Unit> BuildFireCommand(
                                                this IGeneralBus bus,
                                                IObservable<bool> canExecute,
                                                Func<Command> commandFunc,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null,
                                                TimeSpan? responseTimeout = null,
                                                TimeSpan? ackTimeout = null)
        {
            return FireCommands(bus, new[] { commandFunc }, canExecute, scheduler, userErrorMsg, responseTimeout, ackTimeout);
        }

        /// <summary>
        /// Creates a ReactiveCommand that will fire the specified Command on the bus when executed. The ReactiveCommand's CanExecute will always be true.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="commandFunc"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <param name="responseTimeout"></param>
        /// <param name="ackTimeout"></param>
        /// <returns></returns>
        public static ReactiveCommand<Unit, Unit> BuildFireCommand(
                                                this ICommandPublisher bus,
                                                Func<Command> commandFunc,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null,
                                                TimeSpan? responseTimeout = null,
                                                TimeSpan? ackTimeout = null)
        {
            return FireCommands(bus, new[] { commandFunc }, null, scheduler, userErrorMsg, responseTimeout, ackTimeout);
        }

        /// <summary>
        /// Creates a ReactiveCommand that will fire the specified Command on the bus when executed. The ReactiveCommand's CanExecute will always be true.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="commandFunc"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <param name="responseTimeout"></param>
        /// <param name="ackTimeout"></param>
        /// <returns></returns>
        public static ReactiveCommand<Unit, Unit> BuildFireCommand(
                                                this IGeneralBus bus,
                                                Func<Command> commandFunc,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null,
                                                TimeSpan? responseTimeout = null,
                                                TimeSpan? ackTimeout = null)
        {
            return FireCommands(bus, new[] { commandFunc }, null, scheduler, userErrorMsg, responseTimeout, ackTimeout);
        }

        /// <summary>
        /// Creates a ReactiveCommand that will fire the specified Command on the bus when executed, with a defined CanExecute.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="canExecute"></param>
        /// <param name="commands"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <param name="responseTimeout"></param>
        /// <param name="ackTimeout"></param>
        /// <returns></returns>
        public static ReactiveCommand<Unit, Unit> BuildFireCommand(
                                                this ICommandPublisher bus,
                                                IObservable<bool> canExecute,
                                                IEnumerable<Func<Command>> commands,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null,
                                                TimeSpan? responseTimeout = null,
                                                TimeSpan? ackTimeout = null)
        {
            return FireCommands(bus, commands, canExecute, scheduler, userErrorMsg, responseTimeout, ackTimeout);
        }

        /// <summary>
        /// Creates a ReactiveCommand that will fire the specified Command on the bus when executed, with a defined CanExecute.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="canExecute"></param>
        /// <param name="commands"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <param name="responseTimeout"></param>
        /// <param name="ackTimeout"></param>
        /// <returns></returns>
        public static ReactiveCommand<Unit, Unit> BuildFireCommands(
                                                this IGeneralBus bus,
                                                IObservable<bool> canExecute,
                                                IEnumerable<Func<Command>> commands,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null,
                                                TimeSpan? responseTimeout = null,
                                                TimeSpan? ackTimeout = null)
        {
            return FireCommands(bus, commands, canExecute, scheduler, userErrorMsg, responseTimeout, ackTimeout);
        }

        /// <summary>
        /// Creates a ReactiveCommand that will fire the specified Command on the bus when executed. The ReactiveCommand's CanExecute will always be true.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="commands"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <param name="responseTimeout"></param>
        /// <param name="ackTimeout"></param>
        /// <returns></returns>
        public static ReactiveCommand<Unit, Unit> BuildFireCommands(
                                                this ICommandPublisher bus,
                                                IEnumerable<Func<Command>> commands,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null,
                                                TimeSpan? responseTimeout = null,
                                                TimeSpan? ackTimeout = null)
        {
            return FireCommands(bus, commands, null, scheduler, userErrorMsg, responseTimeout, ackTimeout);
        }

        /// <summary>
        /// Creates a ReactiveCommand that will fire the specified Command on the bus when executed.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="commands"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <param name="responseTimeout"></param>
        /// <param name="ackTimeout"></param>
        /// <returns></returns>
        public static ReactiveCommand<Unit, Unit> BuildFireCommands(
                                                this IGeneralBus bus,
                                                IEnumerable<Func<Command>> commands,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null,
                                                TimeSpan? responseTimeout = null,
                                                TimeSpan? ackTimeout = null)
        {
            return FireCommands(bus, commands, null, scheduler, userErrorMsg, responseTimeout, ackTimeout);
        }

        private static ReactiveCommand<Unit, Unit> FireCommands(
                    ICommandPublisher bus,
                    IEnumerable<Func<Command>> commands,
                    IObservable<bool> canExecute = null,
                    IScheduler scheduler = null,
                    string userErrorMsg = null,
                    TimeSpan? responseTimeout = null,
                    TimeSpan? ackTimeout = null)
        {
            if (scheduler == null)
                scheduler = RxApp.MainThreadScheduler;
            Func<Unit, Task> task = async _ => await Task.Run(() =>
            {
                foreach (var func in commands)
                {
                    bus.Fire(func(), userErrorMsg, responseTimeout, ackTimeout);
                }
            });
            var cmd =
                canExecute == null ?
                        ReactiveCommand.CreateFromTask(task, outputScheduler: scheduler) :
                        ReactiveCommand.CreateFromTask(task, canExecute, scheduler);

            cmd.ThrownExceptions
                        .SelectMany(ex => Interactions.Errors.Handle(new UserError(userErrorMsg ?? ex.Message, ex)))
                        .ObserveOn(MainThreadScheduler)
                        .Subscribe(result =>
                        {
                            //This will return the recovery option returned from the registered user error handler
                            //right now this is a simple message box in the view code behind
                            /* n.b. this forces evaluation/execution of the select many  */
                        });
            return cmd;
        }

        /// <summary>
        /// BuildFireCommandEx() does the same thing as BuildFireCommand(), except commandFunc must be defined
        /// as a function that takes an object as input and returns a Command as result.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="canExecute"></param>
        /// <param name="commandFunc"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <param name="responseTimeout"></param>
        /// <param name="ackTimeout"></param>
        /// <returns></returns>
        public static ReactiveCommand<object, Unit> BuildFireCommandEx(
                                this IGeneralBus bus,
                                IObservable<bool> canExecute,
                                Func<object, Command> commandFunc,
                                IScheduler scheduler = null,
                                string userErrorMsg = null,
                                TimeSpan? responseTimeout = null,
                                TimeSpan? ackTimeout = null)
        {
            return FireCommandEx(bus, commandFunc, canExecute, scheduler, userErrorMsg, responseTimeout, ackTimeout);
        }

        /// <summary>
        /// BuildFireCommandEx() does the same thing as BuildFireCommand(), except commandFunc must be defined
        /// as a function that takes an object as input and returns a Command as result.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="commandFunc"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <param name="responseTimeout"></param>
        /// <param name="ackTimeout"></param>
        /// <returns></returns>
        public static ReactiveCommand<object, Unit> BuildFireCommandEx(
                                                       this IGeneralBus bus,
                                                       Func<object, Command> commandFunc,
                                                       IScheduler scheduler = null,
                                                       string userErrorMsg = null,
                                                       TimeSpan? responseTimeout = null,
                                                       TimeSpan? ackTimeout = null)
        {
            return FireCommandEx(bus, commandFunc, null, scheduler, userErrorMsg, responseTimeout, ackTimeout);
        }

        private static ReactiveCommand<object, Unit> FireCommandEx(
            IGeneralBus bus,
            Func<object, Command> commandFunc,
            IObservable<bool> canExecute = null,
            IScheduler scheduler = null,
            string userErrorMsg = null,
            TimeSpan? responseTimeout = null,
            TimeSpan? ackTimeout = null)
        {

            if (scheduler == null)
                scheduler = RxApp.MainThreadScheduler;
            Func<object, Task> task = async x => await Task.Run(() =>
            {
                var c = commandFunc(x);
                if (c != null) bus.Fire(c, userErrorMsg, responseTimeout, ackTimeout);
            });
            var cmd =
                canExecute == null ?
                        ReactiveCommand.CreateFromTask(task, outputScheduler: scheduler) :
                        ReactiveCommand.CreateFromTask(task, canExecute, scheduler);

            cmd.ThrownExceptions
                        .SelectMany(ex => Interactions.Errors.Handle(new UserError(userErrorMsg ?? ex.Message, ex)))
                        .ObserveOn(MainThreadScheduler)
                        .Subscribe(result =>
                        {
                            //This will return the recovery option returned from the registered user error handler
                            //right now this is a simple message box in the view code behind
                            /* n.b. this forces evaluation/execution of the select many  */
                        });
            return cmd;
        }

        /// <summary>
        /// Creates a ReactiveCommand that will publish the specified Event on the bus when executed.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="canExecute"></param>
        /// <param name="eventFunc"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <returns></returns>
        public static ReactiveCommand<Unit, Unit> BuildPublishEvent(
                                                    this IPublisher bus,
                                                    IObservable<bool> canExecute,
                                                    Func<Event> eventFunc,
                                                    IScheduler scheduler = null,
                                                    string userErrorMsg = null)
        {
            return PublishEvents(bus, new[] { eventFunc }, canExecute, scheduler, userErrorMsg);
        }

        /// <summary>
        /// Creates a ReactiveCommand that will publish the specified Event on the bus when executed.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="eventFunc"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <returns></returns>
        public static ReactiveCommand<Unit, Unit> BuildPublishEvent(
                                                   this IPublisher bus,
                                                   Func<Event> eventFunc,
                                                   IScheduler scheduler = null,
                                                   string userErrorMsg = null)
        {
            return PublishEvents(bus, new[] { eventFunc }, null, scheduler, userErrorMsg);
        }

        /// <summary>
        /// Creates a ReactiveCommand that will publish the specified Event on the bus when executed.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="canExecute"></param>
        /// <param name="events"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <returns></returns>
        public static ReactiveCommand<Unit, Unit> BuildPublishEvent(
                                                this IPublisher bus,
                                                IObservable<bool> canExecute,
                                                IEnumerable<Func<Event>> events,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null)
        {
            return PublishEvents(bus, events, canExecute, scheduler, userErrorMsg);
        }

        /// <summary>
        /// Creates a ReactiveCommand that will publish the specified Event on the bus when executed.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="events"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <returns></returns>
        public static ReactiveCommand<Unit, Unit> BuildPublishEvent(
                                                this IPublisher bus,
                                                IEnumerable<Func<Event>> events,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null)
        {
            return PublishEvents(bus, events, null, scheduler, userErrorMsg);
        }

        private static ReactiveCommand<Unit, Unit> PublishEvents(
                                                    IPublisher bus,
                                                    IEnumerable<Func<Event>> events,
                                                    IObservable<bool> canExecute = null,
                                                    IScheduler scheduler = null,
                                                    string userErrorMsg = null)
        {
            if (scheduler == null)
                scheduler = RxApp.MainThreadScheduler;
            Func<Unit, Task> task = async _ => await Task.Run(() =>
            {
                foreach (var func in events)
                {
                    bus.Publish(func());
                }
            });

            var cmd =
                canExecute == null ?
                        ReactiveCommand.CreateFromTask(task, outputScheduler: scheduler) :
                        ReactiveCommand.CreateFromTask(task, canExecute, scheduler);

            cmd.ThrownExceptions
                        .SelectMany(ex => Interactions.Errors.Handle(new UserError(userErrorMsg ?? ex.Message, ex)))
                        .ObserveOn(MainThreadScheduler)
                        .Subscribe(result =>
                        {
                            //This will return the recovery option returned from the registered user error handler
                            //right now this is a simple message box in the view code behind
                            /* n.b. this forces evaluation/execution of the select many  */
                        });
            return cmd;
        }

        /// <summary>
        /// BuildPublishEventEx() does the same thing as BuildPublishEvent(), except eventFunc must be defined
        /// as a function that takes an object as input and returns a Command as result.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="canExecute"></param>
        /// <param name="eventFunc"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <returns></returns>
        public static ReactiveCommand<object, Unit> BuildPublishEventEx(
                                                    this IPublisher bus,
                                                    IObservable<bool> canExecute,
                                                    Func<object, Event> eventFunc,
                                                    IScheduler scheduler = null,
                                                    string userErrorMsg = null)
        {
            return PublishEventsEx(bus, new[] { eventFunc }, canExecute, scheduler, userErrorMsg);
        }

        /// <summary>
        /// BuildPublishEventEx() does the same thing as BuildPublishEvent(), except eventFunc must be defined
        /// as a function that takes an object as input and returns a Command as result.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="eventFunc"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <returns></returns>
        public static ReactiveCommand<object, Unit> BuildPublishEventEx(
                                                   this IPublisher bus,
                                                   Func<object, Event> eventFunc,
                                                   IScheduler scheduler = null,
                                                   string userErrorMsg = null)
        {
            return PublishEventsEx(bus, new[] { eventFunc }, null, scheduler, userErrorMsg);
        }

        /// <summary>
        /// BuildPublishEventEx() does the same thing as BuildPublishEvent(), except eventFuncs must be defined
        /// as a function that takes an object as input and returns a Command as result.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="canExecute"></param>
        /// <param name="eventFuncs"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <returns></returns>
        public static ReactiveCommand<object, Unit> BuildPublishEventsEx(
                                                this IPublisher bus,
                                                IObservable<bool> canExecute,
                                                IEnumerable<Func<object, Event>> eventFuncs,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null)
        {
            return PublishEventsEx(bus, eventFuncs, canExecute, scheduler, userErrorMsg);
        }

        /// <summary>
        /// BuildPublishEventEx() does the same thing as BuildPublishEvent(), except eventFuncs must be defined
        /// as a function that takes an object as input and returns a Command as result.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="eventFuncs"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <returns></returns>
        public static ReactiveCommand<object, Unit> BuildPublishEventsEx(
                                                this IPublisher bus,
                                                IEnumerable<Func<object, Event>> eventFuncs,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null)
        {
            return PublishEventsEx(bus, eventFuncs, null, scheduler, userErrorMsg);
        }

        private static ReactiveCommand<object, Unit> PublishEventsEx(
                                                    IPublisher bus,
                                                    IEnumerable<Func<object, Event>> eventFuncs,
                                                    IObservable<bool> canExecute = null,
                                                    IScheduler scheduler = null,
                                                    string userErrorMsg = null)
        {
            if (scheduler == null)
                scheduler = RxApp.MainThreadScheduler;
            Func<object, Task> task = async x => await Task.Run(() =>
            {
                foreach (var func in eventFuncs)
                {
                    bus.Publish(func(x));
                }
            });

            var cmd =
                canExecute == null ?
                        ReactiveCommand.CreateFromTask(task, outputScheduler: scheduler) :
                        ReactiveCommand.CreateFromTask(task, canExecute, scheduler);

            cmd.ThrownExceptions
                        .SelectMany(ex => Interactions.Errors.Handle(new UserError(userErrorMsg ?? ex.Message, ex)))
                        .ObserveOn(MainThreadScheduler)
                        .Subscribe(result =>
                        {
                            //This will return the recovery option returned from the registered user error handler
                            //right now this is a simple message box in the view code behind
                            /* n.b. this forces evaluation/execution of the select many  */
                        });
            return cmd;
        }

        private static IScheduler MainThreadScheduler
        {
            get
            {
                try
                {
                    return DispatcherScheduler.Current;
                }
                catch
                {
                    return ThreadPoolScheduler.Instance;
                }
            }
        }
    }
}
