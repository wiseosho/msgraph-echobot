using EchoBot;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Bot.Builder.Integration.AspNet.Core;  
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Bot.Builder;

    //.UseWindowsService(options =>
    //{
    //    options.ServiceName = "Echo Bot Service";
    //})

try{

    IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        //services.AddControllers();
        //LoggerProviderOptions.RegisterProviderOptions<
        //    EventLogSettings, EventLogLoggerProvider>(services);
        //services.AddSingleton<IBotFrameworkHttpAdapter, BotFrameworkHttpAdapter>();
        services.AddSingleton<LogRequestFilter>();
        services.AddSingleton<BotFrameworkHttpAdapter>();

        services.AddSingleton<IBot, SimpleBot>();
        services.AddSingleton<IBotHost, BotHost>();

        services.AddHostedService<EchoBotWorker>();
    })
    .Build();

await host.RunAsync();

}
catch ( Exception ex ){
    Console.WriteLine($"Critical error: {ex.Message}");
}