﻿using Rabbit.Rpc.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Rabbit.Rpc.Server.Implementation.ServiceDiscovery.Attributes
{
    /// <summary>
    /// Service标记类型的服务条目提供程序。
    /// </summary>
    public class AttributeServiceEntryProvider : IServiceEntryProvider
    {
        #region Field

        private readonly IEnumerable<Type> _types;
        private readonly IClrServiceEntryFactory _clrServiceEntryFactory;
        private readonly ILogger<AttributeServiceEntryProvider> _logger;

        #endregion Field

        #region Constructor

        public AttributeServiceEntryProvider(IEnumerable<Type> types, IClrServiceEntryFactory clrServiceEntryFactory, ILogger<AttributeServiceEntryProvider> logger)
        {
            _types = types;
            _clrServiceEntryFactory = clrServiceEntryFactory;
            _logger = logger;
        }

        #endregion Constructor

        #region Implementation of IServiceEntryProvider

        /// <summary>
        /// 获取服务条目集合。
        /// </summary>
        /// <returns>服务条目集合。</returns>
        public IEnumerable<ServiceEntry> GetEntries()
        {
            var services = _types.Where(i => i.IsInterface && i.GetCustomAttribute<RpcServiceAttribute>() != null).ToArray();
            var serviceImplementations = _types.Where(i => i.IsClass && !i.IsAbstract && i.Namespace != null && !i.Namespace.StartsWith("System") && !i.Namespace.StartsWith("Microsoft")).ToArray();

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.Information($"发现了以下服务：{string.Join(",", services.Select(i => i.ToString()))}。");
                //                _logger.Information($"发现了以下服务实现：{string.Join(",", serviceImplementations.Select(i => i.ToString()))}。");
            }

            var entries = new List<ServiceEntry>();
            foreach (var service in services)
            {
                foreach (var serviceImplementation in serviceImplementations.Where(i => service.IsAssignableFrom(i)))
                {
                    entries.AddRange(_clrServiceEntryFactory.CreateServiceEntry(service, serviceImplementation));
                }
            }
            return entries;
        }

        #endregion Implementation of IServiceEntryProvider
    }
}