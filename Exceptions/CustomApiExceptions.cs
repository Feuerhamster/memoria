using Microsoft.AspNetCore.Mvc;

namespace Memoria.Exceptions;

public class CustomApiException : ObjectResult {
	protected CustomApiException(int? statusCode, string error, string? details = null) : base(null) {
		base.StatusCode = statusCode ?? StatusCodes.Status400BadRequest;
		base.Value = new { Error = error, Detail = details };
	}
}

public class LoginFailedApiException(string? details = null)
    : CustomApiException(
        statusCode: StatusCodes.Status400BadRequest,
        error: "login_failed",
        details: details
    )
{
}

public class LogoutFailedApiException(string? details = null)
    : CustomApiException(
        statusCode: StatusCodes.Status400BadRequest,
        error: "logout_failed",
        details: details
    )
{
}

public class ValidationErrorApiException(string? details = null)
    : CustomApiException(
        statusCode: StatusCodes.Status422UnprocessableEntity,
        error: "validation_error",
        details: details
    )
{
}

public class NotFoundApiException(string? details = null)
    : CustomApiException(
        statusCode: StatusCodes.Status404NotFound,
        error: "not_found",
        details: details
    )
{
}

public class OperationFailedApiException(string? details = null)
    : CustomApiException(
        statusCode: StatusCodes.Status500InternalServerError,
        error: "operation_failed",
        details: details
    )
{
}

public class ConflictApiException(string? details = null)
    : CustomApiException(
        statusCode: StatusCodes.Status409Conflict,
        error: "conflict",
        details: details
    )
{
}

public class ActionNotAllowedApiException(string? details = null)
    : CustomApiException(
        statusCode: StatusCodes.Status403Forbidden,
        error: "action_not_allowed",
        details: details
    )
{
}

public class AccessDeniedApiException(string? details = null)
    : CustomApiException(
        statusCode: StatusCodes.Status403Forbidden,
        error: "access_denied",
        details: details
    )
{
}

public class ApplicationUnhealthyApiException(string? details = null)
	: CustomApiException(
		statusCode: StatusCodes.Status503ServiceUnavailable,
		error: "application_unhealthy",
		details: details ?? "The healthcheck on this application resulted in an unhealthy diagnosis"
	)
{
}
