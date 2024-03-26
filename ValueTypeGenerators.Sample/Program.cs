using Microsoft.AspNetCore.Mvc;

namespace ValueTypeGenerators.Sample;

public sealed class Program {
    public static async Task Main(string[] args) {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        if (app.Environment.IsDevelopment()) {
            app.UseSwagger();
            app.UseSwaggerUI();
            app.UseDeveloperExceptionPage();
        }

        app.MapGet("/", static () => "ValueTypeGenerators.Sample")
            .WithName("Root")
            .WithOpenApi();

        app.MapGet("/echo/guid/{value}", static ([FromRoute] ProductId value) => value)
            .WithName("EchoGuid")
            .WithOpenApi();

        app.MapGet("/echo/int/{value}", static ([FromRoute] PersonId value) => value)
            .WithName("EchoInt32")
            .WithOpenApi();

        app.MapGet("/echo/long/{value}", static ([FromRoute] TimestampId value) => value)
            .WithName("EchoInt64")
            .WithOpenApi();

        await app.RunAsync();
    }
}
