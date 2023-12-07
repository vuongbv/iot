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

namespace WE.IOTService.Domain.Store;

public interface ITenantSettingRepository : IRepository<TenantSetting>
{

}
public class TenantSettingRepository : Repository<TenantSetting>, ITenantSettingRepository
{


    public TenantSettingRepository(IMultiTenantService<Common.Mongodb.MultiTenant.Tenant> tenantService, IMongoClient client) : base(tenantService, client)
    {

    }

    static TenantSettingRepository()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(TenantSetting)))
        {
            BsonClassMap.RegisterClassMap<TenantSetting>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
            });
        }
    }


    protected override string CollectionName()
    {
        return "TenantSetting";
    }

    protected override Task SetupCollectionAsync(IMongoCollection<TenantSetting> collection,
        CancellationToken ct)
    {
        //setup index
        return Task.CompletedTask;
    }
}
