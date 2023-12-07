using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WE.IOT.Service.Application.Dtos.Measurement
{
    public class FiveGDto
    {
        public string Deviceid { get; set; }
        public DateTime SignalDatetime { get; set; }
        public double? UploadSpeed { get; set; }
        public double? DownloadSpeed { get; set; }
        public double? Latency { get; set; }
        public int? SignalPercent { get; set; }
        public int Rsrp5g { get; set; }
    }
    public class DownloadDto
    {
        [BsonId]
        public ObjectId _id { get; set; }
        public DateTime Time { get; set; }
        public double? Speed { get; set; }
    }
    public class UploadDto
    {
        [BsonId]
        public ObjectId _id { get; set; }
        public DateTime Time { get; set; }
        public double? Speed { get; set; }
    }
    public class LatencyDto
    {
        [BsonId]
        public ObjectId _id { get; set; }
        public DateTime Time { get; set; }
        public double? Latency { get; set; }
    }
    public class SignalDto
    {
        [BsonId]
        public ObjectId _id { get; set; }
        public DateTime Time { get; set; }
        public int SignalPercent { get; set; }
        public int Rsrp5g { get; set; }
    }
    public class UptimeDto
    {
        [BsonId]
        public ObjectId _id { get; set; }
        public DateTime Time { get; set; }
        public int Uptime { get; set; }
    }
    public class RouterStateDto
    {
        [BsonId]
        public ObjectId _id { get; set; }
        public DateTime Time { get; set; }
        public string State { get; set; }
    }
}
