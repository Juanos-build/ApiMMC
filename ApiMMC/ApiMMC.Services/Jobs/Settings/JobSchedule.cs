namespace ApiMMC.Services.Jobs.Settings
{
    public class JobSchedule(string jobTypeName, string cronExpression)
    {
        public string JobTypeName { get; } = jobTypeName;
        public string CronExpression { get; } = cronExpression;
    }
}
