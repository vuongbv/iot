using Common.Mongodb;
using Common.Mongodb.MultiTenant;
using Common.Mongodb.Repository;
using Common.MongoDbMultitenant;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using WE.IOTService.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOTService.Domain.Store
{
    public interface IDeviceRepository : IRepository<Device>
    {

    }
    public class DeviceRepository : Repository<Device>, IDeviceRepository
    {


        public DeviceRepository(IServiceScopeFactory serviceScopeFactory) : base(serviceScopeFactory)
        {

        }


        static DeviceRepository()
        {
            if (!BsonClassMap.IsClassMapRegistered(typeof(Device)))
            {
                BsonClassMap.RegisterClassMap<Device>(cm =>
                {
                    cm.AutoMap();
                    cm.SetIgnoreExtraElements(true);
                });
            }
        }

        protected override string CollectionName()
        {
            return "Device";
        }

        protected override Task SetupCollectionAsync(IMongoCollection<Device> collection,
            CancellationToken ct)
        {
            //setup index
            return Task.CompletedTask;
        }
    }
}
