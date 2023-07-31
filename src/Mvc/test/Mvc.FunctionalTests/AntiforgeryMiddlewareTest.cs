// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Mvc.FunctionalTests;

public class AntiforgeryMiddlewareTest
{
    [Fact]
    public async Task Works_WithAntiforgeryMetadata_ValidToken()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddMvcCore().UseSpecificControllers(typeof(TestController));
        builder.Services.AddControllersWithViews();
        builder.Services.AddAntiforgery();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.UseAntiforgery();
        app.MapControllers();

        await app.StartAsync();

        var client = app.GetTestClient();
        var antiforgery = app.Services.GetRequiredService<IAntiforgery>();
        var antiforgeryOptions = app.Services.GetRequiredService<IOptions<AntiforgeryOptions>>();
        var tokens = antiforgery.GetAndStoreTokens(new DefaultHttpContext());

        var request = new HttpRequestMessage(HttpMethod.Post, "/Test/PostWithRequireAntiforgeryToken");
        request.Headers.Add("Cookie", antiforgeryOptions.Value.Cookie.Name + "=" + tokens.CookieToken);
        var nameValueCollection = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", tokens.RequestToken),
            new("name", "Test task"),
            new("isComplete", "false"),
            new("dueDate", DateTime.Today.AddDays(1).ToString(CultureInfo.InvariantCulture)),
        };
        request.Content = new FormUrlEncodedContent(nameValueCollection);
        var result = await client.SendAsync(request);
        result.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Works_WithAntiforgeryMetadata_InvalidToken()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddMvcCore().UseSpecificControllers(typeof(TestController));
        builder.Services.AddControllersWithViews();
        builder.Services.AddAntiforgery();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.UseAntiforgery();
        app.MapControllers();

        await app.StartAsync();

        var client = app.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Post, "/Test/PostWithRequireAntiforgeryToken");
        var nameValueCollection = new List<KeyValuePair<string, string>>
        {
            new("name", "Test task"),
            new("isComplete", "false"),
            new("dueDate", DateTime.Today.AddDays(1).ToString(CultureInfo.InvariantCulture)),
        };
        request.Content = new FormUrlEncodedContent(nameValueCollection);
        var result = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task Works_WithAntiforgeryMetadata_ValidToken_RequestSizeLimit()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddMvcCore().UseSpecificControllers(typeof(TestController));
        builder.Services.AddControllersWithViews();
        builder.Services.AddAntiforgery();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.Use((context, next) =>
        {
            context.Features.Set<IHttpMaxRequestBodySizeFeature>(new FakeHttpMaxRequestBodySizeFeature(5_000_000));
            return next(context);
        });
        app.UseRouting();
        app.Use((context, next) =>
        {
            context.Request.Body = new SizeLimitedStream(context.Request.Body, context.Features.Get<IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize);
            return next(context);
        });
        app.UseAntiforgery();
        app.MapControllers();

        await app.StartAsync();

        var client = app.GetTestClient();
        var antiforgery = app.Services.GetRequiredService<IAntiforgery>();
        var antiforgeryOptions = app.Services.GetRequiredService<IOptions<AntiforgeryOptions>>();
        var tokens = antiforgery.GetAndStoreTokens(new DefaultHttpContext());

        var request = new HttpRequestMessage(HttpMethod.Post, "/Test/PostWithRequireAntiforgeryTokenAndSizeLimit");
        request.Headers.Add("Cookie", antiforgeryOptions.Value.Cookie.Name + "=" + tokens.CookieToken);
        var nameValueCollection = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", tokens.RequestToken),
            new("name", "Test task"),
            new("isComplete", "false"),
            new("dueDate", DateTime.Today.AddDays(1).ToString(CultureInfo.InvariantCulture)),
        };
        request.Content = new FormUrlEncodedContent(nameValueCollection);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await client.SendAsync(request));
        Assert.Equal("The maximum number of bytes have been read.", exception.Message);
    }

    [Fact]
    public async Task Works_WithAntiforgeryMetadata_ValidToken_RequestFormLimits()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddMvcCore().UseSpecificControllers(typeof(TestController));
        builder.Services.AddControllersWithViews();
        builder.Services.AddAntiforgery();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.UseAntiforgery();
        app.MapControllers();

        await app.StartAsync();

        var client = app.GetTestClient();
        var antiforgery = app.Services.GetRequiredService<IAntiforgery>();
        var antiforgeryOptions = app.Services.GetRequiredService<IOptions<AntiforgeryOptions>>();
        var tokens = antiforgery.GetAndStoreTokens(new DefaultHttpContext());

        var request = new HttpRequestMessage(HttpMethod.Post, "/Test/PostWithRequireAntiforgeryTokenAndFormLimit");
        request.Headers.Add("Cookie", antiforgeryOptions.Value.Cookie.Name + "=" + tokens.CookieToken);
        var nameValueCollection = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", tokens.RequestToken),
            new("name", "Test task"),
            new("isComplete", "false"),
            new("dueDate", DateTime.Today.AddDays(1).ToString(CultureInfo.InvariantCulture)),
        };
        request.Content = new FormUrlEncodedContent(nameValueCollection);
        var result = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task Works_WithAntiforgeryMetadata_ValidToken_DisableRequestSizeLimits()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddMvcCore().UseSpecificControllers(typeof(TestWithRequestSizeLimitController));
        builder.Services.AddControllersWithViews();
        builder.Services.AddAntiforgery();
        builder.WebHost.UseTestServer();
        await using var app = builder.Build();
        app.UseAntiforgery();
        app.MapControllers();

        await app.StartAsync();

        var client = app.GetTestClient();
        var antiforgery = app.Services.GetRequiredService<IAntiforgery>();
        var antiforgeryOptions = app.Services.GetRequiredService<IOptions<AntiforgeryOptions>>();
        var tokens = antiforgery.GetAndStoreTokens(new DefaultHttpContext());

        var request = new HttpRequestMessage(HttpMethod.Post, "/TestWithRequestSizeLimit/PostWithRequireAntiforgeryTokenAndDisableSizeLimit");
        request.Headers.Add("Cookie", antiforgeryOptions.Value.Cookie.Name + "=" + tokens.CookieToken);
        var nameValueCollection = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", tokens.RequestToken),
            new("name", "Test task"),
            new("isComplete", "false"),
            new("dueDate", DateTime.Today.AddDays(1).ToString(CultureInfo.InvariantCulture)),
        };
        request.Content = new FormUrlEncodedContent(nameValueCollection);
        var result = await client.SendAsync(request);
        result.EnsureSuccessStatusCode();
    }

    [Route("[controller]/[action]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        [HttpPost]
        [RequireAntiforgeryToken]
        public ActionResult PostWithRequireAntiforgeryToken([FromForm] Todo todo)
            => new OkObjectResult(todo);

        [HttpPost]
        [RequireAntiforgeryToken]
        [RequestSizeLimit(4)]
        public ActionResult PostWithRequireAntiforgeryTokenAndSizeLimit([FromForm] Todo todo)
            => new OkObjectResult(todo);

        [HttpPost]
        [RequireAntiforgeryToken]
        [RequestFormLimits(ValueCountLimit = 2)]
        public ActionResult PostWithRequireAntiforgeryTokenAndFormLimit([FromForm] Todo todo)
            => new OkObjectResult(todo);
    }

    [Route("[controller]/[action]")]
    [ApiController]
    [RequestSizeLimit(4)]
    public class TestWithRequestSizeLimitController : ControllerBase
    {
        [HttpPost]
        [RequireAntiforgeryToken]
        public ActionResult PostWithRequireAntiforgeryTokenAndDisableSizeLimit([FromForm] Todo todo)
            => new OkObjectResult(todo);
    }

    public class Todo
    {
        public string Name { get; set; } = string.Empty;
        public bool IsCompleted { get; set; } = false;
        public DateTime DueDate { get; set; } = DateTime.Now.Add(TimeSpan.FromDays(1));
    }

    public class FakeHttpMaxRequestBodySizeFeature : IHttpMaxRequestBodySizeFeature
    {
        public FakeHttpMaxRequestBodySizeFeature(
            long? maxRequestBodySize = null,
            bool isReadOnly = false)
        {
            MaxRequestBodySize = maxRequestBodySize;
            IsReadOnly = isReadOnly;
        }
        public bool IsReadOnly { get; }
        public long? MaxRequestBodySize { get; set; }
    }
}

public static class MvcExtensions
    {
        /// <summary>
        /// Finds the appropriate controllers
        /// </summary>
        /// <param name="partManager">The manager for the parts</param>
        /// <param name="controllerTypes">The controller types that are allowed. </param>
        public static void UseSpecificControllers(
            this ApplicationPartManager partManager,
            params Type[] controllerTypes)
        {
            partManager.FeatureProviders.Add(new TestControllerFeatureProvider());
            partManager.ApplicationParts.Clear();
            partManager.ApplicationParts.Add(new SelectedControllersApplicationParts(controllerTypes));
        }

        /// <summary>
        /// Only allow selected controllers
        /// </summary>
        /// <param name="mvcCoreBuilder">The builder that configures mvc core</param>
        /// <param name="controllerTypes">The controller types that are allowed. </param>
        public static IMvcCoreBuilder UseSpecificControllers(
            this IMvcCoreBuilder mvcCoreBuilder,
            params Type[] controllerTypes) => mvcCoreBuilder
                .ConfigureApplicationPartManager(partManager => partManager.UseSpecificControllers(controllerTypes));

        /// <summary>
        /// Only instantiates selected controllers, not all of them. Prevents application scanning for controllers.
        /// </summary>
        private class SelectedControllersApplicationParts : ApplicationPart, IApplicationPartTypeProvider
        {
            public SelectedControllersApplicationParts()
            {
                Name = "Only allow selected controllers";
            }
            public SelectedControllersApplicationParts(Type[] types)
            {
                Types = types.Select(x => x.GetTypeInfo()).ToArray();
            }

            public override string Name { get; }

            public IEnumerable<TypeInfo> Types { get; }
        }

        private class TestControllerFeatureProvider : ControllerFeatureProvider
        {
            protected override bool IsController(TypeInfo typeInfo) => true;
        }
    }
