using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace TodoApp.Shared.Infrastructure.Api;

/// <summary>
/// MVC only discovers <c>public</c> controllers by default. Allowing <c>internal</c> ones means a module's
/// controllers, requests and handlers can all stay internal, so no other module can reference them.
/// </summary>
public sealed class InternalControllerFeatureProvider : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo) =>
        typeInfo is { IsClass: true, IsAbstract: false, ContainsGenericParameters: false }
        && typeof(ControllerBase).IsAssignableFrom(typeInfo)
        && !typeInfo.IsDefined(typeof(NonControllerAttribute));
}
