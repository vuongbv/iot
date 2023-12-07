using Common.Models.Strapi;

namespace WE.IOT.Service.Application.Dto.Measurement;

public class CradleRouterDto
{
    public string name { get; set; }
    public string state { get; set; }
    public DateTime? state_updated_at { get; set; }
    public DateTime? updated_at { get; set; }

}
public class CradleDataResult
{
    public List<CradleRouterSignalDto> data { get; set; }
    public MetaTag meta { get; set; }

}
public class CradleRouterSignalDto
{
    public double? rsrp5g { get; set; }
    public double? rsrp { get; set; }
    public double? rsrq5g { get; set; }
    public double? rsrq { get; set; }
    public double? sinr5g { get; set; }
    public double? sinr { get; set; }
    public double? signal_percent { get; set; }
    public double? uptime { get; set; }
    public string? created_at_timeuuid { get; set; }
    public DateTime? updated_at { get; set; }
    public DateTime? created_at { get; set; }

}
public class MetaTag
{
    public int limit { get; set; }
    public string next { get; set; }
}
public class CradleRouterTestDto
{
    public string id { get; set; }
    public string state { get; set; }

}
public class TestModel
{
    public int Id { get; set; }
    public CradleRouterTestResultDto Result { get; set; }
}
public class CradleRouterTestResultDto
{
    public string account { get; set; }
    public string id { get; set; }
    public List<CradleRouterTestResultDataDto> results { get; set; }
}
public class CradleRouterTestResultErrorDto
{
    public string account { get; set; }
    public string id { get; set; }
    public List<CradleRouterTestResultDataErrorDto> results { get; set; }
}
public class CradleRouterTestResultDataDto
{
    public string created_at { get; set; }
    public string updated_at { get; set; }
    public int net_device_id { get; set; }
    public int router_id { get; set; }
    public string state { get; set; }
    public TestResultDataDto results { get; set; }

}
public class CradleRouterTestResultDataErrorDto
{
    public string created_at { get; set; }
    public string updated_at { get; set; }
    public int net_device_id { get; set; }
    public string router_id { get; set; }
    public string state { get; set; }
    public string results { get; set; }

}
public class TestResultDataDto
{
    public string client_received { get; set; }
    public string client_sent { get; set; }
    public string duration { get; set; }
    public string server_received { get; set; }
    public string server_sent { get; set; }
    public string throughput { get; set; }
    public string timestamp { get; set; }
    public string jitter { get; set; }
    public string latency { get; set; }

}

public class CradleUptimeStateDataResult
{
    public List<CradleRouterUptimeStateDto> data { get; set; }
    public MetaTag meta { get; set; }

}
public class CradleRouterUptimeStateDto
{
    public double period { get; set; }
    public string state { get; set; }
    public string? created_at_timeuuid { get; set; }
    public DateTime? updated_at { get; set; }
    public DateTime? created_at { get; set; }

}