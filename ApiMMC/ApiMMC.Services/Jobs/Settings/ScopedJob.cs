using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace ApiMMC.Services.Jobs.Settings
{
    public class ScopedJob(IServiceScope scope, IJob innerJob) : IJob, IDisposable
    {
        private readonly IServiceScope _scope = scope;
        private readonly IJob _innerJob = innerJob;

        public async Task Execute(IJobExecutionContext context)
        {
            await _innerJob.Execute(context);
        }

        public void Dispose()
        {
            _scope.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
