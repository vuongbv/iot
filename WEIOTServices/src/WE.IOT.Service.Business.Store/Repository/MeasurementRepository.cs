using Common.Mongodb;
using Common.Mongodb.MultiTenant;
using Common.Mongodb.Repository;
using Common.MongoDbMultitenant;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Newtonsoft.Json;
using WE.IOTService.Domain.Entities;

namespace WE.IOT.Service.Domain.Repository;

public interface IMeasurementRepository: IRepository<Measurement>
{

}

public class MeasurementRepository: Repository<Measurement>, IMeasurementRepository
{

    public MeasurementRepository(IMultiTenantService<Tenant> tenantService, IMongoClient client) : base(tenantService, client)
    {

    }

    static MeasurementRepository()
    {

        if (!BsonClassMap.IsClassMapRegistered(typeof(Measurement)))
        {
            BsonClassMap.RegisterClassMap<Measurement>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
            });
        }
    }

    protected override string CollectionName()
    {
        return "Measurement";
    }

    protected override Task SetupCollectionAsync(IMongoCollection<Measurement> collection,
        CancellationToken ct)
    {
        //setup index
        return Task.CompletedTask;
    }
}

