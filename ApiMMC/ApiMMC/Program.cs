using ApiMMC.Services.Helpers.Initiation;

var builder = WebApplication.CreateBuilder(args);

var hostBuilder = MicroserviceJobBootstrapper.CreateBuilder(args);

await hostBuilder.RunAsync();
