using Microsoft.AspNetCore.Mvc;

namespace Memoria.Exceptions;

public class CustomApiException : ObjectResult {
	protected CustomApiException(int? statusCode, string error, Exception? exception = null) : base(null) {
		base.StatusCode = statusCode ?? StatusCodes.Status400BadRequest;
		base.Value = new { Error = error, Detail = exception?.ToString() };
	}
}

public class LoginFailedApiException(Exception? e = null)
    : CustomApiException(
        statusCode: StatusCodes.Status400BadRequest,
        error: "login_failed",
        exception: e
    )
{
}

public class LogoutFailedApiException(Exception? e = null)
    : CustomApiException(
        statusCode: StatusCodes.Status400BadRequest,
        error: "logout_failed",
        exception: e
    )
{
}

public class ValidationErrorApiException(Exception? e = null)
    : CustomApiException(
        statusCode: StatusCodes.Status422UnprocessableEntity,
        error: "validation_error",
        exception: e
    )
{
}

public class InvalidAuthorizationApiException(Exception? e = null)
    : CustomApiException(
        statusCode: StatusCodes.Status401Unauthorized,
        error: "invalid_authorization",
        exception: e
    )
{
}

public class NotFoundApiException(Exception? e = null)
    : CustomApiException(
        statusCode: StatusCodes.Status404NotFound,
        error: "not_found",
        exception: e
    )
{
}

public class OperationFailedApiException(Exception? e = null)
    : CustomApiException(
        statusCode: StatusCodes.Status500InternalServerError,
        error: "operation_failed",
        exception: e
    )
{
}

public class ConflictApiException(Exception? e = null)
    : CustomApiException(
        statusCode: StatusCodes.Status409Conflict,
        error: "conflict",
        exception: e
    )
{
}

public class ActionNotAllowedApiException(Exception? e = null)
    : CustomApiException(
        statusCode: StatusCodes.Status403Forbidden,
        error: "action_not_allowed",
        exception: e
    )
{
}

public class AccessDeniedApiException(Exception? e = null)
    : CustomApiException(
        statusCode: StatusCodes.Status403Forbidden,
        error: "access_denied",
        exception: e
    )
{
}

public class InvalidRecipeSourceApiException(Exception? e = null)
	: CustomApiException(
		statusCode: StatusCodes.Status400BadRequest,
		error: "invalid_recipe_source",
		exception: e ?? new Exception("This recipe source is invalid or does not work")
	)
{
}

public class ApplicationUnhealthyApiException(Exception? e = null)
	: CustomApiException(
		statusCode: StatusCodes.Status503ServiceUnavailable,
		error: "application_unhealthy",
		exception: e ?? new Exception("The healthcheck on this application resulted in an unhealthy diagnosis")
	)
{
}