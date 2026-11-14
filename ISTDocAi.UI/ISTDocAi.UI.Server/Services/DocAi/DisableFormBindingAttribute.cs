using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
namespace ISTDocAi.UI.Server.Services.DocAi;
// MVC must not consume multipart before the original single-pass streaming reader.
[AttributeUsage(AttributeTargets.Method)]
public sealed class DisableFormBindingAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        for (var i = context.ValueProviderFactories.Count - 1; i >= 0; i--)
            if (context.ValueProviderFactories[i] is FormValueProviderFactory or FormFileValueProviderFactory or JQueryFormValueProviderFactory)
                context.ValueProviderFactories.RemoveAt(i);
    }
    public void OnResourceExecuted(ResourceExecutedContext context) { }
}
