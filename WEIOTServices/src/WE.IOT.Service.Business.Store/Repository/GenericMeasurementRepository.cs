using Common.Mongodb;
using Common.Mongodb.MultiTenant;
using Common.Mongodb.Repository;
using Common.MongoDbMultitenant;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Newtonsoft.Json;
using WE.IOTService.Domain.Entities;

namespace WE.IOT.Service.Domain.Repository;

public interface IGenericMeasurementRepository : IRepository<GenericMeasurement>
{

}

public class GenericMeasurementRepository : Repository<GenericMeasurement>, IGenericMeasurementRepository
{

    public GenericMeasurementRepository(IMultiTenantService<Tenant> tenantService, IMongoClient client) : base(tenantService, client)
    {

    }

    static GenericMeasurementRepository()
    {

        BsonClassMap.RegisterClassMap<GenericMeasurement>(cm =>
        {
            cm.AutoMap();

        });
    }

    protected override string CollectionName()
    {
        return "Measurement";
    }

    protected override Task SetupCollectionAsync(IMongoCollection<GenericMeasurement> collection,
        CancellationToken ct)
    {
        //setup index
        return Task.CompletedTask;
    }
}

