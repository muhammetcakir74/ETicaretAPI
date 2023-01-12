using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ETicaretAPI.Application.Abstractions.Hubs;
using ETicaretAPI.SignalR.HubServices;
using Microsoft.Extensions.DependencyInjection;

namespace ETicaretAPI.SignalR
{
    public static class ServiceRegistration
    {
        public static void AddSingalRServices(this IServiceCollection collection)
        {
            collection.AddTransient<IProductHubService, ProductHubService>();
            collection.AddSignalR();
        }
    }
}
