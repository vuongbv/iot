using Common.MongoDb.Entities;
using MongoDB.Bson;

namespace WE.IOT.Service.Domain.Entities;

public class Operation : BaseEntity
{
    public string Source { get; set; } = default!;
    public string CNO { get; set; } = default!;
    public string Point { get; set; } = default!;
    public string PointStatus { get; set; } = default!;
    public string Command { get; set; } = default!;
    public DateTimeOffset ExecutionTime { get; set; } = default!;
    public string Type { get; set; } = default!;
    public string CommandStatus { get; set; }
    public string PinNumber { get; set; } = default!;
    public string CommandExecutionMesssage { get; set; } = default!;
    public BsonValue? OperationData { get; set; } = default!;
}
