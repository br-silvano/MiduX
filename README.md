# MiduX  

*Lightweight mediator pipeline for modern .NET applications*

[![NuGet](https://img.shields.io/nuget/v/MiduX.svg)](https://www.nuget.org/packages/MiduX/)
[![Build](https://img.shields.io/github/actions/workflow/status/br-silvano/MiduX/build-and-publish.yml)](https://github.com/br-silvano/MiduX/actions)
[![License](https://img.shields.io/github/license/br-silvano/MiduX)](LICENSE)

---

## 📚 Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Configuration](#configuration)
- [Recommended Folder Structure](#recommended-folder-structure)
- [Usage Examples](#usage-examples)
  - [Commands and Queries](#todo-controller)
  - [Notifications](#usage-example-notifications)
- [Exception Handling Middleware](#exception-handling-middleware)
- [Unit Testing and Mocking](#unit-testing-and-mocking)
- [Integration with Validation](#integration-with-validation)
- [Benefits of MiduX](#benefits-of-midux)
- [Contributions](#contributions)
- [License](#license)

---

## 🚀 Features

- ✅ **Native support** for Commands, Queries, and Notifications  
- 🧱 Built on the **Mediator pattern**  
- 🧼 Supports **CQRS** and **Clean Architecture** practices  
- 🔌 **Extensible** – easily integrates with validation, logging, caching, etc.  
- 🧪 Emphasis on **testability** and separation of concerns  

---

## 📦 Installation

Add the `MiduX` package to your project via NuGet:

```bash
dotnet add package MiduX
```

Alternatively, add the following to your `.csproj`:

```xml
<PackageReference Include="MiduX" Version="1.0.0" />
```

---

## ⚙️ Configuration

In your `Program.cs` or `Startup.cs`, register your handlers and add the mediator to the dependency injection pipeline:

```csharp
builder.Services.AddScoped<IRequestHandler<CreateTodoCommand, Guid>, CreateTodoCommandHandler>();
builder.Services.AddScoped<IRequestHandler<GetTodoByIdQuery, TodoDto>, GetTodoByIdQueryHandler>();
builder.Services.AddTransient<INotificationHandler<AlertNotification>, AlertNotificationHandler>();

builder.Services.AddMediator();
```

Additionally, configure the culture if needed:

```csharp
using System.Globalization;

CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");
```

---

## 🧩 Recommended Folder Structure

```text
└── src/
    ├── Application/
    │   ├── Commands/
    │   ├── Handlers/
    │   ├── Queries/
    │   └── Notifications/
    ├── Domain/
    │   └── Entities/
    ├── Infrastructure/
    │   └── Handlers/
    └── WebApi/
        ├── Controllers/
        └── Middleware/
```

---

## ✉️ Usage Examples

### Todo Controller

#### Commands and Queries

```csharp
[ApiController]
[Route("api/[controller]")]
public class TodoController : ControllerBase
{
    private readonly IMediator _mediator;

    public TodoController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var query = new GetTodoByIdQuery(id);
        var result = await _mediator.Send<GetTodoByIdQuery, TodoDto>(query);
        return result != null ? Ok(result) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTodoCommand command)
    {
        var todoId = await _mediator.Send<CreateTodoCommand, Guid>(command);
        return CreatedAtAction(nameof(GetById), new { id = todoId }, todoId);
    }
}
```

#### Creating a Custom Handler

```csharp
public record CreateTodoCommand(string Title) : IRequest<Guid>;

public class CreateTodoCommandHandler : IRequestHandler<CreateTodoCommand, Guid>
{
    public Task<Guid> Handle(CreateTodoCommand request, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        // Your logic to save the TODO
        return Task.FromResult(id);
    }
}
```

---

### 🔁 Usage Example: Notifications

#### Sending a Notification (Application Layer)

```csharp
await _mediator.Publish(new AlertNotification("New todo created"));
```

#### Handling the Notification (Infrastructure Layer)

```csharp
public class AlertNotificationHandler : INotificationHandler<AlertNotification>
{
    public Task Handle(AlertNotification notification, CancellationToken cancellationToken)
    {
        // Notification logic (e.g., logging, sending emails, etc.)
        Console.WriteLine($"Alert: {notification.Message}");
        return Task.CompletedTask;
    }
}
```

---

## ⚠️ Exception Handling Middleware

Use the middleware below to catch exceptions thrown by MiduX, especially `ValidationException`:

```csharp
public class ExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlerMiddleware> logger)
{
    private readonly ILogger<ExceptionHandlerMiddleware> _logger = logger;
    private readonly RequestDelegate _next = next;

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            if (ex is MediatorException mediatorEx && mediatorEx.InnerException is ValidationException validationEx)
            {
                _logger.LogWarning(ex, "Validation error");
                var errors = validationEx.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

                var problemDetails = new ValidationProblemDetails
                {
                    Title = "Validation error",
                    Status = StatusCodes.Status400BadRequest,
                    Instance = context.Request.Path,
                    Detail = "Some fields did not pass validation.",
                    Errors = errors
                };

                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(problemDetails);
            }
            else
            {
                _logger.LogError(ex, "Internal server error");
                await WriteProblemDetailsAsync(context, StatusCodes.Status500InternalServerError, "Internal server error", "An unexpected error occurred.");
            }
        }
    }

    private static async Task WriteProblemDetailsAsync(HttpContext context, int statusCode, string title, string detail)
    {
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            var problemDetails = new ProblemDetails
            {
                Title = title,
                Detail = detail,
                Status = statusCode,
                Instance = context.Request.Path
            };
            await context.Response.WriteAsJsonAsync(problemDetails);
        }
    }
}

public class ValidationProblemDetails : ProblemDetails
{
    public IDictionary<string, string[]>? Errors { get; set; }
}
```

#### Middleware Registration

In `Program.cs`:

```csharp
app.UseMiddleware<ExceptionHandlerMiddleware>();
```

---

## 🧪 Unit Testing and Mocking

Mocking `IMediator.Send<TRequest, TResponse>` simplifies unit testing for controllers and services. For example, using Moq:

```csharp
[Fact]
public async Task GetById_ShouldReturnTodo_WhenFound()
{
    var mediatorMock = new Mock<IMediator>();
    var fakeTodo = new TodoDto { Id = Guid.NewGuid(), Title = "Test Todo" };

    mediatorMock.Setup(m => m.Send<GetTodoByIdQuery, TodoDto>(It.IsAny<GetTodoByIdQuery>(), default))
                .ReturnsAsync(fakeTodo);

    var controller = new TodoController(mediatorMock.Object);
    var result = await controller.GetById(fakeTodo.Id);

    Assert.IsType<OkObjectResult>(result);
}
```

---

## ✅ Integration with Validation

MiduX integrates seamlessly with libraries like **FluentValidation**. For instance, create a validator:

```csharp
public class CreateTodoCommandValidator : AbstractValidator<CreateTodoCommand>
{
    public CreateTodoCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty();
    }
}
```

When using the validation pipeline behavior, any validation errors will be caught and processed by the middleware.

---

## 💡 Benefits of MiduX

- **Clear separation** between read and write operations (CQRS)
- **Low coupling** between application components
- **Built-in validation** via exceptions
- **Asynchronous** and **parallel notifications**
- **Easily extensible** and integrable with other libraries

---

## 📌 Contributions

Contributions are welcome! If you encounter bugs or have suggestions for improvements, please open an issue or submit a pull request.

---

## 📝 License

This project is licensed under the terms of the MIT License.
