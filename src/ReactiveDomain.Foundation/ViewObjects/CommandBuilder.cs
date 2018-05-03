using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveUI;
using ReactiveUI.Legacy;
using ReactiveCommand = ReactiveUI.ReactiveCommand;

namespace ReactiveDomain.Foundation.ViewObjects
{
    public static class CommandBuilder
    {
        public static ReactiveCommand FromAction(
                                                IObservable<bool> canExecute,
                                                Action action,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null)
        {
            return FromAction(_ => action(), canExecute, scheduler, userErrorMsg);
        }

        public static ReactiveCommand FromAction(
                                                Action action,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null)
        {
            return FromAction(_ => action(), null, scheduler, userErrorMsg);
        }
        public static ReactiveCommand FromAction(
                                               IObservable<bool> canExecute,
                                               Action<object> action,
                                               IScheduler scheduler = null,
                                               string userErrorMsg = null)
        {
            return FromAction(action, canExecute, scheduler, userErrorMsg);
        }

        public static ReactiveCommand FromAction(
                                                Action<object> action,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null)
        {
            return FromAction(action, null, scheduler, userErrorMsg);
        }
        private static ReactiveCommand FromAction(
                                                Action<object> action,
                                                IObservable<bool> canExecute = null,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null)
        {

            if (scheduler == null)
                scheduler = RxApp.MainThreadScheduler;
            Func<object, Task> task = async _ => await Task.Run(() => action(_));

            var cmd = ReactiveCommand.CreateFromTask(task, canExecute, scheduler);

            cmd.ThrownExceptions
                       .ObserveOn(MainThreadScheduler).SelectMany(ex => UserError.Throw(userErrorMsg ?? ex.Message, ex))
                       .Subscribe(result =>
                       {
                           //This will return the recovery option returned from the registered user error handler
                           //right now this is a simple message box in the view code behind
                           /* n.b. this forces evaluation/execution of the select many  */
                       });
            return cmd;
        }

        public static ReactiveCommand BuildFireCommand(
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

        public static ReactiveCommand BuildFireCommand(
                                                this IDispatcher bus,
                                                IObservable<bool> canExecute,
                                                Func<Command> commandFunc,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null,
                                                TimeSpan? responseTimeout = null,
                                                TimeSpan? ackTimeout = null)
        {
            return FireCommands(bus, new[] { commandFunc }, canExecute, scheduler, userErrorMsg, responseTimeout, ackTimeout);
        }

        public static ReactiveCommand BuildFireCommand(
                                                this ICommandPublisher bus,
                                                Func<Command> commandFunc,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null,
                                                TimeSpan? responseTimeout = null,
                                                TimeSpan? ackTimeout = null)
        {
            return FireCommands(bus, new[] { commandFunc }, null, scheduler, userErrorMsg, responseTimeout, ackTimeout);
        }

        public static ReactiveCommand BuildFireCommand(
                                                this IDispatcher bus,
                                                Func<Command> commandFunc,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null,
                                                TimeSpan? responseTimeout = null,
                                                TimeSpan? ackTimeout = null)
        {
            return FireCommands(bus, new[] { commandFunc }, null, scheduler, userErrorMsg, responseTimeout, ackTimeout);
        }

        public static ReactiveCommand BuildFireCommand(
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


        public static ReactiveCommand BuildFireCommand(
                                                this IDispatcher bus,
                                                IObservable<bool> canExecute,
                                                IEnumerable<Func<Command>> commands,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null,
                                                TimeSpan? responseTimeout = null,
                                                TimeSpan? ackTimeout = null)
        {
            return FireCommands(bus, commands, canExecute, scheduler, userErrorMsg, responseTimeout, ackTimeout);
        }

        public static ReactiveCommand BuildFireCommand(
                                                this ICommandPublisher bus,
                                                IEnumerable<Func<Command>> commands,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null,
                                                TimeSpan? responseTimeout = null,
                                                TimeSpan? ackTimeout = null)
        {
            return FireCommands(bus, commands, null, scheduler, userErrorMsg, responseTimeout, ackTimeout);
        }

        public static ReactiveCommand BuildFireCommand(
                                                this IDispatcher bus,
                                                IEnumerable<Func<Command>> commands,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null,
                                                TimeSpan? responseTimeout = null,
                                                TimeSpan? ackTimeout = null)
        {
            return FireCommands(bus, commands, null, scheduler, userErrorMsg, responseTimeout, ackTimeout);
        }

        private static ReactiveCommand FireCommands(
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
            Func<object, Task> task = async _ => await Task.Run(() =>
            {
                foreach (var func in commands)
                {
                    bus.Send(func(), userErrorMsg, responseTimeout, ackTimeout);
                }
            });
            var cmd = ReactiveCommand.CreateFromTask(task, canExecute, scheduler);

            cmd.ThrownExceptions
                       .SelectMany(ex => UserError.Throw(userErrorMsg, ex))
                       .ObserveOn(MainThreadScheduler).Subscribe(result =>
                       {
                           //This will return the recovery option returned from the registered user error handler
                           //right now this is a simple message box in the view code behind
                           /* n.b. this forces evaluation/execution of the select many  */
                       });
            return cmd;
        }

        /// <summary>
        /// BuildFireCommandEx() does the same thing as BuildFireCommand(), except commandFunc must be defined
        /// as a function that takes Object as input and returns Command as result.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="canExecute"></param>
        /// <param name="commandFunc"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <param name="responseTimeout"></param>
        /// <param name="ackTimeout"></param>
        /// <returns></returns>
        public static ReactiveCommand BuildFireCommandEx(
                                this IDispatcher bus,
                                IObservable<bool> canExecute,
                                Func<Object, Command> commandFunc,
                                IScheduler scheduler = null,
                                string userErrorMsg = null,
                                TimeSpan? responseTimeout = null,
                                TimeSpan? ackTimeout = null)
        {
            return FireCommandEx(bus, commandFunc, canExecute, scheduler, userErrorMsg, responseTimeout, ackTimeout);
        }

        /// <summary>
        /// BuildFireCommandEx() does the same thing as BuildFireCommand(), except commandFunc must be defined
        /// as a function that takes Object as input and returns Command as result.
        /// </summary>
        /// <param name="bus"></param>
        /// <param name="commandFunc"></param>
        /// <param name="scheduler"></param>
        /// <param name="userErrorMsg"></param>
        /// <param name="responseTimeout"></param>
        /// <param name="ackTimeout"></param>
        /// <returns></returns>
        public static ReactiveCommand BuildFireCommandEx(
                                                       this IDispatcher bus,
                                                       Func<Object, Command> commandFunc,
                                                       IScheduler scheduler = null,
                                                       string userErrorMsg = null,
                                                       TimeSpan? responseTimeout = null,
                                                       TimeSpan? ackTimeout = null)
        {
            return FireCommandEx(bus, commandFunc, null, scheduler, userErrorMsg, responseTimeout, ackTimeout);
        }

        private static ReactiveCommand FireCommandEx(
            IDispatcher bus,
            Func<Object, Command> commandFunc,
            IObservable<bool> canExecute = null,
            IScheduler scheduler = null,
            string userErrorMsg = null,
            TimeSpan? responseTimeout = null,
            TimeSpan? ackTimeout = null)
        {

            if (scheduler == null)
                scheduler = RxApp.MainThreadScheduler;
            Func<object, Task> task = async _ => await Task.Run(() =>
            {
                var c = commandFunc(_);
                if (c != null) bus.Send(c, userErrorMsg, responseTimeout, ackTimeout);
            });
            var cmd = ReactiveCommand.CreateFromTask(task, canExecute, scheduler);

            cmd.ThrownExceptions
                       .SelectMany(ex => UserError.Throw(userErrorMsg ?? ex.Message, ex))
                       .ObserveOn(MainThreadScheduler).Subscribe(result =>
                       {
                           //This will return the recovery option returned from the registered user error handler
                           //right now this is a simple message box in the view code behind
                           /* n.b. this forces evaluation/execution of the select many  */
                       });
            return cmd;
        }


        public static ReactiveCommand BuildPublishCommand(
                                                    this IBus bus,
                                                    IObservable<bool> canExecute,
                                                    Func<Event> eventFunc,
                                                    IScheduler scheduler = null,
                                                    string userErrorMsg = null)
        {
            return PublishEvents(bus, new[] { eventFunc }, canExecute, scheduler, userErrorMsg);
        }
        public static ReactiveCommand BuildPublishCommand(
                                                   this IBus bus,
                                                   Func<Event> eventFunc,
                                                   IScheduler scheduler = null,
                                                   string userErrorMsg = null)
        {
            return PublishEvents(bus, new[] { eventFunc }, null, scheduler, userErrorMsg);
        }
        public static ReactiveCommand BuildPublishCommand(
                                                this IBus bus,
                                                IObservable<bool> canExecute,
                                                IEnumerable<Func<Event>> events,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null)
        {
            return PublishEvents(bus, events, canExecute, scheduler, userErrorMsg);
        }
        public static ReactiveCommand BuildPublishCommand(
                                                this IBus bus,
                                                IEnumerable<Func<Event>> events,
                                                IScheduler scheduler = null,
                                                string userErrorMsg = null)
        {
            return PublishEvents(bus, events, null, scheduler, userErrorMsg);
        }
        private static ReactiveCommand PublishEvents(
                                                    IBus bus,
                                                    IEnumerable<Func<Event>> events,
                                                    IObservable<bool> canExecute = null,
                                                    IScheduler scheduler = null,
                                                    string userErrorMsg = null)
        {
            if (scheduler == null)
                scheduler = RxApp.MainThreadScheduler;
            Func<object, Task> task = async _ => await Task.Run(() =>
            {
                foreach (var func in events)
                {
                    bus.Publish(func());
                }
            });

            var cmd = ReactiveCommand.CreateFromTask(task, canExecute, scheduler);

            cmd.ThrownExceptions
                       .SelectMany(ex => UserError.Throw(userErrorMsg ?? ex.Message, ex))
                       .ObserveOn(MainThreadScheduler).Subscribe(result =>
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
