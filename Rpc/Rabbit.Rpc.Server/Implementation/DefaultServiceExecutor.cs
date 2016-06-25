﻿using DotNetty.Buffers;
using Rabbit.Rpc.Logging;
using Rabbit.Rpc.Messages;
using Rabbit.Rpc.Serialization;
using Rabbit.Rpc.Transport;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Rabbit.Rpc.Server.Implementation
{
    public class DefaultServiceExecutor : IServiceExecutor
    {
        #region Field

        private readonly IServiceEntryLocate _serviceEntryLocate;
        private readonly ISerializer<byte[]> _serializer;
        private readonly ILogger<DefaultServiceExecutor> _logger;

        #endregion Field

        #region Constructor

        public DefaultServiceExecutor(IServiceEntryLocate serviceEntryLocate, ISerializer<byte[]> serializer, ILogger<DefaultServiceExecutor> logger)
        {
            _serviceEntryLocate = serviceEntryLocate;
            _serializer = serializer;
            _logger = logger;
        }

        #endregion Constructor

        #region Implementation of IServiceExecutor

        /// <summary>
        /// 执行。
        /// </summary>
        /// <param name="sender">消息发送者。</param>
        /// <param name="message">调用消息。</param>
        public async Task ExecuteAsync(IMessageSender sender, object message)
        {
            var buffer = (IByteBuffer)message;

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.Information($"接收到消息：{buffer.ToString(Encoding.UTF8)}。");

            var content = buffer.ToArray();
            RemoteInvokeMessage remoteInvokeMessage;
            try
            {
                remoteInvokeMessage = _serializer.Deserialize<byte[], RemoteInvokeMessage>(content);
            }
            catch (Exception exception)
            {
                _logger.Error($"将接收到的消息反序列化成 TransportMessage<RemoteInvokeMessage> 时发送了错误，消息内容：{buffer.ToString(Encoding.UTF8)}。", exception);
                return;
            }

            var entry = _serviceEntryLocate.Locate(remoteInvokeMessage);

            if (entry == null)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                    _logger.Error($"根据服务Id：{remoteInvokeMessage.ServiceId}，找不到服务条目。");
                return;
            }

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.Debug("准备执行本地逻辑。");

            var resultMessage = new RemoteInvokeResultMessage
            {
                Id = remoteInvokeMessage.Id
            };
            try
            {
                var result = entry.Func(remoteInvokeMessage.Parameters);
                var task = result as Task;

                if (task == null)
                {
                    resultMessage.Result = result;
                }
                else
                {
                    task.Wait();

                    var taskType = task.GetType();
                    if (taskType.IsGenericType)
                        resultMessage.Result = taskType.GetProperty("Result").GetValue(task);
                }
            }
            catch (Exception exception)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                    _logger.Error("执行本地逻辑时候发生了错误。", exception);
                resultMessage.ExceptionMessage = GetExceptionMessage(exception);
            }

            try
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.Debug("准备发送响应消息。");
                var resultData = _serializer.Serialize(resultMessage);

                buffer = Unpooled.Buffer(resultData.Length);
                buffer.WriteBytes(resultData);
                await sender.SendAsync(buffer);
                if (_logger.IsEnabled(LogLevel.Debug))
                    _logger.Debug("响应消息发送成功。");
            }
            catch (Exception exception)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                    _logger.Error("发送响应消息时候发生了异常。", exception);
            }
        }

        #endregion Implementation of IServiceExecutor

        #region Private Method

        private static string GetExceptionMessage(Exception exception)
        {
            if (exception == null)
                return string.Empty;

            var message = exception.Message;
            if (exception.InnerException != null)
            {
                message += "|InnerException:" + GetExceptionMessage(exception.InnerException);
            }
            return message;
        }

        #endregion Private Method
    }
}