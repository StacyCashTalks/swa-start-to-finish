using System.Text.Json;
using Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Models;
using StacyClouds.SwaAuth.Api;

namespace Api;

public class TodoFunctions(TodoService todoService)
{
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    
    [Function(nameof(TodoFunctions) + "_get")]
    public async Task<IActionResult> Get([HttpTrigger(AuthorizationLevel.Function, "get", Route = "todos")] HttpRequest req)
    {
        var authenticated = StaticWebAppApiAuthentication.TryParseHttpHeaderForClientPrincipal(req.Headers, out var clientPrincipal);

        if (!authenticated || clientPrincipal == null || !clientPrincipal.UserRoles!.Contains("authorised"))
        {
            return new UnauthorizedResult();
        }
        
        return new OkObjectResult(await todoService.GetTodosForUser(clientPrincipal.UserId!));
    }
    
    [Function(nameof(TodoFunctions) + "_post")]
    public async Task<IActionResult> Post([HttpTrigger(AuthorizationLevel.Function, "post", Route = "todos")] HttpRequest req)
    {
        var authenticated = StaticWebAppApiAuthentication.TryParseHttpHeaderForClientPrincipal(req.Headers, out var clientPrincipal);

        if (!authenticated || clientPrincipal == null || !clientPrincipal.UserRoles!.Contains("authorised"))
        {
            return new UnauthorizedResult();
        }

        using var reader = new StreamReader(req.Body);
        var label = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(label))
        {
            return new BadRequestResult();
        }

        var todo = await todoService.AddTodo(clientPrincipal.UserId!, label);
        
        return new OkObjectResult(todo);
    }


    [Function(nameof(TodoFunctions) + "_put")]
    public async Task<IActionResult> Put(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "todos")] HttpRequest req)
    {
        var authenticated = StaticWebAppApiAuthentication.TryParseHttpHeaderForClientPrincipal(req.Headers, out var clientPrincipal);

        if (!authenticated || clientPrincipal == null || !clientPrincipal.UserRoles!.Contains("authorised"))
        {
            return new UnauthorizedResult();
        }
        
        var updatedTodo = await JsonSerializer.DeserializeAsync<Todo>(req.Body, _jsonOptions);
        if (updatedTodo is null)
        {
            return new BadRequestResult();
        }

        var userTodos = await todoService.GetTodosForUser(clientPrincipal.UserId!);
        
        if (userTodos.All(td => td.Id != updatedTodo.Id))
        {
            return new NotFoundResult();
        }

        await todoService.UpdateTodo(clientPrincipal.UserId!, updatedTodo);
        return new OkResult();
    }
    
    [Function(nameof(TodoFunctions) + "_delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "todos/{id}")] HttpRequest req, string id)
    {
        var authenticated = StaticWebAppApiAuthentication.TryParseHttpHeaderForClientPrincipal(req.Headers, out var clientPrincipal);

        if (!authenticated || clientPrincipal == null || !clientPrincipal.UserRoles!.Contains("authorised"))
        {
            return new UnauthorizedResult();
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return new BadRequestResult();
        }
        await todoService.DeleteTodo(clientPrincipal.UserId!, id);
        
        return new NoContentResult();
    }
    
}