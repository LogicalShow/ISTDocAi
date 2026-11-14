using FluentValidation;
using ISTDocAi.Domain.Common.Shared.Response;
using MediatR;

public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse> 
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();



        //--------------------------- credential validation -----------------








    //    Error[] errors = _validators
    //.Select(validator => validator.Validate(request))
    //.SelectMany(validationResult => validationResult.Errors)
    //.Where(validationFailure => validationFailure is not null)
    //.Select(failure => new Error(
    //    failure.PropertyName,
    //    failure.ErrorMessage))
    //.Distinct()
    //.ToArray();

    //    if (errors.Any())
    //    {
    //        return CreateValidationResult<TResponse>(errors);
    //    }

        //-------------------------------------------- async validation -----------------

        var context = new ValidationContext<TRequest>(request);

        // استخدم ValidateAsync واجمع كل النتائج بشكل متوازي
        var results = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken))
        );

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(e => e is not null)
            .ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next();
    }


    private static TResult CreateValidationResult<TResult>(Error[] errors)
     where TResult : Result
    {
        if (typeof(TResult) == typeof(Result))
        {
            return (ValidationResult.WithErrors(errors) as TResult)!;
        }

        object validationResult = typeof(ValidationResult<>)
            .GetGenericTypeDefinition()
            .MakeGenericType(typeof(TResult).GenericTypeArguments[0])
            .GetMethod(nameof(ValidationResult.WithErrors))!
            .Invoke(null, new object?[] { errors })!;

        return (TResult)validationResult;
    }
}
