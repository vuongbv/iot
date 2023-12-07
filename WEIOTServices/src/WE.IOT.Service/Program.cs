using Common.Authentication;
using Common.Authorization;
using Common.Caching;
using Common.Email;
using Common.Exceptions;
using Common.Extensions;
using Common.Measurement;
using Common.Mongodb.MultiTenant;
using Common.MongoDb;
using Common.MongoDb.Repository;
using Common.MongoDbMultitenant;
using Common.Mqtt;
using Common.RestClientHelpers;
using Common.RestSharpClient;
using Common.SMS;
using Common.SMS.Sender;
using Common.Threshold;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using FluentValidation;
using Quartz;
using WE.IOT.Service.Application.BackgroundJobs;
using WE.IOTService.Application;
using WE.IOTService.Domain.BussinesHandler;
using WE.IOT.Service.SignalRHub;


var builder = WebApplication.CreateBuilder(args);

ConfigurationManager configuration = builder.Configuration;
IWebHostEnvironment environment = builder.Environment;
var rootApi = "";

builder.Services.AddProfileBase(configuration, rootApi, environment);

builder.Services.AddMultiTenentConfig(configuration);
builder.Services.AddMongoDb(configuration);
builder.Services.AddDefaultTenantMongoDbStore();
builder.Services.AddCustomMultitenantService<ClaimnBaseMultitenantService>();

builder.Services.AddAutoMapper(WEIOTServiceApplication.Assembly);
builder.Services.AddValidatorsFromAssembly(WEIOTServiceApplication.Assembly);
builder.Services.AddIOTApplicationService();

builder.Services.AddValidatorsFromAssembly(WEIOTServiceApplication.Assembly);

builder.Services.AddRedisConfig(configuration);

builder.Services.AddSmsConfig(configuration);
builder.Services.AddEmailConfig(configuration);
builder.Services.AddSendGridConfig(configuration);
builder.Services.AddRestSharpConfig(configuration);
builder.Services.AddDynamicAuthenticationConfig(configuration);
builder.Services.AddEnrichUserInfoPolicy();

builder.Services.AddDevicePowerOptions(configuration);

builder.Services.AddMqttClientCredentialOptions(configuration);
builder.Services.AddMqttHostedService();
builder.Services.AddAlarmRuleBackgroupdJob();
builder.Services.AddOperationBackgroupdJob();

//builder.Services.AddTransient<Itestme, Testme>();

builder.Services.AddTransient<IthresholdSetting, ThresholdSetting>();
builder.Services.AddTransient<IRepositoryFactory, RepositoryFactory>();
//builder.Services.AddTransient<ITenantSettingRepositoryCommon, TenantSettingRepositoryCommon>();
//builder.Services.AddTransient<ISmsSender, SmsSender>();
builder.Services.AddTransient<IDeviceRepositoryCommon, DeviceRepositoryCommon>();



// add quartz
builder.Services.AddQuartz(a =>
{
    a.UseMicrosoftDependencyInjectionJobFactory();
});

builder.Services.AddQuartzHostedService(a
    => {
        a.WaitForJobsToComplete = true;
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder => builder
        .WithOrigins("http://localhost:4200")
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()
        .WithExposedHeaders("Content-Disposition"));
});

builder.Services.AddSignalR();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("CorsPolicy");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

//app.UsePerformanceLoggingMiddleware();
app.UseGlobalExceptionMiddleware();

app.MapHub<IotHub>("iot");

app.Run();