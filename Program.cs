namespace DentneDAPI;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers();

        var app = builder.Build();

        app.MapGet("/", () => "Hello World! API is working!");

        app.Run();
    }
}