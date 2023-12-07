using Common.Authentication;
using Common.Mongodb;
using Common.Mongodb.Repository;
using Common.MongoDbMultitenant;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using WE.IOT.Service.Domain.Entities;

namespace WE.IOT.Service.Domain.Repository;

public interface IOperationRepository : IRepository<Operation>
{

}

public class OperationRepository : Repository<Operation>, IOperationRepository
{
    public OperationRepository(IServiceScopeFactory serviceScopeFactory) : base(serviceScopeFactory)
    {

    }

    static OperationRepository()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(Operation)))
        {
            BsonClassMap.RegisterClassMap<Operation>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
            });
        }
    }

    protected override string CollectionName()
    {
        return "Operation";
    }

    protected override Task SetupCollectionAsync(IMongoCollection<Operation> collection,
        CancellationToken ct)
    {
        //setup index
        return Task.CompletedTask;
    }
}
