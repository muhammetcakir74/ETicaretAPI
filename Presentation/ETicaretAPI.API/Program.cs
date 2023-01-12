using System.Security.Claims;
using System.Text;
using ETicaretAPI.API.Configurations.ColumnWriters;
using ETicaretAPI.API.Extensions;
using ETicaretAPI.Application;
using ETicaretAPI.Application.Validators.Products;
using ETicaretAPI.Infrastructure;
using ETicaretAPI.Infrastructure.Enums;
using ETicaretAPI.Infrastructure.Filters;
using ETicaretAPI.Infrastructure.Services.Storage.Azure;
using ETicaretAPI.Infrastructure.Services.Storage.Local;
using ETicaretAPI.Persistance;
using ETicaretAPI.SignalR;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Sinks.PostgreSQL;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPersistenceServices();
builder.Services.AddInfrastructureServices();
builder.Services.AddApplicationServices();
builder.Services.AddSingalRServices();

//builder.Services.AddStorage<LocalStorage>();
builder.Services.AddStorage<AzureStorage>();

//CORS POLICY
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => 
    policy.WithOrigins("http://localhost:4200", "https://localhost:4200").AllowAnyHeader().AllowAnyMethod().AllowCredentials()
));

//LOGLAMA
Logger log = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/log.txt")
    .WriteTo.PostgreSQL(builder.Configuration.GetConnectionString("PostgreSQL"),"logs",
    needAutoCreateTable:true,
    columnOptions: new Dictionary<string, ColumnWriterBase>
    {
        {"message", new RenderedMessageColumnWriter() },
        {"message_template", new MessageTemplateColumnWriter() },
        {"level", new LevelColumnWriter() },
        {"time_stamp", new TimestampColumnWriter() },
        {"exception", new ExceptionColumnWriter() },
        {"log_event", new LogEventSerializedColumnWriter() },
        {"user_name", new UserNameColumnWriter() }
    })
    .WriteTo.Seq(builder.Configuration["Seq:ServerURL"])//Seq te g�rsel log sistemi
    .Enrich.FromLogContext() //D�� columwriter kullan�m� i�in (bizdeki UserNameColumnWriter)
    .MinimumLevel.Information()//Hangi durumlarda log tutulaca�� error yaparsak sadece errorda log kayd� yapar.
    .CreateLogger();

builder.Host.UseSerilog(log);

builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields = HttpLoggingFields.All;
    logging.RequestHeaders.Add("sec-ch-ua");
    logging.MediaTypeOptions.AddText("application/javascript");
    logging.RequestBodyLogLimit = 4096;
    logging.ResponseBodyLogLimit = 4096;
});



builder.Services.AddControllers(options => options.Filters.Add<ValidationFilter>())
    .AddFluentValidation(configuration => configuration.RegisterValidatorsFromAssemblyContaining<CreateProductValidator>
    ())
    .ConfigureApiBehaviorOptions(options => options.SuppressModelStateInvalidFilter = true);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer("Admin",options =>
{
    options.TokenValidationParameters = new()
    {
        ValidateAudience = true, //Olu�turulacak token de�erini hangi sitelerin kullanaca�� belirlenir. -> www.bilmemne.com
        ValidateIssuer = true, // Olu�turulacak token de�erenin kimin da��tt���n� ifade eder. -> www.myapi.com
        ValidateLifetime = true, //Olu�turulan token de�erinin s�resini kontrol eden de�er.
        ValidateIssuerSigningKey = true,//�retilecek token de�erinin uygulamam�za ait bir de�er olup olmad���n� sa�layan de�er.

        ValidAudience = builder.Configuration["Token:Audience"],
        ValidIssuer = builder.Configuration["Token:Issuer"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Token:SecurityKey"])),
        //Token'�n s�resinin do�ru hesaplanmas� i�in.
        LifetimeValidator = (notBefore, expires, securityToken, validationParameters) => expires != null ? expires > DateTime.UtcNow : false,
    
        NameClaimType = ClaimTypes.Name //JWT �zerinde Name claime kar��l�k gelen de�eri User.Identity.Name propertisinden elde edebiliriz.
    };
});

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.ConfigureExceptionHandler<Program>(app.Services.GetRequiredService<ILogger<Program>>());

app.UseStaticFiles();

app.UseSerilogRequestLogging();

app.UseHttpLogging();

//CORS
app.UseCors();

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.Use(async (context,next) =>
{
    var username = context.User?.Identity?.IsAuthenticated != null || true ? context.User.Identity.Name : null;
    LogContext.PushProperty("user_name",username);

    await next();
});

app.MapControllers();

app.MapHubs();

app.Run();
