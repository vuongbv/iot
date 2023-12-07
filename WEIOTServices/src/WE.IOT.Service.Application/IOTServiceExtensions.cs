using Common.Mqtt;
using Common.Validate;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WE.IOT.Service.Application.MqttHandler;
using WE.IOT.Service.Application.Services;
using WE.IOTService.Domain.Store;
using WE.IOT.Service.Domain.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOTService.Application
{
    public static class IOTServiceExtensions
    {
        public static void AddMqttHostedService(this IServiceCollection services)
        {

            services.AddSingleton<MqttMeasurementService>();
            services.AddHostedService<MqttMeasurementHandler>();
        }

        public static void AddIOTApplicationService(this IServiceCollection services)
        {
            services.AddIOTRepository();
            services.AddIOTService();
        }

        private static void AddIOTRepository(this IServiceCollection services)
        {
            services.AddScoped<IDeviceRepository, DeviceRepository>();
            services.AddScoped<IMeasurementRepository, MeasurementRepository>();
            services.AddScoped<IAlarmSettingRepository, AlarmSettingRepository>();
            services.AddScoped<ITenantSettingRepository, TenantSettingRepository>();
            services.AddScoped<IGenericMeasurementRepository, GenericMeasurementRepository>();
            services.AddScoped<IOperationRepository, OperationRepository>();
            services.AddScoped<IWidgetRepository, WidgetRepository>();

        }

        private static void AddIOTService(this IServiceCollection services)
        {
            services.AddScoped<IDeviceService, DeviceService>();
            services.AddScoped<IMeasurementServices, MeasurementServices>();
            services.AddScoped<IAlarmSettingService, AlarmSettingService>();
            services.AddScoped<ITenantSettingService, TenantSettingService>();
            services.AddScoped<IOperationService, OperationService>();
            services.AddScoped<IDeviceStatusService, DeviceStatusService>();
        }


        
    }
}
