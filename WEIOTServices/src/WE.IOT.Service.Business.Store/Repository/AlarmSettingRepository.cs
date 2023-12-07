using Common.Mongodb;
using Common.Mongodb.MultiTenant;
using Common.Mongodb.Repository;
using Common.MongoDbMultitenant;
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
    public interface IAlarmSettingRepository : IRepository<AlarmSetting>
    {

    }
    public class AlarmSettingRepository : Repository<AlarmSetting>, IAlarmSettingRepository
    {

        public AlarmSettingRepository(IMultiTenantService<Tenant> tenantService, IMongoClient client) : base(tenantService, client)
        {
            
        }

        static AlarmSettingRepository()
        {
            BsonClassMap.RegisterClassMap<AlarmSetting>(cm =>
            {
                cm.AutoMap();
            });
        }

        protected override string CollectionName()
        {
            return "AlarmSetting";
        }

        protected override Task SetupCollectionAsync(IMongoCollection<AlarmSetting> collection,
            CancellationToken ct)
        {
            //setup index
            return Task.CompletedTask;
        }
    }
}
