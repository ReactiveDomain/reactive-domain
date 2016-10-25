using System;
using System.Collections.Generic;
using ReactiveDomain.Bus;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Diagnostics
{
    public class MonitoredCommandBus : CommandBus
    {
        public MonitoredCommandBus(string name)
            : base(name, false)
        {
        }

        public override void NoMessageHandler(dynamic msg, Type type)
        {
            var metaData = new Dictionary<string, object>
            {
                ["Result"] = "No Handler",
                ["BusName"] = this.Name,
                ["MsgType"] = type.Name,
                ["MsgTypeId"] = msg.MsgTypeId,
                ["HandlerName"] = "**None**",
                ["Timestamp"] = (double) TimeoutService.EpochMsFromDateTime(DateTime.UtcNow)
            };

            //DiagnosticMonitor.WriteEventToEventStore(
            //    DiagnosticMonitor.MonitorStreamName, 
            //    msg, 
            //    metaData);
        }
        public override void MessageReceived(dynamic msg, Type type, string publishedBy)
        {
            var metaData = new Dictionary<string, object>
            {
                ["PublishedBy"] = publishedBy,
                ["BusName"] = this.Name,
                ["MsgType"] = type.Name,
                ["MsgTypeId"] = msg.MsgTypeId,
                ["ThreadId"] = System.Threading.Thread.CurrentThread.ManagedThreadId,
                ["Timestamp"] = (double) TimeoutService.EpochMsFromDateTime(DateTime.UtcNow)
            };
            //DiagnosticMonitor.WriteEventToEventStore(
            //    DiagnosticMonitor.AuditStreamName,
            //    msg, 
            //    metaData);
        }
        

        public override void PostHandleMessage(dynamic msg, Type type, IMessageHandler handler, TimeSpan handleTimeSpan)
        {
            var now = DateTime.UtcNow;
            var metaData = new Dictionary<string, object>
            {
                ["Result"] = "Handled",
                ["BusName"] = this.Name,
                ["MsgType"] = type.Name,
                ["MsgTypeId"] = msg.MsgTypeId,
                ["HandlerName"] = handler.HandlerName,
                ["ProcessTime"] = handleTimeSpan.TotalMilliseconds,
                ["ThreadId"] = System.Threading.Thread.CurrentThread.ManagedThreadId,
                ["Timestamp"] = (double) TimeoutService.EpochMsFromDateTime(now)
            };
            //DiagnosticMonitor.WriteEventToEventStore(
            //    DiagnosticMonitor.MonitorStreamName,
            //    msg, 
            //    metaData);
        }

        public override void NoCommandHandler(dynamic cmd, Type type)
        {
            var metaData = new Dictionary<string, object>();
            metaData["Result"] = "No Handler";
            metaData["BusName"] = this.Name;
            metaData["MsgType"] = type.Name;
            metaData["MsgTypeId"] = cmd.MsgTypeId;
            metaData["HandlerName"] = "**None**";
            metaData["Timestamp"] = (double)TimeoutService.EpochMsFromDateTime(DateTime.UtcNow);
            //DiagnosticMonitor.WriteEventToEventStore(
            //    DiagnosticMonitor.MonitorStreamName,
            //    cmd, 
            //    metaData);
        }

      

        public override void PostHandleCommand(dynamic cmd, Type type, string handlerName, dynamic response, TimeSpan handleTimeSpan)
        {
            var now = DateTime.UtcNow;
            var metaData = new Dictionary<string, object>();
            if (response != null && response.Succeeded)
            {
                metaData["Result"] = "Succeeded";
            }
            else
            {
                if (response == null || response.Error == null)
                {
                    metaData["Result"] = "Failed";
                }
                else
                {
                    metaData["Result"] = response.Error.Message;
                }
            }
            metaData["BusName"] = this.Name;
            metaData["MsgType"] = type.Name;
            metaData["MsgTypeId"] = cmd.MsgTypeId;
            metaData["HandlerName"] = handlerName;
            metaData["ProcessTime"] = handleTimeSpan.TotalMilliseconds;
            metaData["ThreadId"] = System.Threading.Thread.CurrentThread.ManagedThreadId;
            metaData["Timestamp"] = (double)TimeoutService.EpochMsFromDateTime(now);
            //DiagnosticMonitor.WriteEventToEventStore(
            //    DiagnosticMonitor.MonitorStreamName,
            //    cmd, 
            //    metaData);
        }
        public override void CommandReceived(dynamic cmd, Type type, string firedBy)
        {
            var metaData = new Dictionary<string, object>();
            metaData["FiredBy"] = firedBy;
            metaData["BusName"] = this.Name;
            metaData["MsgType"] = type.Name;
            metaData["MsgTypeId"] = cmd.MsgTypeId;
            metaData["ThreadId"] = System.Threading.Thread.CurrentThread.ManagedThreadId;
            metaData["Timestamp"] = (double)TimeoutService.EpochMsFromDateTime(DateTime.UtcNow);
            //DiagnosticMonitor.WriteEventToEventStore(
            //    DiagnosticMonitor.AuditStreamName,
            //    cmd, 
            //    metaData);
        }

    }
}
