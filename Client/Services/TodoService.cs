using Models;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace Client.Services;

public class TodoService(HttpClient httpClient, AuthenticationStateProvider authenticationStateProvider)
{
    private List<Todo>? _todos;

    private readonly JsonSerializerOptions _jsonSerializerOptions =
        new JsonSerializerOptions(JsonSerializerDefaults.Web);

    public IReadOnlyList<Todo>? Todos => _todos?.AsReadOnly();

    public async Task LoadTodos()
    {
        if (_todos != null)
        {
            return;
        }

        _todos = await IsAuthenticated()
            ? await httpClient.GetFromJsonAsync<List<Todo>>("api/todos") ?? []
            : [];
    }

    public async Task AddTodo(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return;
        }

        var todo = new Todo(Guid.NewGuid(), label, false);

        if (await IsAuthenticated())
        {
            var result = await httpClient.PostAsJsonAsync("api/todos", label, _jsonSerializerOptions);
            result.EnsureSuccessStatusCode();
            todo = await result.Content.ReadFromJsonAsync<Todo>(_jsonSerializerOptions) ?? todo;
        }

        _todos ??= [];
        _todos.Add(todo);
    }

    public async Task UpdateTodo(Todo todo)
    {
        if (_todos is null)
        {
            return;
        }

        var index = _todos.FindIndex(t => t.Id == todo.Id);
        if (index == -1)
        {
            throw new ArgumentException($"Cannot find Todo with id {todo.Id}");
        }

        if (await IsAuthenticated())
        {
            var result = await httpClient.PutAsJsonAsync("api/todos", todo, _jsonSerializerOptions);
            result.EnsureSuccessStatusCode();
        }

        _todos[index] = todo;
    }

    private async Task<bool> IsAuthenticated()
    {
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        return authState.User.HasClaim(ClaimTypes.Role, "authorised");
    }
}