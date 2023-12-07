using Common.Mongodb;
using Common.Mongodb.MultiTenant;
using Common.Mongodb.Repository;
using Common.MongoDbMultitenant;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using WE.IOTService.Domain.Entities;
using WE.IOT.Service.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOTService.Domain.Store
{
    public interface IWidgetRepository : IRepository<Widget>
    {

    }
    public class WidgetRepository : Repository<Widget>, IWidgetRepository
    {


        public WidgetRepository(IServiceScopeFactory serviceScopeFactory) : base(serviceScopeFactory)
        {

        }


        static WidgetRepository()
        {
            if (!BsonClassMap.IsClassMapRegistered(typeof(Widget)))
            {
                BsonClassMap.RegisterClassMap<Widget>(cm =>
                {
                    cm.AutoMap();
                    cm.SetIgnoreExtraElements(true);
                });
            }
        }

        protected override string CollectionName()
        {
            return "Widget";
        }

        protected override Task SetupCollectionAsync(IMongoCollection<Widget> collection,
            CancellationToken ct)
        {
            //setup index
            return Task.CompletedTask;
        }
    }
}
