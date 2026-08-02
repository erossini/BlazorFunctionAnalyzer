using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PSC.FunctionStudy.UI;
using PSC.FunctionStudy.Web.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// The real history lives here, in the browser. The server registers the no-op counterpart,
// which is what the component sees while it is being prerendered.
builder.Services.AddScoped<IStudyHistory, LocalStorageStudyHistory>();

await builder.Build().RunAsync();
