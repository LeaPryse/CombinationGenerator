using CombinationGenerator.Application.Exceptions;
using CombinationGenerator.Application.Permutations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CombinationGenerator.Api.ErrorHandling;

/// <summary>
/// Replaces the framework's automatic 400 for model-binding failures (<see cref="ApiBehaviorOptions.InvalidModelStateResponseFactory"/>)
/// so that it has the contract's error shape: <c>errors</c> keyed by the field the client knows (<c>n</c>,
/// <c>pageSize</c>, <c>page</c>) with the contract message, instead of framework keys such as <c>$.n</c> or
/// <c>request</c> and framework messages.
/// </summary>
/// <remarks>
/// Binding fails only for input that cannot be converted at all: a malformed JSON body on <c>start</c>
/// (<c>{"n":"abc"}</c>, <c>{"n":3.5}</c>, <c>{"n":99999999999}</c>, invalid JSON, a JSON array…) or a query value on
/// <c>all</c> that is not an integer of the parameter's type (<c>pageSize=abc</c>, <c>page=1.5</c>,
/// <c>page=9223372036854775808</c>). Range checks normally happen later, in the Application layer; because the action
/// does not run here, the session-independent range checks of the values that <i>did</i> bind are applied by this
/// factory too, so that every offending field is listed (e.g. <c>pageSize=abc&amp;page=0</c>).
/// <list type="bullet">
/// <item>Keys naming <c>pageSize</c> → <see cref="InvalidPageSizeException.ErrorMessage"/>.</item>
/// <item>Keys naming <c>page</c> → <see cref="InvalidPageException.NotAnIntegerMessage"/>.</item>
/// <item>Any other key can only come from the <c>start</c> body (<c>$</c>, <c>$.n</c>, <c>request</c>) →
/// <see cref="InvalidNException.ErrorMessage"/> under <c>n</c>.</item>
/// </list>
/// </remarks>
public static class InvalidModelStateResponse
{
    private const string ValidationTitle = "One or more validation errors occurred.";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    /// <summary>Builds the 400 response for an invalid model state.</summary>
    /// <param name="context">The action context whose model state is invalid.</param>
    /// <returns>A 400 <c>application/problem+json</c> result.</returns>
    public static IActionResult Create(ActionContext context)
    {
        // One contract message per field, even if the framework reported several.
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, entry) in context.ModelState)
        {
            if (entry.Errors.Count == 0)
            {
                continue;
            }

            var (field, message) = MapField(key);
            found.TryAdd(field, message);
        }

        AddRangeErrorsOfBoundArguments(context, found);

        // Stable, documented order (n, pageSize, page); detail repeats the first reported message. The errors are
        // written into the problem's dictionary directly because ModelStateDictionary does not enumerate in insertion
        // order (it would put "page" before "pageSize" and detail would no longer match the first entry).
        var ordered = found.OrderBy(e => FieldRank(e.Key)).ToList();
        var detail = ordered.Count > 0 ? ordered[0].Value : null;

        var httpContext = context.HttpContext;
        var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(InvalidModelStateResponse));
        logger.LogInformation(
            "Request binding failed for {Fields}: {Message}",
            string.Join(", ", ordered.Select(e => e.Key)),
            detail);

        var factory = httpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateValidationProblemDetails(
            httpContext,
            new ModelStateDictionary(),
            StatusCodes.Status400BadRequest,
            ValidationTitle,
            BadRequestType,
            detail);
        problem.Errors.Clear();
        foreach (var (field, message) in ordered)
        {
            problem.Errors[field] = [message];
        }

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    /// <summary>
    /// When one query value of <c>all</c> cannot be parsed, the action does not run, so the Application's range checks
    /// would never see the other values. To list every offending field (contract), the session-independent range
    /// checks (<see cref="PagingValidation"/>) are applied here to the arguments that did bind. The filter that calls
    /// this factory passes the <see cref="ActionExecutingContext"/>, whose <c>ActionArguments</c> holds exactly the
    /// successfully bound values (an absent query value means its default, which is valid).
    /// </summary>
    private static void AddRangeErrorsOfBoundArguments(ActionContext context, Dictionary<string, string> found)
    {
        if (context is not ActionExecutingContext executing)
        {
            return;
        }

        if (!found.ContainsKey(InvalidPageSizeException.FieldName) &&
            executing.ActionArguments.TryGetValue(InvalidPageSizeException.FieldName, out var pageSize) &&
            pageSize is int pageSizeValue &&
            PagingValidation.CheckPageSize(pageSizeValue) is { } pageSizeError)
        {
            found.Add(pageSizeError.Field, pageSizeError.Message);
        }

        if (!found.ContainsKey(InvalidPageException.FieldName) &&
            executing.ActionArguments.TryGetValue(InvalidPageException.FieldName, out var page) &&
            page is long pageValue &&
            PagingValidation.CheckPageLowerBound(pageValue) is { } pageError)
        {
            found.Add(pageError.Field, pageError.Message);
        }
    }

    private static int FieldRank(string field) => field switch
    {
        InvalidNException.FieldName => 0,
        InvalidPageSizeException.FieldName => 1,
        InvalidPageException.FieldName => 2,
        _ => 3,
    };

    private static (string Field, string Message) MapField(string key)
    {
        var name = key.TrimStart('$', '.');
        var lastDot = name.LastIndexOf('.');
        if (lastDot >= 0)
        {
            name = name[(lastDot + 1)..];
        }

        if (string.Equals(name, InvalidPageSizeException.FieldName, StringComparison.OrdinalIgnoreCase))
        {
            return (InvalidPageSizeException.FieldName, InvalidPageSizeException.ErrorMessage);
        }

        if (string.Equals(name, InvalidPageException.FieldName, StringComparison.OrdinalIgnoreCase))
        {
            return (InvalidPageException.FieldName, InvalidPageException.NotAnIntegerMessage);
        }

        return (InvalidNException.FieldName, InvalidNException.ErrorMessage);
    }
}
