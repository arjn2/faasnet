using BMS.Core.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BMS_API.Filters;

/// <summary>
/// Action filter that automatically adds copyright information to API responses
/// Reads the [Copyright] attribute from controllers and includes it in the response headers and body
/// </summary>
public class CopyrightResponseFilter : ActionFilterAttribute
{
    public override void OnActionExecuted(ActionExecutedContext context)
    {
        // Get the controller type
        var controllerType = context.Controller.GetType();
        
        // Try to get the Copyright attribute from the controller
        var copyrightAttribute = controllerType.GetCustomAttributes(typeof(CopyrightAttribute), false)
                                              .FirstOrDefault() as CopyrightAttribute;
        
        if (copyrightAttribute != null)
        {
            // Add copyright information to response headers
            var headers = context.HttpContext.Response.Headers;
            headers["X-Copyright"] = copyrightAttribute.ToString();
            headers["X-Copyright-Author"] = copyrightAttribute.Author;
            headers["X-Copyright-Year"] = copyrightAttribute.Year.ToString();
            headers["X-Copyright-License"] = copyrightAttribute.License;
            
            // For JSON/XML responses, add copyright to the response body
            if (context.Result is ObjectResult objectResult && objectResult.Value != null)
            {
                // Create a wrapper object that includes both the original data and copyright
                var wrappedResponse = new
                {
                    data = objectResult.Value,
                    copyright = new
                    {
                        notice = copyrightAttribute.ToString(),
                        author = copyrightAttribute.Author,
                        year = copyrightAttribute.Year,
                        license = copyrightAttribute.License
                    }
                };
                
                objectResult.Value = wrappedResponse;
            }
        }
        
        base.OnActionExecuted(context);
    }
}