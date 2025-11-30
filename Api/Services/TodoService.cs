using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Models;

namespace Api.Services;

public class TodoService
{
    private readonly Container _container;

    public TodoService(IConfiguration configuration)
    {
        var client = new CosmosClient(
            configuration["TodoCosmosDbConnectionString"],
            new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            });
        var database = client.GetDatabase(configuration["TodoDatabaseName"]);
        _container = database.GetContainer(configuration["TodoContainer"]);
    }

    public async Task<IEnumerable<Todo>> GetTodosForUser(string userId)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.userId = @userId")
            .WithParameter("@userId", userId);

        var results = new List<Todo>();
        using var feed = _container.GetItemQueryIterator<TodoEntity>(query);
        
        while (feed.HasMoreResults)
        {
            var response = await feed.ReadNextAsync();
            results.AddRange(response.Select(e => e.ToTodo()));
        }

        return results;
    }

    public async Task<Todo> AddTodo(string userId, string label)
    {
        var entity = new TodoEntity(
            Guid.NewGuid().ToString(),
            userId,
            label,
            false);

        var response = await _container.CreateItemAsync(entity, new PartitionKey(userId));
        return response.Resource.ToTodo();
    }

    public async Task UpdateTodo(string userId, Todo todo)
    {
        var entity = new TodoEntity(
            todo.Id.ToString(),
            userId,
            todo.Label,
            todo.Complete);

        await _container.ReplaceItemAsync(
            entity,
            entity.Id,
            new PartitionKey(userId));
    }
    
    public async Task DeleteTodo(string userId, string id)
    {
        try
        {
            await _container.DeleteItemAsync<TodoEntity>(id, new PartitionKey(userId));
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Item already doesn't exist—idempotent delete
        }
    }

    private record TodoEntity(
        string Id,
        string UserId,
        string Label,
        bool Complete)
    {
        public Todo ToTodo() => new(Guid.Parse(Id), Label, Complete);
    }
}