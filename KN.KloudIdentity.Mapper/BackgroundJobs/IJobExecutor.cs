namespace KN.KloudIdentity.Mapper.BackgroundJobs;

public interface IJobExecutor
{
    Task ExecuteAsync(string jobId);
}
