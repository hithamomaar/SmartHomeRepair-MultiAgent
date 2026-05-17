using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SmartHomeRepair.API.Memory;

namespace SmartHomeRepair.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            builder.Services.AddCors(
                options =>
                {
                    options.AddDefaultPolicy(builder =>
                    {
                        builder.AllowAnyOrigin();
                        builder.AllowAnyMethod();
                        builder.AllowAnyHeader();

                    });
                });

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<SessionStore>();
            builder.Services.AddSingleton<RepairSubAgents>();

            builder.Services.AddScoped<HomeRepairPlugin>();
            builder.Services.AddKernel().AddOpenAIChatCompletion(
                "gpt-4o-mini",
                apiKey: builder.Configuration["Apikey"]
                //serviceId:"openai"
            ).AddOpenAITextEmbeddingGeneration(
                "text-embedding-3-small", 
                apiKey: builder.Configuration["Apikey"]
                //serviceId: "openaiembedded"
                );

            builder.Services.AddQdrantVectorStore("localhost", port: 6334, https: false);
            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }


            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
